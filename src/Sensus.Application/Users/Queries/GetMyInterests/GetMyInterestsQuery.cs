using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Queries.GetMyInterests;

/// <summary>
/// Consulta las categorías de interés del usuario autenticado (Spec 054, FR-054-01).
/// </summary>
public sealed record GetMyInterestsQuery() : IRequest<Result<UserInterestsDto>>, ISensitiveRequest;
