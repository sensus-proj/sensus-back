using System.Globalization;
using System.Text;

namespace Sensus.Domain.Enums;

/// <summary>
/// Tipo de contenido objetivo de un reporte comunitario (Spec 040).
/// </summary>
public enum ReportTargetType
{
    Offer = 0,
    Post = 1,
    Comment = 2
}

/// <summary>
/// Motivos tipificados de reporte de contenido (Spec 040 y FT-19).
/// </summary>
public enum ReportReason
{
    Spam = 0,
    Offensive = 1,
    Fraud = 2,
    FalseOffer = 3,
    PriceMismatch = 4,
    Other = 5
}

/// <summary>
/// Utilidades de parseo y exclusión legal absoluta (CSAM) para reportes (Spec 040).
/// </summary>
public static class ContentReportPolicy
{
    private static readonly string[] ProhibitedTerms =
    [
        "csam",
        "abuso infantil",
        "abuso sexual infantil",
        "explotacion infantil",
        "explotacion sexual infantil",
        "pornografia infantil",
        "child abuse",
        "child sexual",
        "child exploitation",
        "child pornography",
        "underage sexual"
    ];

    public static bool TryParseTargetType(string? raw, out ReportTargetType targetType)
    {
        string normalized = (raw ?? string.Empty).Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "OFFER":
            case "OFERTA":
                targetType = ReportTargetType.Offer;
                return true;
            case "POST":
            case "PUBLICACION":
                targetType = ReportTargetType.Post;
                return true;
            case "COMMENT":
            case "COMENTARIO":
                targetType = ReportTargetType.Comment;
                return true;
            default:
                targetType = default;
                return false;
        }
    }

    public static bool TryParseReason(string? raw, out ReportReason reason)
    {
        string normalized = RemoveDiacritics((raw ?? string.Empty).Trim()).ToUpperInvariant();
        switch (normalized)
        {
            case "SPAM":
                reason = ReportReason.Spam;
                return true;
            case "OFENSIVO":
            case "OFFENSIVE":
                reason = ReportReason.Offensive;
                return true;
            case "FRAUDE":
            case "FRAUD":
                reason = ReportReason.Fraud;
                return true;
            case "FALSE_OFFER":
            case "OFERTA_FALSA":
                reason = ReportReason.FalseOffer;
                return true;
            case "PRICE_MISMATCH":
            case "PRECIO_INCORRECTO":
                reason = ReportReason.PriceMismatch;
                return true;
            case "OTRO":
            case "OTHER":
                reason = ReportReason.Other;
                return true;
            default:
                reason = default;
                return false;
        }
    }

    public static string ToApiCode(this ReportTargetType targetType) => targetType switch
    {
        ReportTargetType.Offer => "offer",
        ReportTargetType.Post => "post",
        ReportTargetType.Comment => "comment",
        _ => "offer"
    };

    public static string ToApiCode(this ReportReason reason) => reason switch
    {
        ReportReason.Spam => "spam",
        ReportReason.Offensive => "ofensivo",
        ReportReason.Fraud => "fraude",
        ReportReason.FalseOffer => "false_offer",
        ReportReason.PriceMismatch => "price_mismatch",
        ReportReason.Other => "otro",
        _ => "otro"
    };

    /// <summary>
    /// Detecta menciones de explotación sexual o abuso infantil (CSAM) para rechazar sin almacenar jamás (FR-040-08).
    /// </summary>
    public static bool IsProhibitedLegalContent(string? reason, string? details)
    {
        string combined = $"{RemoveDiacritics(reason)} {RemoveDiacritics(details)}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        foreach (string term in ProhibitedTerms)
        {
            if (combined.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
