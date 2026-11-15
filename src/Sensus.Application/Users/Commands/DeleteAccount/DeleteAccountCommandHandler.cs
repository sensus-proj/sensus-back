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
    private readonly ICacheService? _cacheService;

    public DeleteAccountCommandHandler(
        IUserRepository userRepository,
        IComercioRepository comercioRepository,
        IPasswordResetTokenRepository tokenRepository,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher,
        IProfileImageStorage imageStorage,
        IOfferRepository offerRepository,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService = null)
    {
        _userRepository = userRepository;
        _comercioRepository = comercioRepository;
        _tokenRepository = tokenRepository;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _imageStorage = imageStorage;
        _offerRepository = offerRepository;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
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

        // Limpieza de foto de perfil en almacenamiento remoto si existe
        if (!string.IsNullOrWhiteSpace(user.FotoPerfilUrl))
        {
            await _imageStorage.DeleteImageAsync(user.FotoPerfilUrl, cancellationToken);
        }

        // FR-030-06: las ofertas se archivan antes de borrar el comercio para que dejen de ser visibles
        await ArchiveMerchantOffersAsync(userId, cancellationToken);

        // Cascada de purga completa: comercio, tokens de recuperación y usuario
        await _comercioRepository.DeleteByUsuarioIdAsync(userId, cancellationToken);
        await _tokenRepository.InvalidateExistingTokensForUserAsync(userId, cancellationToken);
        await _userRepository.DeleteAsync(userId, cancellationToken);

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
