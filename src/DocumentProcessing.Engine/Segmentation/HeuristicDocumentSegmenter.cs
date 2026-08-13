using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Segmentation;

namespace DocumentProcessing.Engine.Segmentation;

/// <summary>
/// Deterministic structural segmenter.
///
/// Recognized heading-led structures may span physical pages. Content that has
/// not entered a recognized structure remains page-bounded fallback. This keeps
/// uncertain body flow bounded while allowing real intellectual sections to
/// continue naturally across pages.
/// </summary>
public sealed class HeuristicDocumentSegmenter :
    IDocumentSegmenter
{
    public const string SegmentationProfileId =
        "typography-aware-cross-page-fallback-v2";

    public DocumentSegmentationResult Segment(
        DocumentTextNormalizationResult document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var headingEvaluator =
            new HeadingEvidenceEvaluator(
                document);

        var segments =
            new List<DocumentSegment>();

        SegmentAccumulator? structured =
            null;

        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var eligibleBlocks =
                page.Blocks
                    .Where(block =>
                        !block.IsExcluded &&
                        !string.IsNullOrWhiteSpace(
                            block.Text))
                    .ToArray();

            if (eligibleBlocks.Length == 0)
            {
                continue;
            }

            var fallbackBlocks =
                new List<NormalizedDocumentTextBlock>();

            foreach (var block in eligibleBlocks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (headingEvaluator.IsHeading(
                        block))
                {
                    if (structured is not null)
                    {
                        FlushSegment(
                            segments,
                            ref structured);
                    }
                    else if (fallbackBlocks.Count > 0)
                    {
                        AddFallbackSegment(
                            page.PhysicalPageNumber,
                            fallbackBlocks,
                            segments);

                        fallbackBlocks.Clear();
                    }

                    structured =
                        new SegmentAccumulator(
                            block.Text);

                    structured.Add(
                        page.PhysicalPageNumber,
                        block);

                    continue;
                }

                if (structured is not null)
                {
                    structured.Add(
                        page.PhysicalPageNumber,
                        block);
                }
                else
                {
                    fallbackBlocks.Add(
                        block);
                }
            }

            if (structured is null &&
                fallbackBlocks.Count > 0)
            {
                AddFallbackSegment(
                    page.PhysicalPageNumber,
                    fallbackBlocks,
                    segments);
            }
        }

        FlushSegment(
            segments,
            ref structured);

        return new DocumentSegmentationResult(
            document,
            SegmentationProfileId,
            segments);
    }

    private static void AddFallbackSegment(
        int physicalPageNumber,
        IReadOnlyList<NormalizedDocumentTextBlock> blocks,
        ICollection<DocumentSegment> output)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        AddSegment(
            physicalPageNumber,
            physicalPageNumber,
            headingText: null,
            blocks,
            output);
    }

    private static void FlushSegment(
        ICollection<DocumentSegment> output,
        ref SegmentAccumulator? current)
    {
        if (current is null ||
            current.Blocks.Count == 0)
        {
            current = null;
            return;
        }

        AddSegment(
            current.FirstPhysicalPageNumber,
            current.LastPhysicalPageNumber,
            current.HeadingText,
            current.Blocks,
            output);

        current = null;
    }

    private static void AddSegment(
        int firstPhysicalPageNumber,
        int lastPhysicalPageNumber,
        string? headingText,
        IReadOnlyList<NormalizedDocumentTextBlock> blocks,
        ICollection<DocumentSegment> output)
    {
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
                    firstPhysicalPageNumber,
                    ordinal),
                ordinal,
                firstPhysicalPageNumber,
                lastPhysicalPageNumber,
                text,
                blocks.ToArray(),
                headingText));
    }

    private static string CreateSegmentId(
        int firstPhysicalPageNumber,
        int ordinal) =>
        $"p{firstPhysicalPageNumber:D6}-s{ordinal:D6}";

    private sealed class SegmentAccumulator(
        string headingText)
    {
        public string HeadingText { get; } =
            headingText;

        public int FirstPhysicalPageNumber { get; private set; }

        public int LastPhysicalPageNumber { get; private set; }

        public List<NormalizedDocumentTextBlock> Blocks { get; } =
            [];

        public void Add(
            int physicalPageNumber,
            NormalizedDocumentTextBlock block)
        {
            ArgumentNullException.ThrowIfNull(block);

            if (physicalPageNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalPageNumber));
            }

            if (Blocks.Count == 0)
            {
                FirstPhysicalPageNumber =
                    physicalPageNumber;
            }

            LastPhysicalPageNumber =
                physicalPageNumber;

            Blocks.Add(block);
        }
    }
}
