using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Reports.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Reports.Commands.CreateContentReport;

public sealed record CreateContentReportCommand(
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details) : IRequest<Result<ContentReportDto>>, ISensitiveRequest;

public sealed class CreateContentReportCommandValidator : AbstractValidator<CreateContentReportCommand>
{
    public CreateContentReportCommandValidator()
    {
        RuleFor(x => x.TargetType)
            .Must(raw => !string.IsNullOrWhiteSpace(raw))
            .WithMessage("El tipo de contenido reportado es obligatorio.");

        RuleFor(x => x.TargetId)
            .NotEmpty()
            .WithMessage("El identificador del contenido reportado es obligatorio.");

        RuleFor(x => x.Reason)
            .Must(raw => !string.IsNullOrWhiteSpace(raw))
            .WithMessage("El motivo del reporte es obligatorio.");

        RuleFor(x => x.Details)
            .Must(details => details is null || details.Trim().Length <= ContentReport.MaxDetailsLength)
            .WithMessage($"Los detalles adicionales no deben exceder {ContentReport.MaxDetailsLength} caracteres.");
    }
}

public sealed class CreateContentReportCommandHandler : IRequestHandler<CreateContentReportCommand, Result<ContentReportDto>>
{
    private readonly IContentReportRepository _reportRepository;
    private readonly IReportTargetService _targetService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ContentReportSettings _settings;
    private readonly ICdnCacheInvalidator _cdnCacheInvalidator;
    private readonly ILogger<CreateContentReportCommandHandler> _logger;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public CreateContentReportCommandHandler(
        IContentReportRepository reportRepository,
        IReportTargetService targetService,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IOptions<ContentReportSettings> settings,
        ICdnCacheInvalidator cdnCacheInvalidator,
        ILogger<CreateContentReportCommandHandler> logger,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _reportRepository = reportRepository;
        _targetService = targetService;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings.Value;
        _cdnCacheInvalidator = cdnCacheInvalidator;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<Result<ContentReportDto>> Handle(
        CreateContentReportCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId)
            || !Guid.TryParse(_currentUserService.UserId, out Guid reporterUserId)
            || reporterUserId == Guid.Empty)
        {
            return Result.Failure<ContentReportDto>(
                Error.Unauthorized("ContentReport.Unauthorized", "Debes iniciar sesión para reportar contenido."));
        }

        // FR-040-08: exclusión legal absoluta — rechazar y nunca persistir incidentes CSAM / abuso infantil.
        if (ContentReportPolicy.IsProhibitedLegalContent(request.Reason, request.Details))
        {
            string correlationId = _correlationIdAccessor?.CorrelationId ?? "n/a";
            _logger.LogWarning(
                "Intento de reporte de contenido legalmente excluido rechazado sin almacenamiento. CorrelationId: {CorrelationId}",
                correlationId);

            return Result.Failure<ContentReportDto>(
                Error.Validation(
                    "ContentReport.ProhibitedLegalContent",
                    "Este tipo de contenido está fuera del alcance de moderación de la plataforma. Repórtalo directamente ante las autoridades competentes (ej. NCMEC, Fiscalía o línea de emergencia 911/089). El incidente no fue almacenado."));
        }

        if (!ContentReportPolicy.TryParseTargetType(request.TargetType, out ReportTargetType targetType))
        {
            return Result.Failure<ContentReportDto>(
                Error.Validation("ContentReport.InvalidTargetType", "El tipo de contenido debe ser 'offer', 'post' o 'comment'."));
        }

        if (!ContentReportPolicy.TryParseReason(request.Reason, out ReportReason reason))
        {
            return Result.Failure<ContentReportDto>(
                Error.Validation("ContentReport.InvalidReason", "El motivo de reporte no es válido."));
        }

        ReportTargetInfo targetInfo = await _targetService.GetTargetInfoAsync(
            targetType,
            request.TargetId,
            cancellationToken);

        if (!targetInfo.Exists)
        {
            return Result.Failure<ContentReportDto>(
                Error.NotFound("ContentReport.TargetNotFound", "El contenido reportado no existe o ya no está disponible."));
        }

        if (targetInfo.AuthorUserId.HasValue && targetInfo.AuthorUserId.Value == reporterUserId)
        {
            return Result.Failure<ContentReportDto>(
                Error.Validation("ContentReport.SelfReportNotAllowed", "No puedes reportar tu propio contenido."));
        }

        DateTime nowUtc = _dateTimeProvider.UtcNow;
        int windowHours = Math.Max(1, _settings.DeduplicationWindowHours);
        DateTime sinceUtc = nowUtc.AddHours(-windowHours);

        bool alreadyReported = await _reportRepository.ExistsRecentAsync(
            reporterUserId,
            targetType,
            request.TargetId,
            sinceUtc,
            cancellationToken);

        if (alreadyReported)
        {
            return Result.Failure<ContentReportDto>(
                Error.Conflict(
                    "ContentReport.DuplicateReport",
                    "Ya enviaste un reporte para este contenido en las últimas 24 horas."));
        }

        Result<ContentReport> reportResult = ContentReport.Create(
            reporterUserId,
            targetType,
            request.TargetId,
            reason,
            request.Details,
            nowUtc);

        if (reportResult.IsFailure)
        {
            return Result.Failure<ContentReportDto>(reportResult.Error);
        }

        ContentReport report = reportResult.Value;
        bool added = await _reportRepository.TryAddAsync(report, cancellationToken);
        if (!added)
        {
            return Result.Failure<ContentReportDto>(
                Error.Conflict(
                    "ContentReport.DuplicateReport",
                    "Ya enviaste un reporte para este contenido en las últimas 24 horas."));
        }

        int recentCount = await _reportRepository.CountRecentByTargetAsync(
            targetType,
            request.TargetId,
            sinceUtc,
            cancellationToken);

        int autoReviewThreshold = Math.Max(1, _settings.AutoReviewThreshold);
        bool markedForReview = false;
        if (recentCount >= autoReviewThreshold)
        {
            markedForReview = await _targetService.MarkUnderReviewAsync(
                targetType,
                request.TargetId,
                nowUtc,
                cancellationToken);

            if (markedForReview)
            {
                await _cdnCacheInvalidator.InvalidateAsync(request.TargetId.ToString(), cancellationToken);
            }
        }

        _logger.LogInformation(
            "Reporte {ReportId} registrado para {TargetType}:{TargetId}. EnRevision={MarkedForReview}.",
            report.Id,
            targetType.ToApiCode(),
            report.TargetId,
            markedForReview);

        string message = markedForReview
            ? "Reporte recibido. El contenido fue enviado a revisión automática."
            : "Reporte recibido. Nuestro equipo lo revisará si corresponde.";

        return Result.Success(new ContentReportDto(
            report.Id,
            targetType.ToApiCode(),
            report.TargetId,
            reason.ToApiCode(),
            markedForReview,
            report.CreatedAtUtc,
            message));
    }
}
