using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Users.DTOs;
using Sensus.Application.Users.Interests;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.ReplaceMyInterests;

public sealed class ReplaceMyInterestsCommandHandler : IRequestHandler<ReplaceMyInterestsCommand, Result<UserInterestsDto>>
{
    private readonly IUserInterestsRepository _userInterestsRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService? _cacheService;

    public ReplaceMyInterestsCommandHandler(
        IUserInterestsRepository userInterestsRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService = null)
    {
        _userInterestsRepository = userInterestsRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
    }

    public async Task<Result<UserInterestsDto>> Handle(ReplaceMyInterestsCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest)
        {
            return Result.Failure<UserInterestsDto>(
                Error.Forbidden("Interests.GuestForbidden", "Los usuarios invitados no pueden guardar intereses personales."));
        }

        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId) ||
            userId == Guid.Empty)
        {
            return Result.Failure<UserInterestsDto>(
                Error.Unauthorized("Interests.Unauthorized", "Se requiere autenticación para guardar intereses."));
        }

        if (request.Categories is null)
        {
            return Result.Failure<UserInterestsDto>(
                Error.Validation("UserInterests.CategoriesRequired", "La lista de categorías es obligatoria."));
        }

        var parsedCategories = new List<OfferCategory>(request.Categories.Count);
        foreach (string raw in request.Categories)
        {
            if (!OfferCategoryCatalog.TryParse(raw, out OfferCategory parsed))
            {
                return Result.Failure<UserInterestsDto>(
                    Error.Validation(
                        "UserInterests.InvalidCategory",
                        $"Cada categoría debe pertenecer al catálogo OfferCategory: {string.Join(", ", OfferCategoryCatalog.Names)}."));
            }

            parsedCategories.Add(parsed);
        }

        DateTime now = _dateTimeProvider.UtcNow;
        UserInterests? existing = await _userInterestsRepository.GetByUserIdAsync(userId, cancellationToken);

        UserInterests interests;
        if (existing is null)
        {
            Result<UserInterests> created = UserInterests.Create(userId, parsedCategories, now);
            if (created.IsFailure)
            {
                return Result.Failure<UserInterestsDto>(created.Error);
            }

            interests = created.Value;
        }
        else
        {
            Result replaced = existing.Replace(parsedCategories, now);
            if (replaced.IsFailure)
            {
                return Result.Failure<UserInterestsDto>(replaced.Error);
            }

            interests = existing;
        }

        await _userInterestsRepository.UpsertAsync(interests, cancellationToken);

        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync(FeedCacheKeys.Affinity(userId), cancellationToken);
        }

        return Result.Success(interests.ToDto());
    }
}
