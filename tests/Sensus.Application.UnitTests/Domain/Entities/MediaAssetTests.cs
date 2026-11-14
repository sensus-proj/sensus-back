using FluentAssertions;
using Sensus.Domain.Entities;
using Xunit;

namespace Sensus.Application.UnitTests.Domain.Entities;

public sealed class MediaAssetTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var publicId = "sensus/media/abc123/image";
        var secureUrl = "https://res.cloudinary.com/demo/image/upload/abc123.jpg";
        var contentType = "image/jpeg";
        var createdAt = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var asset = MediaAsset.Create(ownerId, publicId, secureUrl, contentType, MediaAssetStatus.Pending, createdAt);

        // Assert
        asset.OwnerId.Should().Be(ownerId);
        asset.PublicId.Should().Be(publicId);
        asset.SecureUrl.Should().Be(secureUrl);
        asset.ContentType.Should().Be(contentType);
        asset.Status.Should().Be(MediaAssetStatus.Pending);
        asset.CreatedAtUtc.Should().Be(createdAt);
        asset.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPublicId_ShouldThrow(string invalidPublicId)
    {
        // Act
        var act = () => MediaAsset.Create(Guid.NewGuid(), invalidPublicId, "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Pending, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyContentType_ShouldThrow(string invalidContentType)
    {
        // Act
        var act = () => MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", invalidContentType, MediaAssetStatus.Pending, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Approve_ShouldSetStatusToApproved()
    {
        // Arrange
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", null, "image/jpeg", MediaAssetStatus.Pending, DateTime.UtcNow);

        // Act
        asset.Approve("https://res.cloudinary.com/demo/image/upload/approved.jpg");

        // Assert
        asset.Status.Should().Be(MediaAssetStatus.Approved);
        asset.SecureUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/approved.jpg");
    }

    [Fact]
    public void Quarantine_ShouldSetStatusToQuarantined()
    {
        // Arrange
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Pending, DateTime.UtcNow);

        // Act
        asset.Quarantine();

        // Assert
        asset.Status.Should().Be(MediaAssetStatus.Quarantined);
    }

    [Fact]
    public void Reject_ShouldSetStatusToRejected()
    {
        // Arrange
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Pending, DateTime.UtcNow);

        // Act
        asset.Reject();

        // Assert
        asset.Status.Should().Be(MediaAssetStatus.Rejected);
    }

    [Fact]
    public void Approve_WithoutSecureUrl_ShouldKeepExistingSecureUrl()
    {
        // Arrange
        var originalUrl = "https://example.com/original.jpg";
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", originalUrl, "image/png", MediaAssetStatus.Pending, DateTime.UtcNow);

        // Act
        asset.Approve();

        // Assert
        asset.Status.Should().Be(MediaAssetStatus.Approved);
        asset.SecureUrl.Should().Be(originalUrl);
    }

    [Fact]
    public void Release_FromQuarantined_ShouldApprove()
    {
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Quarantined, DateTime.UtcNow);

        bool released = asset.Release();

        released.Should().BeTrue();
        asset.Status.Should().Be(MediaAssetStatus.Approved);
    }

    [Theory]
    [InlineData(MediaAssetStatus.Pending)]
    [InlineData(MediaAssetStatus.Approved)]
    [InlineData(MediaAssetStatus.Rejected)]
    public void Release_FromOtherStatus_ShouldNotChangeStatus(MediaAssetStatus status)
    {
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", status, DateTime.UtcNow);

        bool released = asset.Release();

        released.Should().BeFalse();
        asset.Status.Should().Be(status);
    }

    [Fact]
    public void IsQuarantineExpired_WhenQuarantinedBeyondRetention_ShouldBeTrue()
    {
        var created = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Quarantined, created);

        asset.IsQuarantineExpired(created.AddDays(30), TimeSpan.FromDays(30)).Should().BeTrue();
        asset.IsQuarantineExpired(created.AddDays(29), TimeSpan.FromDays(30)).Should().BeFalse();
    }

    [Fact]
    public void IsQuarantineExpired_WhenNotQuarantined_ShouldBeFalse()
    {
        var created = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Approved, created);

        asset.IsQuarantineExpired(created.AddDays(90), TimeSpan.FromDays(30)).Should().BeFalse();
    }

    [Fact]
    public void Purge_ShouldRejectAndClearUrl()
    {
        var asset = MediaAsset.Create(Guid.NewGuid(), "public-id", "https://example.com/img.jpg", "image/jpeg", MediaAssetStatus.Quarantined, DateTime.UtcNow);

        asset.Purge();

        asset.Status.Should().Be(MediaAssetStatus.Rejected);
        asset.SecureUrl.Should().BeNull();
    }
}
