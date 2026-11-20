using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Commands.ReplaceMyInterests;

/// <summary>
/// Reemplaza la lista completa de categorías de interés del usuario autenticado (Spec 054, FR-054-02).
/// </summary>
public sealed record ReplaceMyInterestsCommand(IReadOnlyList<string> Categories)
    : IRequest<Result<UserInterestsDto>>, ISensitiveRequest;
