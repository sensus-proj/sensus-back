using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Comercios.Commands.RegisterComercio;
using Sensus.Application.Comercios.Commands.UpdateMerchantProfile;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Commands.CreateOffer;
using Sensus.Application.Offers.Commands.RepublishOffer;
using Sensus.Application.Offers.Commands.UpdateOffer;
using Sensus.Application.Users.Commands.RegisterUser;
using Sensus.Application.Users.Commands.UpdateProfile;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Common.Moderation;

public class ContentModerationHandlersTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RegisterUser_ShouldFail_WhenNameContainsOffensiveContent()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var jwt = Substitute.For<IJwtTokenGenerator>();
        var clock = Substitute.For<IDateTimeProvider>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();

        userRepo.ExistsByEmailAsync("test@sensus.com", Arg.Any<CancellationToken>()).Returns(false);
        moderation.ModerateContentTextAsync(
                "Nombre Grosero",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "El nombre contiene palabras antisonantes.", ["profanity"], "Rejected"));

        var handler = new RegisterUserCommandHandler(userRepo, hasher, jwt, clock, audit, moderation);

        var result = await handler.Handle(
            new RegisterUserCommand("Nombre Grosero", "test@sensus.com", "Password123#", true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.NameModerationRejected");
        await userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfile_ShouldFail_WhenNameContainsOffensiveContent()
    {
        var user = User.Create("Usuario Sano", "user@sensus.com", "hash", true, FixedNow).Value;
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();
        var cdnInvalidator = Substitute.For<ICdnCacheInvalidator>();

        currentUser.UserId.Returns(user.Id.ToString());
        userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        moderation.ModerateContentTextAsync(
                "Alias Ofensivo",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "Lenguaje ofensivo detectado.", ["profanity"], "Rejected"));

        var handler = new UpdateProfileCommandHandler(
            userRepo,
            currentUser,
            audit,
            cdnInvalidator,
            textModerationService: moderation);

        var result = await handler.Handle(new UpdateProfileCommand("Alias Ofensivo", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.NameModerationRejected");
        await userRepo.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterComercio_ShouldFail_WhenStoreNameContainsOffensiveContent()
    {
        var user = User.Create("Dueño Sano", "merchant@sensus.com", "hash", true, FixedNow).Value;
        var userRepo = Substitute.For<IUserRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var geocoding = Substitute.For<IGeocodingService>();
        var jwt = Substitute.For<IJwtTokenGenerator>();
        var clock = Substitute.For<IDateTimeProvider>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();

        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns(user.Id.ToString());
        userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        comercioRepo.ExistsByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(false);
        moderation.ModerateContentTextAsync(
                Arg.Is<string>(s => s.Contains("Tienda Grosera")),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "Nombre de comercio prohibido.", ["profanity"], "Rejected"));

        var handler = new RegisterComercioCommandHandler(
            userRepo,
            comercioRepo,
            currentUser,
            geocoding,
            jwt,
            clock,
            audit,
            moderation);

        var result = await handler.Handle(
            new RegisterComercioCommand("Tienda Grosera", "Calle 10 #20", "06000"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Comercio.ContentModerationRejected");
        await comercioRepo.DidNotReceive().AddAsync(Arg.Any<Comercio>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMerchantProfile_ShouldFail_WhenStoreNameContainsOffensiveContent()
    {
        var userId = Guid.NewGuid();
        var comercio = Comercio.Create(userId, "Tienda Sana", "Calle 10 #20", "06000", 19.43, -99.13).Value;
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var geocoding = Substitute.For<IGeocodingService>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();

        currentUser.UserId.Returns(userId.ToString());
        comercioRepo.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        moderation.ModerateContentTextAsync(
                Arg.Is<string>(s => s.Contains("Nombre Vulgar")),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "Nombre comercial ofensivo.", ["profanity"], "Rejected"));

        var handler = new UpdateMerchantProfileCommandHandler(
            comercioRepo,
            currentUser,
            geocoding,
            audit,
            textModerationService: moderation);

        var result = await handler.Handle(
            new UpdateMerchantProfileCommand("Nombre Vulgar", "Calle 10 #20", "06000", null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Comercio.ContentModerationRejected");
        await comercioRepo.DidNotReceive().UpdateAsync(Arg.Any<Comercio>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOffer_ShouldFail_WhenOfferTextContainsOffensiveContent()
    {
        var userId = Guid.NewGuid();
        var comercio = Comercio.Create(userId, "Panadería Sol", "Calle 10 #20", "06000", 19.43, -99.13).Value;
        var offerRepo = Substitute.For<IOfferRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<CreateOfferCommandHandler>>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();

        clock.UtcNow.Returns(FixedNow);
        currentUser.UserId.Returns(userId.ToString());
        comercioRepo.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        moderation.ModerateContentTextAsync(
                Arg.Is<string>(s => s.Contains("Título Ofensivo")),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "La publicación contiene palabras antisonantes.", ["profanity"], "Rejected"));

        var handler = new CreateOfferCommandHandler(
            offerRepo,
            comercioRepo,
            currentUser,
            clock,
            logger,
            audit,
            textModerationService: moderation);

        var result = await handler.Handle(
            new CreateOfferCommand("Título Ofensivo", "Descripción normal", 100m, 20, 5, FixedNow, FixedNow.AddHours(6)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ContentModerationRejected");
        await offerRepo.DidNotReceive().AddAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateOffer_ShouldFail_WhenEditedTextContainsOffensiveContent()
    {
        var userId = Guid.NewGuid();
        var comercio = Comercio.Create(userId, "Panadería Sol", "Calle 10 #20", "06000", 19.43, -99.13).Value;
        var offer = Offer.Create(
            comercio.Id,
            "Pan dulce fresco",
            "Caja con piezas variadas",
            100m,
            20,
            5,
            FixedNow,
            FixedNow.AddHours(6),
            null,
            19.43,
            -99.13,
            FixedNow).Value;

        var offerRepo = Substitute.For<IOfferRepository>();
        var claimRepo = Substitute.For<IOfferClaimRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<UpdateOfferCommandHandler>>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();

        clock.UtcNow.Returns(FixedNow.AddMinutes(5));
        currentUser.UserId.Returns(userId.ToString());
        comercioRepo.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        offerRepo.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        claimRepo.GetActiveClaimCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(0);
        moderation.ModerateContentTextAsync(
                Arg.Is<string>(s => s.Contains("Descripción Grosera")),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "Descripción rechazada por groserías.", ["profanity"], "Rejected"));

        var handler = new UpdateOfferCommandHandler(
            offerRepo,
            claimRepo,
            comercioRepo,
            currentUser,
            clock,
            logger,
            audit,
            textModerationService: moderation);

        var result = await handler.Handle(
            new UpdateOfferCommand(
                offer.Id,
                "Pan dulce fresco",
                "Descripción Grosera para probar",
                100m,
                20,
                5,
                FixedNow,
                FixedNow.AddHours(6),
                null,
                offer.Version),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ContentModerationRejected");
        await offerRepo.DidNotReceive().UpdateWithVersionCheckAsync(Arg.Any<Offer>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepublishOffer_ShouldFail_WhenOfferTextIsQuarantinedFailClosed()
    {
        var userId = Guid.NewGuid();
        var comercio = Comercio.Create(userId, "Panadería Sol", "Calle 10 #20", "06000", 19.43, -99.13).Value;
        var offer = Offer.Create(
            comercio.Id,
            "Oferta previa",
            "Descripción previa",
            100m,
            20,
            5,
            FixedNow.AddDays(-2),
            FixedNow.AddDays(-1),
            null,
            19.43,
            -99.13,
            FixedNow.AddDays(-2)).Value;
        offer.Expire(FixedNow);

        var offerRepo = Substitute.For<IOfferRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<RepublishOfferCommandHandler>>();
        var moderation = Substitute.For<ITextModerationService>();
        var audit = Substitute.For<IModerationAuditRecorder>();

        clock.UtcNow.Returns(FixedNow.AddMinutes(10));
        currentUser.UserId.Returns(userId.ToString());
        comercioRepo.GetByUsuarioIdAsync(userId, Arg.Any<CancellationToken>()).Returns(comercio);
        offerRepo.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        moderation.ModerateContentTextAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, "En revisión preventiva.", ["profanity"], "Quarantine"));

        var handler = new RepublishOfferCommandHandler(
            offerRepo,
            comercioRepo,
            currentUser,
            clock,
            logger,
            audit,
            textModerationService: moderation);

        var result = await handler.Handle(
            new RepublishOfferCommand(offer.Id, FixedNow.AddMinutes(15), FixedNow.AddHours(6), 3),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ContentModerationRejected");
        await offerRepo.DidNotReceive().UpdateAsync(Arg.Any<Offer>(), Arg.Any<CancellationToken>());
    }
}
