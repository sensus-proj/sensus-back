using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Commands.CloseAccountContent;

/// <summary>
/// Cierre comercial y anonimización de contenido al dar de baja una cuenta (Spec 061).
/// Se ejecuta antes de la baja lógica de la spec 060.
/// </summary>
public sealed record CloseAccountContentCommand(Guid UserId) : IRequest<Result<CloseAccountContentResult>>;

/// <summary>Métricas del cierre de contenido, usadas también para la auditoría sin PII.</summary>
public sealed record CloseAccountContentResult(
    int PostsAnonymized,
    int CommentsAnonymized,
    int ClaimsExpired,
    int OffersArchived,
    int AuditEntries);
