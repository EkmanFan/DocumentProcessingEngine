using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Deterministic automatic heading decision over neutral normalized block
/// evidence.
///
/// Automatic heading inference requires typography. Textual shape alone is not
/// promoted to structural truth. Source- or caller-supplied editorial heading
/// hints are a separate concern and are intentionally not handled here.
/// </summary>
internal sealed class HeadingEvidenceEvaluator
{
    private const int MaximumHeadingCharacters = 180;
    private const int MaximumHeadingWords = 24;
    private const int MinimumHeadingLetterCount = 4;

    private const double MinimumAlphaNumericRatio = 0.55;
    private const double MinimumHeadingFontRatio = 1.18;
    private const double SectionFontRatio = 1.30;

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

        if (!HasAcceptableHeadingText(
                text))
        {
            return false;
        }

        if (block.SourceBlock.WordCount >
            MaximumHeadingWords)
        {
            return false;
        }

        var fontRatio =
            GetFontRatio(block);

        if (fontRatio is not >=
            MinimumHeadingFontRatio)
        {
            return false;
        }

        if (fontRatio <
            SectionFontRatio &&
            LooksLikeSentence(
                text))
        {
            return false;
        }

        return true;
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
                StringComparison.Ordinal) ||
            text.Any(
                char.IsControl))
        {
            return false;
        }

        var letterCount =
            text.Count(
                char.IsLetter);

        if (letterCount <
            MinimumHeadingLetterCount)
        {
            return false;
        }

        var nonWhitespaceCount =
            text.Count(character =>
                !char.IsWhiteSpace(
                    character));

        if (nonWhitespaceCount == 0)
        {
            return false;
        }

        var alphaNumericCount =
            text.Count(character =>
                char.IsLetterOrDigit(
                    character));

        return alphaNumericCount /
               (double)nonWhitespaceCount >=
               MinimumAlphaNumericRatio;
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
            (totalWeight + 1) /
            2;

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

    private sealed record FontSample(
        double PointSize,
        int Weight);
}
