using System.Text.RegularExpressions;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Deterministic heading decision over neutral normalized block evidence.
///
/// Typography is optional. When it is unavailable the evaluator degrades to
/// conservative explicit/uppercase textual evidence rather than fabricating a
/// font hierarchy.
/// </summary>
internal sealed partial class HeadingEvidenceEvaluator
{
    private const int MaximumHeadingCharacters = 180;
    private const int MaximumHeadingWords = 24;
    private const int MinimumHeadingLetterCount = 3;

    private const double MinimumHeadingFontRatio = 1.18;
    private const double SectionFontRatio = 1.30;

    // Explicit structural text can survive ordinary font styling, but strong
    // evidence that it is smaller than body text contradicts the heading claim.
    private const double MinimumExplicitFontRatio = 0.95;

    // Short all-caps labels are common subsection headings. Require a modest
    // typographic lift when typography exists.
    private const double MinimumUppercaseFontRatio = 1.10;

    private readonly double? _bodyFontSize;

    public HeadingEvidenceEvaluator(
        DocumentTextNormalizationResult document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _bodyFontSize =
            GetWeightedMedianFontSize(
                document.Pages
                    .SelectMany(page =>
                        page.Blocks)
                    .Where(block =>
                        !block.IsExcluded &&
                        !string.IsNullOrWhiteSpace(
                            block.Text))
                    .ToArray());
    }

    public bool IsHeading(
        NormalizedDocumentTextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var text =
            block.Text.Trim();

        if (!HasAcceptableHeadingText(text))
        {
            return false;
        }

        var wordCount =
            block.SourceBlock.WordCount;

        if (wordCount >
            MaximumHeadingWords)
        {
            return false;
        }

        var fontRatio =
            GetFontRatio(block);

        if (IsExplicitStructuralHeading(text))
        {
            return fontRatio is null ||
                   fontRatio >=
                   MinimumExplicitFontRatio;
        }

        if (fontRatio is >=
            MinimumHeadingFontRatio)
        {
            if (fontRatio <
                SectionFontRatio &&
                LooksLikeSentence(text))
            {
                return false;
            }

            return true;
        }

        if (IsUppercaseHeading(text))
        {
            return fontRatio is null ||
                   fontRatio >=
                   MinimumUppercaseFontRatio;
        }

        return false;
    }

    private double? GetFontRatio(
        NormalizedDocumentTextBlock block)
    {
        if (_bodyFontSize is null or <= 0 ||
            block.SourceBlock
                .MedianPointSize is null or <= 0)
        {
            return null;
        }

        return block.SourceBlock
                   .MedianPointSize.Value /
               _bodyFontSize.Value;
    }

    private static bool HasAcceptableHeadingText(
        string text)
    {
        if (text.Length == 0 ||
            text.Length >
            MaximumHeadingCharacters ||
            text.Contains(
                '\uFFFD',
                StringComparison.Ordinal))
        {
            return false;
        }

        if (text.Any(char.IsControl))
        {
            return false;
        }

        var letterCount =
            text.Count(char.IsLetter);

        if (letterCount <
            MinimumHeadingLetterCount)
        {
            return false;
        }

        var nonWhitespaceCount =
            text.Count(character =>
                !char.IsWhiteSpace(character));

        var alphaNumericCount =
            text.Count(character =>
                char.IsLetterOrDigit(character));

        return nonWhitespaceCount > 0 &&
               alphaNumericCount * 2 >=
               nonWhitespaceCount;
    }

    private static bool IsExplicitStructuralHeading(
        string text) =>
        ExplicitStructuralHeadingRegex()
            .IsMatch(text);

    private static bool IsUppercaseHeading(
        string text)
    {
        var hasLetter =
            false;

        foreach (var character in text)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            hasLetter = true;

            if (char.IsLower(character))
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static bool LooksLikeSentence(
        string text)
    {
        var trimmed =
            text.TrimEnd();

        return trimmed.EndsWith(
                   ".",
                   StringComparison.Ordinal) ||
               trimmed.EndsWith(
                   ";",
                   StringComparison.Ordinal) ||
               trimmed.EndsWith(
                   ",",
                   StringComparison.Ordinal);
    }

    private static double?
        GetWeightedMedianFontSize(
            IReadOnlyCollection<NormalizedDocumentTextBlock> blocks)
    {
        var samples =
            blocks
                .Where(block =>
                    block.SourceBlock
                        .MedianPointSize is > 0 &&
                    block.SourceBlock
                        .WordCount > 0)
                .Select(block =>
                    new FontSample(
                        block.SourceBlock
                            .MedianPointSize!.Value,
                        Math.Max(
                            1,
                            block.SourceBlock
                                .WordCount)))
                .OrderBy(sample =>
                    sample.PointSize)
                .ToArray();

        if (samples.Length == 0)
        {
            return null;
        }

        var totalWeight =
            samples.Sum(sample =>
                (long)sample.Weight);

        var medianPosition =
            (totalWeight + 1) / 2;

        long accumulatedWeight = 0;

        foreach (var sample in samples)
        {
            accumulatedWeight +=
                sample.Weight;

            if (accumulatedWeight >=
                medianPosition)
            {
                return sample.PointSize;
            }
        }

        return samples[^1]
            .PointSize;
    }

    [GeneratedRegex(
        @"^(?:(?:CHAPTER|PART|SECTION|BOOK)\b|(?:\d+\.\d+(?:\.\d+)*|\d+[.)]|[IVXLCDM]+[.)])\s+\S+)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitStructuralHeadingRegex();

    private sealed record FontSample(
        double PointSize,
        int Weight);
}
