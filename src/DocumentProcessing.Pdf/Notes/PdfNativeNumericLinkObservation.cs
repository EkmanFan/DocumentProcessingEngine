using System.Globalization;
using DocumentProcessing.Core.Extraction;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace DocumentProcessing.Pdf.Notes;

/// <summary>
/// PDF-native observation of one numeric link marker and its internal target.
/// </summary>
internal sealed record PdfNativeNumericLinkObservation(
    string Label,
    int PhysicalPageNumber,
    int TargetPhysicalPageNumber,
    int SourceBlockSequence,
    int WordSourceSequence,
    NormalizedRectangle MarkerBounds,
    bool HasEntryPunctuation);

/// <summary>
/// Reads numeric internal-link observations without assigning note semantics.
/// </summary>
internal static class PdfNativeNumericLinkObservationFinder
{
    #region Variables and Constants

    private const int MaximumLabelLength =
        4;

    #endregion

    #region Methods

    public static IReadOnlyList<PdfNativeNumericLinkObservation> Find(
        Page page,
        int physicalPageNumber,
        IReadOnlyList<Word> sourceWords,
        IReadOnlyDictionary<Word, DocumentWord> documentWordBySourceWord,
        IReadOnlyList<DocumentTextBlock> documentBlocks,
        PdfPageCoordinateSpace coordinateSpace)
    {
        ArgumentNullException.ThrowIfNull(
            page);

        ArgumentNullException.ThrowIfNull(
            sourceWords);

        ArgumentNullException.ThrowIfNull(
            documentWordBySourceWord);

        ArgumentNullException.ThrowIfNull(
            documentBlocks);

        var observations =
            new List<PdfNativeNumericLinkObservation>();

        Annotation[] annotations;

        try
        {
            annotations =
                page.GetAnnotations()
                    .ToArray();
        }
        catch (PdfDocumentFormatException)
        {
            return [];
        }

        foreach (var annotation in
                 annotations)
        {
            if (annotation.Type !=
                    AnnotationType.Link ||
                annotation.Action is not
                    GoToAction goTo ||
                goTo.Destination.PageNumber <=
                    0)
            {
                continue;
            }

            var markerLetters =
                page.Letters
                    .Where(
                        letter =>
                            ContainsCenter(
                                annotation.Rectangle,
                                letter.BoundingBox))
                    .OrderBy(
                        letter =>
                            letter.BoundingBox.Left)
                    .ThenBy(
                        letter =>
                            letter.BoundingBox.Bottom)
                    .ToArray();

            if (!TryReadLabel(
                    markerLetters,
                    out var label,
                    out var hasEntryPunctuation))
            {
                continue;
            }

            var markerLetterSet =
                markerLetters.ToHashSet(
                    ReferenceEqualityComparer.Instance);

            var owningWords =
                sourceWords
                    .Where(
                        word =>
                            word.Letters.Any(
                                markerLetterSet.Contains))
                    .ToArray();

            if (owningWords.Length !=
                1)
            {
                continue;
            }

            var documentWord =
                documentWordBySourceWord[
                    owningWords[0]];

            var owningBlocks =
                documentBlocks
                    .Where(
                        block =>
                            block.Words.Any(
                                word =>
                                    word.SourceSequence ==
                                    documentWord.SourceSequence))
                    .ToArray();

            if (owningBlocks.Length !=
                1)
            {
                continue;
            }

            observations.Add(
                new PdfNativeNumericLinkObservation(
                    label,
                    physicalPageNumber,
                    goTo.Destination.PageNumber,
                    owningBlocks[0].SourceSequence,
                    documentWord.SourceSequence,
                    coordinateSpace.ToNormalizedRectangle(
                        annotation.Rectangle),
                    hasEntryPunctuation));
        }

        return observations;
    }

    private static bool TryReadLabel(
        IReadOnlyList<Letter> markerLetters,
        out string label,
        out bool hasEntryPunctuation)
    {
        label =
            string.Empty;

        hasEntryPunctuation =
            false;

        if (markerLetters.Count ==
            0)
        {
            return false;
        }

        var marker =
            string.Concat(
                    markerLetters.Select(
                        letter =>
                            letter.Value))
                .Trim();

        if (marker.EndsWith(
                ".",
                StringComparison.Ordinal) ||
            marker.EndsWith(
                ")",
                StringComparison.Ordinal))
        {
            hasEntryPunctuation =
                true;

            marker =
                marker[..^1];
        }

        if (marker.Length is
                < 1 or
                > MaximumLabelLength ||
            marker.Any(
                character =>
                    !char.IsAsciiDigit(
                        character)) ||
            marker.Length >
                1 &&
            marker[0] ==
                '0' ||
            !int.TryParse(
                marker,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numericLabel) ||
            numericLabel <=
                0)
        {
            return false;
        }

        label =
            numericLabel.ToString(
                CultureInfo.InvariantCulture);

        return true;
    }

    private static bool ContainsCenter(
        PdfRectangle container,
        PdfRectangle candidate)
    {
        var centerX =
            (
                candidate.Left +
                candidate.Right
            ) /
            2;

        var centerY =
            (
                candidate.Bottom +
                candidate.Top
            ) /
            2;

        return centerX >=
                   container.Left &&
               centerX <=
                   container.Right &&
               centerY >=
                   container.Bottom &&
               centerY <=
                   container.Top;
    }

    #endregion
}
