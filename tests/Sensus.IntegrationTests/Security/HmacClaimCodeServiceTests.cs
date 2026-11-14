using FluentAssertions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure.Services;

namespace Sensus.IntegrationTests.Security;

public class HmacClaimCodeServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private static HmacClaimCodeService CreateService(string secret = "test-secret-with-enough-entropy-123456")
    {
        return new HmacClaimCodeService(Options.Create(new ClaimCodeSettings { Secret = secret }));
    }

    [Fact]
    public void Generate_ThenValidate_ShouldReturnOriginalPayload()
    {
        HmacClaimCodeService service = CreateService();
        Guid claimId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();

        ClaimCode generated = service.Generate(claimId, offerId, Now.AddHours(24));

        service.TryValidate(generated.Code, Now, out ClaimCodePayload payload).Should().BeTrue();
        payload.ClaimId.Should().Be(claimId);
        payload.OfferId.Should().Be(offerId);
        payload.ExpiresAtUtc.Should().Be(Now.AddHours(24));
        generated.QrPayload.Should().Be(generated.Code);
    }

    [Fact]
    public void TryValidate_ShouldRejectTamperedCode()
    {
        HmacClaimCodeService service = CreateService();
        ClaimCode generated = service.Generate(Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(24));
        string tampered = generated.Code[..^1] + (generated.Code[^1] == 'A' ? 'B' : 'A');

        service.TryValidate(tampered, Now, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_ShouldRejectCodeSignedWithAnotherSecret()
    {
        ClaimCode generated = CreateService().Generate(Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(24));

        CreateService("another-secret-with-different-entropy-987").TryValidate(generated.Code, Now, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void TryValidate_ShouldRejectExpiredCode()
    {
        HmacClaimCodeService service = CreateService();
        ClaimCode generated = service.Generate(Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(1));

        service.TryValidate(generated.Code, Now.AddHours(2), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-code")]
    [InlineData("v1.a.b.c.d")]
    public void TryValidate_ShouldRejectMalformedCode(string code)
    {
        CreateService().TryValidate(code, Now, out _).Should().BeFalse();
    }

    [Fact]
    public void CodeHash_ShouldBeDeterministicAndNotStorePlainCode()
    {
        HmacClaimCodeService service = CreateService();
        ClaimCode generated = service.Generate(Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(24));

        string first = service.ComputeHash(generated.Code);
        string second = service.ComputeHash(generated.Code);

        first.Should().Be(second);
        first.Should().NotBe(generated.Code);
        first.Should().HaveLength(64);
    }

    [Fact]
    public void Constructor_ShouldFail_WhenSecretIsMissing()
    {
        Action act = () => CreateService(" ");

        act.Should().Throw<InvalidOperationException>();
    }
}
