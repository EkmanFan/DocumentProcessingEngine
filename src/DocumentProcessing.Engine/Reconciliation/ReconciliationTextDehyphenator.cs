using System.Text;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Reconciliation;

/// <summary>
/// Deterministic dehyphenation for native/OCR reconciliation evidence.
///
/// Native comparable extents and OCR regions expose different boundary
/// evidence, so the rules intentionally differ:
///
/// - native words: U+00AD is explicit discretionary-hyphen evidence; a
///   trailing U+00AD joins to the next lowercase word;
/// - OCR observations: an ASCII '-' is removed only when it ends one OCR
///   observation and the next observation begins with a lowercase letter.
///
/// Ordinary hard hyphens inside native words or inside one OCR observation are
/// preserved. No dictionary, edit distance, language model, OCR confidence, or
/// cross-source text similarity is consulted.
/// </summary>
public static class ReconciliationTextDehyphenator
{
    public const string ProfileId =
        "reconciliation-dehyphenation-v1";

    public static TextDehyphenationResult DehyphenateNative(
        ComparableNativeTextExtent extent)
    {
        ArgumentNullException.ThrowIfNull(extent);

        var fragments =
            extent.Words
                .Select(
                    word =>
                        word.Text)
                .ToArray();

        return Compose(
            fragments,
            BoundaryEvidence.NativeWord);
    }

    public static TextDehyphenationResult DehyphenateOcr(
        OcrRegionResult region)
    {
        ArgumentNullException.ThrowIfNull(region);

        var fragments =
            region.TextObservations
                .OrderBy(
                    observation =>
                        observation.ObservationSequence)
                .Select(
                    observation =>
                        observation.Text.Trim())
                .Where(
                    text =>
                        text.Length > 0)
                .ToArray();

        return Compose(
            fragments,
            BoundaryEvidence.OcrObservation);
    }

    private static TextDehyphenationResult Compose(
        IReadOnlyList<string> fragments,
        BoundaryEvidence boundaryEvidence)
    {
        if (fragments.Count == 0)
        {
            return new TextDehyphenationResult(
                string.Empty,
                softHyphenRemovalCount: 0,
                boundaryJoinCount: 0);
        }

        var builder =
            new StringBuilder();

        var softHyphenRemovalCount =
            0;

        var boundaryJoinCount =
            0;

        for (var index = 0;
             index < fragments.Count;
             index++)
        {
            var original =
                fragments[index];

            var next =
                index + 1 < fragments.Count
                    ? fragments[index + 1]
                    : null;

            var trailingSoftHyphen =
                original.Length > 0 &&
                original[^1] == '\u00AD';

            softHyphenRemovalCount +=
                original.Count(
                    character =>
                        character == '\u00AD');

            var cleaned =
                original.Replace(
                    "\u00AD",
                    string.Empty,
                    StringComparison.Ordinal);

            var joinsNext =
                next is not null &&
                StartsWithLowercaseLetter(next) &&
                (
                    boundaryEvidence == BoundaryEvidence.NativeWord &&
                    trailingSoftHyphen
                    ||
                    boundaryEvidence == BoundaryEvidence.OcrObservation &&
                    cleaned.EndsWith(
                        '-')
                );

            if (joinsNext &&
                boundaryEvidence == BoundaryEvidence.OcrObservation)
            {
                cleaned =
                    cleaned[..^1];
            }

            builder.Append(
                cleaned);

            if (next is null)
            {
                continue;
            }

            if (joinsNext)
            {
                boundaryJoinCount++;
            }
            else
            {
                builder.Append(' ');
            }
        }

        return new TextDehyphenationResult(
            builder.ToString(),
            softHyphenRemovalCount,
            boundaryJoinCount);
    }

    private static bool StartsWithLowercaseLetter(
        string value) =>
        value.Length > 0 &&
        char.IsLower(
            value[0]);

    private enum BoundaryEvidence
    {
        NativeWord = 0,
        OcrObservation = 1
    }
}
