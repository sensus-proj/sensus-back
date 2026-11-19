using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Commands.ShareOfferWithFriends;

public sealed class ShareOfferWithFriendsCommandHandler
    : IRequestHandler<ShareOfferWithFriendsCommand, Result<ShareOfferResultDto>>
{
    public const int MaxRecipients = 10;

    private readonly ICurrentUserService _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IFriendRequestRepository _friendRequestRepository;
    private readonly ISharedOfferRepository _sharedOfferRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushDispatcher _pushDispatcher;
    private readonly INotificationSettingsRepository _notificationSettings;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ShareOfferWithFriendsCommandHandler(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IFriendRequestRepository friendRequestRepository,
        ISharedOfferRepository sharedOfferRepository,
        IDeviceRepository deviceRepository,
        IPushDispatcher pushDispatcher,
        IDateTimeProvider dateTimeProvider,
        INotificationSettingsRepository notificationSettings)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _friendRequestRepository = friendRequestRepository;
        _sharedOfferRepository = sharedOfferRepository;
        _deviceRepository = deviceRepository;
        _pushDispatcher = pushDispatcher;
        _notificationSettings = notificationSettings;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ShareOfferResultDto>> Handle(
        ShareOfferWithFriendsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out Guid senderId))
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para compartir ofertas."));
        }

        List<Guid> targetIds = (request.RecipientUserIds ?? [])
            .Where(id => id != Guid.Empty && id != senderId)
            .Distinct()
            .ToList();

        if (targetIds.Count == 0)
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.Validation("FriendShare.EmptyRecipients", "Selecciona al menos un amigo para compartir la oferta."));
        }

        if (targetIds.Count > MaxRecipients)
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.Validation("FriendShare.TooManyRecipients", $"Puedes compartir a un máximo de {MaxRecipients} amigos a la vez."));
        }

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.NotFound("Offer.NotFound", "La oferta seleccionada no está disponible."));
        }

        User? sender = await _userRepository.GetByIdAsync(senderId, cancellationToken);
        if (sender is null)
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        IReadOnlyList<FriendRequest> relations = await _friendRequestRepository.GetForUserAsync(senderId, cancellationToken);
        HashSet<Guid> acceptedFriendIds = relations
            .Where(r => r.Status == FriendRequestStatus.Accepted)
            .Select(r => r.CounterpartIdFor(senderId))
            .ToHashSet();

        List<Guid> verifiedTargets = targetIds.Where(acceptedFriendIds.Contains).ToList();
        if (verifiedTargets.Count == 0)
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.Forbidden("FriendShare.NotFriends", "Solo puedes compartir ofertas con amigos conectados."));
        }

        Comercio? comercio = await _comercioRepository.GetByIdAsync(offer.MerchantId, cancellationToken);
        string storeName = comercio?.NombreComercial ?? "Comercio local";
        string? category = offer.Category ?? comercio?.Categoria.ToString();
        DateTime nowUtc = _dateTimeProvider.UtcNow;

        List<SharedOffer> createdItems = [];
        foreach (Guid recipientId in verifiedTargets)
        {
            Result<SharedOffer> createResult = SharedOffer.Create(
                senderId,
                recipientId,
                offer.Id,
                offer.Title,
                storeName,
                offer.MerchantId,
                category,
                offer.OriginalPrice,
                offer.DiscountedPrice,
                offer.DiscountPercentage,
                offer.ImageUrl,
                nowUtc);

            if (createResult.IsSuccess)
            {
                createdItems.Add(createResult.Value);
            }
        }

        if (createdItems.Count == 0)
        {
            return Result.Failure<ShareOfferResultDto>(
                Error.Validation("FriendShare.NothingCreated", "No se pudo registrar la oferta compartida."));
        }

        await _sharedOfferRepository.AddRangeAsync(createdItems, cancellationToken);

        foreach (Guid recipientId in verifiedTargets)
        {
            await FriendRequestMappingExtensions.NotifyUserDevicesAsync(
                _deviceRepository,
                _pushDispatcher,
                _notificationSettings,
                recipientId,
                title: "Oferta compartida",
                body: $"{sender.NombreCompleto} te compartió: {offer.Title}.",
                data: new Dictionary<string, string>
                {
                    ["type"] = "friend_offer_shared",
                    ["senderUserId"] = senderId.ToString(),
                    ["offerId"] = offer.Id.ToString()
                },
                cancellationToken);
        }

        return Result.Success(new ShareOfferResultDto(
            SharedCount: createdItems.Count,
            Items: createdItems.Select(x => x.ToDto()).ToList()));
    }
}
