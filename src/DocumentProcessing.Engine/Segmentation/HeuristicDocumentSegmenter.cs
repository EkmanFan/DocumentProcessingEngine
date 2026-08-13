using System.Text.RegularExpressions;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Segmentation;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Minimal deterministic structural segmenter.
///
/// V1 is intentionally page-bounded. It starts a new segment only on
/// conservative, text-only heading evidence and otherwise falls back to one
/// content segment per physical page.
/// </summary>
public sealed partial class HeuristicDocumentSegmenter :
    IDocumentSegmenter
{
    public const string SegmentationProfileId =
        "page-bounded-obvious-headings-v1";

    private const int MaximumHeadingLength = 120;
    private const int MaximumHeadingWordCount = 14;
    private const int MinimumHeadingLetterCount = 3;

    public DocumentSegmentationResult Segment(
        DocumentTextNormalizationResult document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var segments =
            new List<DocumentSegment>();

        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SegmentPage(
                page,
                segments,
                cancellationToken);
        }

        return new DocumentSegmentationResult(
            document,
            SegmentationProfileId,
            segments);
    }

    private static void SegmentPage(
        NormalizedDocumentPage page,
        ICollection<DocumentSegment> output,
        CancellationToken cancellationToken)
    {
        var eligibleBlocks =
            page.Blocks
                .Where(block =>
                    !block.IsExcluded &&
                    !string.IsNullOrWhiteSpace(
                        block.Text))
                .ToArray();

        if (eligibleBlocks.Length == 0)
        {
            return;
        }

        var currentBlocks =
            new List<NormalizedDocumentTextBlock>();

        string? currentHeading = null;

        foreach (var block in eligibleBlocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsObviousHeading(block.Text) &&
                currentBlocks.Count > 0)
            {
                AddSegment(
                    page.PhysicalPageNumber,
                    currentHeading,
                    currentBlocks,
                    output);

                currentBlocks = [];
                currentHeading = block.Text;
            }
            else if (currentBlocks.Count == 0 &&
                     IsObviousHeading(block.Text))
            {
                currentHeading = block.Text;
            }

            currentBlocks.Add(block);
        }

        AddSegment(
            page.PhysicalPageNumber,
            currentHeading,
            currentBlocks,
            output);
    }

    private static void AddSegment(
        int physicalPageNumber,
        string? headingText,
        IReadOnlyList<NormalizedDocumentTextBlock> blocks,
        ICollection<DocumentSegment> output)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var ordinal =
            output.Count;

        var text =
            string.Join(
                "\n\n",
                blocks.Select(block =>
                    block.Text));

        output.Add(
            new DocumentSegment(
                CreateSegmentId(
                    physicalPageNumber,
                    ordinal),
                ordinal,
                physicalPageNumber,
                physicalPageNumber,
                text,
                blocks.ToArray(),
                headingText));
    }

    private static string CreateSegmentId(
        int physicalPageNumber,
        int ordinal) =>
        $"p{physicalPageNumber:D6}-s{ordinal:D6}";

    private static bool IsObviousHeading(
        string text)
    {
        var candidate =
            text.Trim();

        if (candidate.Length == 0 ||
            candidate.Length >
            MaximumHeadingLength)
        {
            return false;
        }

        var words =
            candidate.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (words.Length == 0 ||
            words.Length >
            MaximumHeadingWordCount)
        {
            return false;
        }

        var letterCount =
            candidate.Count(
                char.IsLetter);

        if (letterCount <
            MinimumHeadingLetterCount)
        {
            return false;
        }

        if (StructuralHeadingRegex()
            .IsMatch(candidate))
        {
            return true;
        }

        var hasLowercaseLetter =
            candidate.Any(
                char.IsLower);

        return !hasLowercaseLetter;
    }

    [GeneratedRegex(
        @"^(?:(?:CHAPTER|PART|SECTION|BOOK)\s+\S+|(?:\d+(?:\.\d+)*|[IVXLCDM]+)[.)]?\s+\S+)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex StructuralHeadingRegex();
}
