using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sensus.Application.Common.Moderation;

/// <summary>
/// Filtro determinístico local de lenguaje ofensivo / groserías (español e inglés, FR-059-01, NFR-059-01).
/// Actúa como primera línea de defensa instantánea y sin costo de red antes de la moderación con IA.
/// </summary>
public static partial class ProfanityFilter
{
    public const string DefaultRejectionMessage =
        "El texto contiene lenguaje ofensivo o inapropiado. Por favor usa un lenguaje respetuoso.";

    private static readonly HashSet<string> BannedWords = new(StringComparer.Ordinal)
    {
        // Español / modismos México y LatAm
        "pito", "pitos", "pitote", "pitotes", "pitito",
        "puta", "putas", "puto", "putos", "putita", "putitas", "putito", "putitos",
        "putazo", "putazos", "puteria", "putero", "putamadre", "hijoputa", "hijaputa", "hijueputa",
        "pendejo", "pendeja", "pendejos", "pendejas", "pendejada", "pendejadas",
        "verga", "vergas", "vergazo", "vergazos", "verguiza", "vergon", "vergota", "averga",
        "chinga", "chingar", "chingada", "chingado", "chingados", "chingadas", "chingatumadre",
        "chingon", "chingona", "chingadera", "chingaderas", "chinguen", "chingo",
        "cabron", "cabrona", "cabrones", "cabronas",
        "culero", "culera", "culeros", "culeras", "culo", "culos", "culon", "culona",
        "mierda", "mierdas", "mierdero",
        "mamon", "mamona", "mamones", "mamonas", "mamada", "mamadas",
        "marica", "maricas", "maricon", "maricones", "maricona",
        "joto", "jota", "jotos", "jotas",
        "zorra", "zorras", "perra", "perras",
        "pinche", "pinches",
        "cagada", "cagadas", "cagado", "cagados", "cagar", "cagon", "cagona",
        "coño", "cono", "conos", "cojones", "cojon",
        "pija", "pijas", "pijazo", "chupapija",
        "panocha", "panochas", "concha", "conchatumadre",
        "huevon", "huevona", "huevones", "webon", "webona", "webones",
        "boludo", "boluda", "boludos", "pelotudo", "pelotuda", "pelotudos",
        "malparido", "malparida", "malparidos", "gonorrea",
        "gilipollas", "capullo", "idiota", "idiotas", "imbecil", "imbeciles",
        "estupido", "estupida", "estupidos", "estupidas",
        "baboso", "babosa", "ojete", "ojetes", "puñetas", "punetas", "puneton", "puñeton",
        "nalga", "nalgas", "tetas", "chichis", "pene", "penes", "vagina", "vaginas",
        "masturbar", "masturbacion", "porno", "pornografia", "xxx",

        // Inglés
        "fuck", "fucker", "fucking", "fucked", "motherfucker", "motherfucking", "wtf",
        "shit", "shitty", "bullshit",
        "bitch", "bitches", "bitching",
        "asshole", "assholes", "bastard", "bastards",
        "dick", "dicks", "dickhead", "cock", "cocks", "pussy", "pussies",
        "whore", "whores", "slut", "sluts", "cunt", "cunts",
        "faggot", "faggots", "fag", "nigger", "niggers", "nigga", "niggas",
        "retard", "retarded",
    };

    private static readonly string[] BannedPhrases =
    [
        "hijo de puta",
        "hija de puta",
        "hijos de puta",
        "tu puta madre",
        "chinga tu madre",
        "chinga a tu madre",
        "vete a la verga",
        "vale verga",
        "vales verga",
        "me vale verga",
        "come mierda",
        "son of a bitch",
    ];

    public static bool ContainsProfanity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = NormalizeBasic(text);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        try
        {
            // 1. Coincidencia por palabra completa (incluye l33t y repetición de 3+ letras)
            string[] tokens = TokenSplitRegex().Split(normalized);
            foreach (string token in tokens)
            {
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (IsBannedWord(token))
                {
                    return true;
                }
            }

            // 2. Frases ofensivas exactas separadas por espacios simples
            string spaced = string.Join(' ', tokens.Where(t => !string.IsNullOrEmpty(t)));
            foreach (string phrase in BannedPhrases)
            {
                if (ContainsWholePhrase(spaced, phrase))
                {
                    return true;
                }
            }

            // 3. Ofuscación separando letras con espacios/símbolos (e.g. "p.u.t.a", "p i t o")
            foreach (Match match in SeparatedSingleCharsRegex().Matches(normalized))
            {
                string joined = NonAlnumRegex().Replace(match.Value, string.Empty);
                if (IsBannedWord(joined))
                {
                    return true;
                }
            }

            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            // NFR-059-01: fail-closed si una entrada maliciosa dispara timeout en regex.
            return true;
        }
    }

    private static bool IsBannedWord(string token)
    {
        if (BannedWords.Contains(token))
        {
            return true;
        }

        string collapsedTripleSingle = TripleOrMoreCharsRegex().Replace(token, "$1");
        if (BannedWords.Contains(collapsedTripleSingle))
        {
            return true;
        }

        string collapsedTripleDouble = TripleOrMoreCharsRegex().Replace(token, "$1$1");
        return BannedWords.Contains(collapsedTripleDouble);
    }

    private static bool ContainsWholePhrase(string normalizedSpacedText, string phrase)
    {
        int idx = normalizedSpacedText.IndexOf(phrase, StringComparison.Ordinal);
        while (idx >= 0)
        {
            bool leftOk = idx == 0 || normalizedSpacedText[idx - 1] == ' ';
            int end = idx + phrase.Length;
            bool rightOk = end == normalizedSpacedText.Length || normalizedSpacedText[end] == ' ';
            if (leftOk && rightOk)
            {
                return true;
            }

            idx = normalizedSpacedText.IndexOf(phrase, idx + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string NormalizeBasic(string input)
    {
        string decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char ch in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char lower = char.ToLowerInvariant(ch);
            char mapped = lower switch
            {
                '0' => 'o',
                '1' or '!' or '|' => 'i',
                '3' => 'e',
                '4' or '@' => 'a',
                '5' or '$' => 's',
                '7' => 't',
                _ => lower,
            };
            sb.Append(mapped);
        }

        return sb.ToString();
    }

    [GeneratedRegex("[^a-z0-9ñ]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex TokenSplitRegex();

    [GeneratedRegex("([a-z0-9ñ])\\1{2,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex TripleOrMoreCharsRegex();

    [GeneratedRegex("(?:\\b[a-z0-9ñ]\\b[\\s._\\-*]+){2,}\\b[a-z0-9ñ]\\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex SeparatedSingleCharsRegex();

    [GeneratedRegex("[^a-z0-9ñ]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex NonAlnumRegex();
}
