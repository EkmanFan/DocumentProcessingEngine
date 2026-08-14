using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Shared deterministic typography rules for native-backed heading inference.
///
/// Layout-backed hybrid elements do not use this fallback: an explicit neutral
/// LayoutObservation kind is stronger evidence. These rules are used for the
/// legacy native segmenter and for layout-less native elements in the hybrid
/// stream.
/// </summary>
internal sealed class NativeHeadingEvidenceRules
{
    private const int MaximumHeadingCharacters =
        180;

    private const int MaximumHeadingWords =
        24;

    private const int MinimumHeadingLetterCount =
        4;

    private const double MinimumAlphaNumericRatio =
        0.55;

    private const double MinimumHeadingFontRatio =
        1.18;

    private const double SectionFontRatio =
        1.30;

    private readonly double? _bodyFontSize;

    public NativeHeadingEvidenceRules(
        IReadOnlyCollection<DocumentTextBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(
            blocks);

        _bodyFontSize =
            GetWeightedMedianFontSize(
                blocks);
    }

    public bool IsHeading(
        DocumentTextBlock block,
        string normalizedText)
    {
        ArgumentNullException.ThrowIfNull(
            block);

        if (string.IsNullOrWhiteSpace(
                normalizedText))
        {
            return false;
        }

        var text =
            normalizedText.Trim();

        if (!HasAcceptableHeadingText(
                text))
        {
            return false;
        }

        if (block.WordCount >
            MaximumHeadingWords)
        {
            return false;
        }

        var fontRatio =
            GetFontRatio(
                block);

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
        DocumentTextBlock block)
    {
        if (_bodyFontSize is null or <= 0 ||
            block.MedianPointSize is null or <= 0)
        {
            return null;
        }

        return block.MedianPointSize.Value /
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
            text.Count(
                character =>
                    !char.IsWhiteSpace(
                        character));

        if (nonWhitespaceCount == 0)
        {
            return false;
        }

        var alphaNumericCount =
            text.Count(
                character =>
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

    private static double? GetWeightedMedianFontSize(
        IReadOnlyCollection<DocumentTextBlock> blocks)
    {
        var samples =
            blocks
                .Where(
                    block =>
                        block.MedianPointSize is > 0 &&
                        block.WordCount > 0)
                .Select(
                    block =>
                        new FontSample(
                            block.MedianPointSize!.Value,
                            Math.Max(
                                1,
                                block.WordCount)))
                .OrderBy(
                    sample =>
                        sample.PointSize)
                .ToArray();

        if (samples.Length == 0)
        {
            return null;
        }

        var totalWeight =
            samples.Sum(
                sample =>
                    (long)sample.Weight);

        var medianPosition =
            (totalWeight + 1) /
            2;

        long accumulatedWeight =
            0;

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
