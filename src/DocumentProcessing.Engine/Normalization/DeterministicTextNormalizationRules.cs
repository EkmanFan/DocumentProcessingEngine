using System.Text;
using System.Text.RegularExpressions;

namespace DocumentProcessing.Engine.Normalization;

/// <summary>
/// Shared deterministic textual rules used after authoritative text has already
/// been selected.
///
/// This is deliberately not a source-selection component. It does not inspect
/// OCR confidence, fuzzy similarity, document semantics, or model output.
/// </summary>
internal static partial class DeterministicTextNormalizationRules
{
    public static string Normalize(
        string sourceText)
    {
        ArgumentNullException.ThrowIfNull(
            sourceText);

        var normalized =
            sourceText
                .Normalize(
                    NormalizationForm.FormC)
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n');

        normalized =
            DehyphenationRegex()
                .Replace(
                    normalized,
                    string.Empty);

        return WhitespaceRegex()
            .Replace(
                normalized,
                " ")
            .Trim();
    }

    public static string CanonicalizeRecurringText(
        string text)
    {
        var normalized =
            Normalize(
                    text)
                .ToUpperInvariant();

        return DigitRunRegex()
            .Replace(
                normalized,
                "#");
    }

    [GeneratedRegex(
        @"(?<=\p{L})-[\t ]*\n[\t ]*(?=\p{Ll})",
        RegexOptions.CultureInvariant)]
    private static partial Regex DehyphenationRegex();

    [GeneratedRegex(
        @"\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        @"\d+",
        RegexOptions.CultureInvariant)]
    private static partial Regex DigitRunRegex();
}
