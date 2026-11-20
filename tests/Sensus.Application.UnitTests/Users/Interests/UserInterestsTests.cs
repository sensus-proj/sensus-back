using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Application.Users.Commands.ReplaceMyInterests;
using Sensus.Application.Users.DTOs;
using Sensus.Application.Users.Interests;
using Sensus.Application.Users.Queries.GetMyInterests;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Interests;

public sealed class UserInterestsTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_AndReplace_ShouldEnforceInvariants()
    {
        UserInterests.Create(Guid.Empty, [OfferCategory.Bakery], Now).IsFailure.Should().BeTrue();
        UserInterests.Create(Guid.NewGuid(), null!, Now).IsFailure.Should().BeTrue();
        UserInterests.Create(Guid.NewGuid(), [(OfferCategory)999], Now).IsFailure.Should().BeTrue();

        Result<UserInterests> dup = UserInterests.Create(
            Guid.NewGuid(),
            [OfferCategory.Bakery, OfferCategory.Bakery],
            Now);
        dup.IsFailure.Should().BeTrue();
        dup.Error.Code.Should().Be("UserInterests.DuplicateCategory");

        Result<UserInterests> tooMany = UserInterests.Create(
            Guid.NewGuid(),
            [
                OfferCategory.Grocery,
                OfferCategory.CleaningHome,
                OfferCategory.Perishables,
                OfferCategory.Dairy,
                OfferCategory.Food,
                OfferCategory.Bakery
            ],
            Now);
        tooMany.IsFailure.Should().BeTrue();
        tooMany.Error.Code.Should().Be("UserInterests.TooManyCategories");

        Result<UserInterests> empty = UserInterests.Create(Guid.NewGuid(), [], Now);
        empty.IsSuccess.Should().BeTrue();
        empty.Value.Categories.Should().BeEmpty();

        Result replaced = empty.Value.Replace([OfferCategory.Bakery, OfferCategory.Dairy], Now.AddMinutes(5));
        replaced.IsSuccess.Should().BeTrue();
        empty.Value.Categories.Should().Equal(OfferCategory.Bakery, OfferCategory.Dairy);
        empty.Value.UpdatedAtUtc.Should().Be(Now.AddMinutes(5));

        empty.Value.Replace([OfferCategory.Food, OfferCategory.Food], Now.AddMinutes(6)).IsFailure.Should().BeTrue();
        empty.Value.Categories.Should().Equal(OfferCategory.Bakery, OfferCategory.Dairy);
    }

    [Fact]
    public void ToDto_ShouldMapNullToEmptyList_AndCategoriesToStrings()
    {
        UserInterests? missing = null;
        missing.ToDto().Categories.Should().BeEmpty();

        UserInterests interests = UserInterests.Create(
            Guid.NewGuid(),
            [OfferCategory.Bakery, OfferCategory.Dairy],
            Now).Value;

        UserInterestsDto dto = interests.ToDto();
        dto.Categories.Should().Equal("Bakery", "Dairy");
    }

    [Fact]
    public void Validator_ShouldAcceptEmptyAndUpToFiveValid_AndRejectInvalidOrDuplicates()
    {
        var validator = new ReplaceMyInterestsCommandValidator();

        validator.Validate(new ReplaceMyInterestsCommand([])).IsValid.Should().BeTrue();
        validator.Validate(new ReplaceMyInterestsCommand(["Bakery", "Dairy", "Pets", "Food", "Pharmacy"])).IsValid.Should().BeTrue();

        validator.Validate(new ReplaceMyInterestsCommand(null!)).IsValid.Should().BeFalse();
        validator.Validate(new ReplaceMyInterestsCommand(["Moda"])).IsValid.Should().BeFalse();
        validator.Validate(new ReplaceMyInterestsCommand(["Bakery", "bakery"])).IsValid.Should().BeFalse();
        validator.Validate(new ReplaceMyInterestsCommand(
            ["Grocery", "CleaningHome", "Perishables", "Dairy", "Food", "Bakery"])).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyInterestsHandler_ShouldHandleAuthGuestAndEmpty()
    {
        var repo = Substitute.For<IUserInterestsRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var handler = new GetMyInterestsQueryHandler(repo, currentUser);

        currentUser.IsGuest.Returns(true);
        (await handler.Handle(new GetMyInterestsQuery(), CancellationToken.None))
            .Error.Type.Should().Be(ErrorType.Forbidden);

        currentUser.IsGuest.Returns(false);
        currentUser.UserId.Returns((string?)null);
        (await handler.Handle(new GetMyInterestsQuery(), CancellationToken.None))
            .Error.Type.Should().Be(ErrorType.Unauthorized);

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((UserInterests?)null);

        Result<UserInterestsDto> emptyResult = await handler.Handle(new GetMyInterestsQuery(), CancellationToken.None);
        emptyResult.IsSuccess.Should().BeTrue();
        emptyResult.Value.Categories.Should().BeEmpty();

        var existing = UserInterests.Create(userId, [OfferCategory.Pets], Now).Value;
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existing);

        Result<UserInterestsDto> populated = await handler.Handle(new GetMyInterestsQuery(), CancellationToken.None);
        populated.IsSuccess.Should().BeTrue();
        populated.Value.Categories.Should().Equal("Pets");
    }

    [Fact]
    public async Task ReplaceMyInterestsHandler_ShouldUpsertAndInvalidateAffinityCache()
    {
        var repo = Substitute.For<IUserInterestsRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var cache = Substitute.For<ICacheService>();

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.IsGuest.Returns(false);
        clock.UtcNow.Returns(Now);

        var handler = new ReplaceMyInterestsCommandHandler(repo, currentUser, clock, cache);

        Result<UserInterestsDto> badCat = await handler.Handle(
            new ReplaceMyInterestsCommand(["Moda"]), CancellationToken.None);
        badCat.IsFailure.Should().BeTrue();
        badCat.Error.Code.Should().Be("UserInterests.InvalidCategory");

        Result<UserInterestsDto> created = await handler.Handle(
            new ReplaceMyInterestsCommand(["Bakery", "Dairy"]), CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        created.Value.Categories.Should().Equal("Bakery", "Dairy");

        await repo.Received(1).UpsertAsync(
            Arg.Is<UserInterests>(u => u.UserId == userId && u.Categories.SequenceEqual(new[] { OfferCategory.Bakery, OfferCategory.Dairy })),
            Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(FeedCacheKeys.Affinity(userId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountHandler_ShouldPurgeUserInterestsAndInvalidateAffinityCache()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var comercioRepo = Substitute.For<IComercioRepository>();
        var tokenRepo = Substitute.For<IPasswordResetTokenRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var hasher = Substitute.For<IPasswordHasher>();
        var storage = Substitute.For<IProfileImageStorage>();
        var offerRepo = Substitute.For<IOfferRepository>();
        var clock = Substitute.For<IDateTimeProvider>();
        var cache = Substitute.For<ICacheService>();
        var interestsRepo = Substitute.For<IUserInterestsRepository>();

        User user = User.Create("Ana", "ana@sensus.com", "hash", true, Now).Value;
        currentUser.UserId.Returns(user.Id.ToString());
        userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        hasher.VerifyPassword("Pass123!", "hash").Returns(true);
        comercioRepo.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns((Comercio?)null);

        var handler = new DeleteAccountCommandHandler(
            userRepo,
            comercioRepo,
            tokenRepo,
            currentUser,
            hasher,
            storage,
            offerRepo,
            clock,
            cache,
            userInterestsRepository: interestsRepo);

        Result result = await handler.Handle(new DeleteAccountCommand("Pass123!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await interestsRepo.Received(1).DeleteByUserIdAsync(user.Id, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(FeedCacheKeys.Affinity(user.Id), Arg.Any<CancellationToken>());
    }
}
