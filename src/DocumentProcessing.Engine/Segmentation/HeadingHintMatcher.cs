using System.Text.RegularExpressions;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Deterministic matcher for explicit caller-provided editorial heading hints.
///
/// Matching intentionally does not reuse the automatic typography quality gate:
/// a hint is explicit external evidence. The containing segmenter still ignores
/// excluded and empty normalized text before consulting this matcher.
/// </summary>
internal sealed class HeadingHintMatcher
{
    private static readonly Regex WhitespaceRegex =
        new(
            @"\s+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private readonly HintKey[] _hints;

    public HeadingHintMatcher(
        IReadOnlyList<string> hints)
    {
        ArgumentNullException.ThrowIfNull(
            hints);

        _hints =
            hints
                .Select(
                    hint =>
                        new HintKey(
                            NormalizeHeadingKey(
                                hint),
                            CompactHeadingKey(
                                hint)))
                .ToArray();
    }

    public bool IsHeading(
        NormalizedDocumentTextBlock block)
    {
        ArgumentNullException.ThrowIfNull(
            block);

        return IsHeading(
            block.Text,
            block.SourceText);
    }

    /// <summary>
    /// Source-agnostic overload used by the hybrid segmenter. The caller remains
    /// responsible for ensuring the candidate is eligible structural text.
    /// </summary>
    public bool IsHeading(
        string normalizedText,
        string sourceText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            normalizedText);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceText);

        if (_hints.Length == 0)
        {
            return false;
        }

        var normalizedCandidate =
            NormalizeHeadingKey(
                normalizedText);

        var sourceFirstLine =
            sourceText
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n')
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .FirstOrDefault() ??
            normalizedText;

        var compactFirstLine =
            CompactHeadingKey(
                sourceFirstLine);

        foreach (var hint in _hints)
        {
            if (string.Equals(
                    normalizedCandidate,
                    hint.Normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (MatchesDecoratedSuffix(
                    normalizedCandidate,
                    hint.Normalized))
            {
                return true;
            }

            if (string.Equals(
                    compactFirstLine,
                    hint.Compact,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesDecoratedSuffix(
        string candidate,
        string hint)
    {
        if (!candidate.EndsWith(
                hint,
                StringComparison.Ordinal))
        {
            return false;
        }

        var prefixLength =
            candidate.Length -
            hint.Length;

        if (prefixLength <= 0)
        {
            return false;
        }

        var prefix =
            candidate[
                    ..prefixLength]
                .Trim();

        return prefix.Length is > 0 and <= 3 &&
               prefix.All(
                   character =>
                       !char.IsLetter(
                           character) ||
                       char.IsUpper(
                           character));
    }

    private static string NormalizeHeadingKey(
        string heading)
    {
        var normalized =
            WhitespaceRegex
                .Replace(
                    heading,
                    " ")
                .Trim();

        var start =
            0;

        while (start <
               normalized.Length &&
               !char.IsLetterOrDigit(
                   normalized[start]))
        {
            start++;
        }

        var end =
            normalized.Length -
            1;

        while (end >= start &&
               !char.IsLetterOrDigit(
                   normalized[end]))
        {
            end--;
        }

        if (start > end)
        {
            return string.Empty;
        }

        return normalized[
                start..(end + 1)]
            .ToUpperInvariant();
    }

    private static string CompactHeadingKey(
        string heading) =>
        new(
            heading
                .Where(
                    char.IsLetterOrDigit)
                .Select(
                    char.ToUpperInvariant)
                .ToArray());

    private sealed record HintKey(
        string Normalized,
        string Compact);
}
