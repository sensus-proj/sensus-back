using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.DeleteAccount;

public sealed class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IProfileImageStorage _imageStorage;
    private readonly IOfferRepository _offerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICdnCacheInvalidator _cdnCacheInvalidator;
    private readonly ICacheService? _cacheService;
    private readonly IAiConversationRepository? _conversationRepository;
    private readonly IAiMessageRepository? _messageRepository;
    private readonly IAiTraceLogRepository? _traceLogRepository;
    private readonly INotificationLocationRepository? _notificationLocations;
    private readonly IUserInterestsRepository? _userInterestsRepository;
    private readonly IOfferInteractionRecorder? _offerInteractionRecorder;

    public DeleteAccountCommandHandler(
        IUserRepository userRepository,
        IComercioRepository comercioRepository,
        IPasswordResetTokenRepository tokenRepository,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher,
        IProfileImageStorage imageStorage,
        IOfferRepository offerRepository,
        IDateTimeProvider dateTimeProvider,
        ICdnCacheInvalidator cdnCacheInvalidator,
        ICacheService? cacheService = null,
        IAiConversationRepository? conversationRepository = null,
        IAiMessageRepository? messageRepository = null,
        IAiTraceLogRepository? traceLogRepository = null,
        INotificationLocationRepository? notificationLocations = null,
        IUserInterestsRepository? userInterestsRepository = null,
        IOfferInteractionRecorder? offerInteractionRecorder = null)
    {
        _userRepository = userRepository;
        _comercioRepository = comercioRepository;
        _tokenRepository = tokenRepository;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _imageStorage = imageStorage;
        _offerRepository = offerRepository;
        _dateTimeProvider = dateTimeProvider;
        _cdnCacheInvalidator = cdnCacheInvalidator;
        _cacheService = cacheService;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _traceLogRepository = traceLogRepository;
        _notificationLocations = notificationLocations;
        _userInterestsRepository = userInterestsRepository;
        _offerInteractionRecorder = offerInteractionRecorder;
    }

    public async Task<Result> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var rawUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var userId))
        {
            return Result.Failure(Error.Unauthorized("Auth.Unauthorized", "Se requiere sesión activa para eliminar la cuenta."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", "No se encontró el usuario a eliminar."));
        }

        // Re-autenticación obligatoria con contraseña actual para evitar eliminaciones maliciosas
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result.Failure(Error.Unauthorized("Auth.InvalidCredentials", "La contraseña provista para confirmar la eliminación es incorrecta."));
        }

        // Limpieza de foto de perfil en almacenamiento remoto si existe e invalidación en CDN (CA-023-03)
        if (!string.IsNullOrWhiteSpace(user.FotoPerfilUrl))
        {
            await _imageStorage.DeleteImageAsync(user.FotoPerfilUrl, cancellationToken);
            await _cdnCacheInvalidator.InvalidateAsync(user.FotoPerfilUrl, cancellationToken);
        }

        // FR-030-06: las ofertas se archivan antes de borrar el comercio para que dejen de ser visibles
        await ArchiveMerchantOffersAsync(userId, cancellationToken);

        // Cascada de purga completa: comercio, tokens de recuperación, datos de Sensi IA y usuario
        await _comercioRepository.DeleteByUsuarioIdAsync(userId, cancellationToken);
        await _tokenRepository.InvalidateExistingTokensForUserAsync(userId, cancellationToken);

        // FR-046-15: Purga en cascada de conversaciones, mensajes y trazas de auditoría de Sensi IA
        if (_conversationRepository is not null)
        {
            await _conversationRepository.PurgeAllByUserIdAsync(userId, cancellationToken);
        }

        if (_messageRepository is not null)
        {
            await _messageRepository.PurgeAllByUserIdAsync(userId, cancellationToken);
        }

        if (_traceLogRepository is not null)
        {
            await _traceLogRepository.PurgeAllByUserIdAsync(userId, cancellationToken);
        }

        await _userRepository.DeleteAsync(userId, cancellationToken);

        if (_notificationLocations is not null)
        {
            await _notificationLocations.DeleteAsync(userId, cancellationToken);
        }

        // FR-054-06 / FR-030-08: Purga inmediata de intereses del usuario e invalidación de caché de afinidad
        if (_userInterestsRepository is not null)
        {
            await _userInterestsRepository.DeleteByUserIdAsync(userId, cancellationToken);
        }

        // FR-055-07 / FR-030-08: Purga inmediata de interacciones seudonimizadas con todas las versiones de clave HMAC
        if (_offerInteractionRecorder is not null)
        {
            await _offerInteractionRecorder.PurgeByUserAsync(userId, cancellationToken);
        }

        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync(FeedCacheKeys.Affinity(userId), cancellationToken);
        }

        return Result.Success();
    }

    private async Task ArchiveMerchantOffersAsync(Guid userId, CancellationToken cancellationToken)
    {
        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null)
        {
            return;
        }

        IReadOnlyList<Offer> offers = await _offerRepository.GetByMerchantIdAsync(comercio.Id, cancellationToken);
        DateTime now = _dateTimeProvider.UtcNow;

        foreach (Offer offer in offers.Where(o => o.Status != OfferStatus.Archived))
        {
            offer.Archive(now);
            await _offerRepository.UpdateAsync(offer, cancellationToken);
            await _cacheService.InvalidateAsync(offer.Id, cancellationToken);
        }
    }
}
