using FluentAssertions;
using Sensus.Application.Ai;
using Xunit;

namespace Sensus.Application.UnitTests.Ai;

/// <summary>
/// Pruebas de normalización y hashing seguro de claves de caché FAQ de Sensi IA (FR-046-13, Spec 048).
/// </summary>
public sealed class AiCacheKeysTests
{
    [Theory]
    [InlineData("¿Cómo funciona el QR?", "como funciona el qr")]
    [InlineData("  ¿CÓMO   funciona\t\nel  QR?! ", "como funciona el qr")]
    [InlineData("¿Dónde puedo ver mis apartados?", "donde puedo ver mis apartados")]
    public void NormalizePrompt_ShouldNormalizeAccentsCaseAndSeparators(string input, string expected)
    {
        AiCacheKeys.NormalizePrompt(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizePrompt_ShouldReturnEmpty_WhenInputIsNullOrWhitespace()
    {
        AiCacheKeys.NormalizePrompt(null).Should().BeEmpty();
        AiCacheKeys.NormalizePrompt("   \t ").Should().BeEmpty();
    }

    [Fact]
    public void Faq_ShouldProduceDeterministicHashedKey_WithoutExposingPromptText()
    {
        string keyA = AiCacheKeys.Faq("1.0.0", "Consumidor", "¿Cómo funciona el QR?");
        string keyB = AiCacheKeys.Faq("1.0.0", "Consumidor", "como funciona el qr");

        keyA.Should().Be(keyB);
        keyA.Should().StartWith($"{AiCacheKeys.FaqPrefix}1.0.0:consumidor:none:");
        keyA.Should().NotContain("funciona");
        keyA.Should().NotContain("qr");
    }

    [Fact]
    public void Faq_ShouldIsolateByMarkdownVersionAndRole()
    {
        string consumerV1 = AiCacheKeys.Faq("1.0.0", "Consumidor", "¿Cómo funciona el QR?");
        string merchantV1 = AiCacheKeys.Faq("1.0.0", "Comerciante", "¿Cómo funciona el QR?");
        string consumerV2 = AiCacheKeys.Faq("1.1.0", "Consumidor", "¿Cómo funciona el QR?");

        consumerV1.Should().NotBe(merchantV1);
        consumerV1.Should().NotBe(consumerV2);
    }

    [Fact]
    public void Faq_ShouldIsolateByScreenContext()
    {
        string homeKey = AiCacheKeys.Faq("1.0.0", "Consumidor", "¿Qué hago aquí?", "home");
        string claimsKey = AiCacheKeys.Faq("1.0.0", "Consumidor", "¿Qué hago aquí?", "my_claims");
        string nullKey = AiCacheKeys.Faq("1.0.0", "Consumidor", "¿Qué hago aquí?", null);
        string emptyKey = AiCacheKeys.Faq("1.0.0", "Consumidor", "¿Qué hago aquí?", "   ");

        homeKey.Should().Contain(":home:");
        claimsKey.Should().Contain(":my_claims:");
        homeKey.Should().NotBe(claimsKey);
        homeKey.Should().NotBe(nullKey);
        nullKey.Should().Be(emptyKey);
        nullKey.Should().Contain(":none:");
    }
}
