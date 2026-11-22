using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.RegisterUser;

/// <summary>
/// Registra un nuevo usuario con moderación de nombre y auditoría fail-closed (Spec 021, B3).
/// Ref: FR-021-01 - auditoría de cada decisión de moderación antes de persistir.
/// </summary>
public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITextModerationService? _textModerationService;
    private readonly IModerationAuditRecorder _auditRecorder;

    public RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IModerationAuditRecorder auditRecorder,
        ITextModerationService? textModerationService = null)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _auditRecorder = auditRecorder;
        _textModerationService = textModerationService;
    }

    public async Task<Result<AuthResponseDto>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        var emailExists = await _userRepository.ExistsByEmailAsync(request.Correo, cancellationToken);
        if (emailExists)
        {
            return Result.Failure<AuthResponseDto>(
                Error.Conflict("User.EmailExists", "El correo electrónico ya se encuentra registrado."));
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var now = _dateTimeProvider.UtcNow;

        var createResult = User.Create(
            request.NombreCompleto,
            request.Correo,
            passwordHash,
            request.AceptoTerminos,
            now);

        if (createResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(createResult.Error);
        }

        var user = createResult.Value;

        ModerationEvaluation evaluation = await ContentModerationHelper.EvaluateAsync(
            _textModerationService,
            request.NombreCompleto,
            "Nombre de usuario en registro de cuenta",
            "User.NameModerationRejected",
            "El nombre de usuario contiene lenguaje antisonante u ofensivo.",
            cancellationToken);

        var auditEntry = new ModerationAuditEntry(
            user.Id,
            ModerationTargetType.User,
            evaluation.Verdict,
            evaluation.Decision,
            "user_register",
            evaluation.FlaggedCategories,
            evaluation.CategoryScores,
            ActorId: user.Id.ToString(),
            ActorType: ModerationActorType.User);

        if (!evaluation.IsApproved)
        {
            await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);
            return Result.Failure<AuthResponseDto>(evaluation.RejectionError ?? Error.Validation("User.ContentModerationRejected", "Contenido rechazado por moderación."));
        }

        Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
        if (auditResult.IsFailure)
        {
            return Result.Failure<AuthResponseDto>(auditResult.Error);
        }

        await _userRepository.AddAsync(user, cancellationToken);

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        var response = new AuthResponseDto(
            token,
            "Bearer",
            expiresAt,
            user.ToDto());

        return Result.Success(response);
    }
}
