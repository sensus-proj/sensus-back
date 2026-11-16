using FluentAssertions;
using Sensus.Infrastructure.Knowledge;
using Xunit;

namespace Sensus.IntegrationTests.Knowledge;

public sealed class KnowledgeBaseProviderTests
{
    [Fact]
    public void GetDocument_ReturnsValidVersion_HashAndContent()
    {
        // Arrange
        var provider = new KnowledgeBaseProvider();

        // Act
        var doc = provider.GetDocument();

        // Assert
        doc.Should().NotBeNull();
        doc.Version.Should().Be("1.0.0");
        doc.HashSha256.Should().HaveLength(64); // SHA-256 hex string
        doc.Content.Should().Contain("Sensus");
        doc.Content.Should().Contain("Click & Collect");
        doc.Content.Should().Contain("QR");
        doc.Content.Should().Contain("95.4 %");
        doc.Content.Should().Contain("4.4 %");
        doc.Content.Length.Should().BeGreaterThan(1000);
    }
}
