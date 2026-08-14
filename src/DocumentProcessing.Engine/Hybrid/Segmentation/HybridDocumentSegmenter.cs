using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Hybrid.Segmentation;
using DocumentProcessing.Core.Segmentation;
using DocumentProcessing.Engine.Segmentation;

namespace DocumentProcessing.Engine.Hybrid.Segmentation;

/// <summary>
/// Deterministic structural segmentation over the already unified and
/// normalized hybrid stream.
///
/// Heading-led structures may span physical pages and native/OCR origin
/// transitions. Unstructured fallback remains page-bounded, matching the
/// conservative legacy behavior.
/// </summary>
public sealed class HybridDocumentSegmenter
{
    public const string SegmentationProfileId =
        "hybrid-layout-heading-strict-native-typography-optional-hints-cross-page-fallback-v1";

    public HybridDocumentSegmentationResult Segment(
        HybridDocumentNormalizationResult document,
        CancellationToken cancellationToken = default) =>
        Segment(
            document,
            DocumentSegmentationOptions.Default,
            cancellationToken);

    public HybridDocumentSegmentationResult Segment(
        HybridDocumentNormalizationResult document,
        DocumentSegmentationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        var nativeHeadingRules =
            new NativeHeadingEvidenceRules(
                GetLayoutlessNativeBlocks(
                    document));

        var headingHintMatcher =
            new HeadingHintMatcher(
                options.HeadingHints);

        var segments =
            new List<HybridDocumentSegment>();

        SegmentAccumulator? structured =
            null;

        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fallback =
                new List<NormalizedHybridDocumentElement>();

            var fallbackHasText =
                false;

            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (element.IsExcluded)
                {
                    continue;
                }

                if (!element.HasAuthoritativeText)
                {
                    if (structured is not null)
                    {
                        structured.Add(
                            element);
                    }
                    else
                    {
                        fallback.Add(
                            element);
                    }

                    continue;
                }

                if (IsHeading(
                        element,
                        nativeHeadingRules,
                        headingHintMatcher))
                {
                    if (structured is not null)
                    {
                        FlushStructuredSegment(
                            segments,
                            ref structured);
                    }
                    else if (fallbackHasText)
                    {
                        AddFallbackSegment(
                            fallback,
                            segments);

                        fallback.Clear();
                        fallbackHasText =
                            false;
                    }
                    else
                    {
                        // Non-text evidence preceding the first recognized
                        // heading is not assigned to the heading-led section.
                        fallback.Clear();
                    }

                    structured =
                        new SegmentAccumulator(
                            element.Text!);

                    structured.Add(
                        element);

                    continue;
                }

                if (structured is not null)
                {
                    structured.Add(
                        element);
                }
                else
                {
                    fallback.Add(
                        element);

                    fallbackHasText =
                        true;
                }
            }

            if (structured is null &&
                fallbackHasText)
            {
                AddFallbackSegment(
                    fallback,
                    segments);
            }
        }

        FlushStructuredSegment(
            segments,
            ref structured);

        return new HybridDocumentSegmentationResult(
            document,
            SegmentationProfileId,
            segments);
    }

    private static IReadOnlyCollection<DocumentTextBlock>
        GetLayoutlessNativeBlocks(
        HybridDocumentNormalizationResult document)
    {
        var blocks =
            new List<DocumentTextBlock>();

        var seen =
            new HashSet<DocumentTextBlock>(
                ReferenceEqualityComparer.Instance);

        foreach (var element in
                 document.Pages.SelectMany(
                     page =>
                         page.Elements))
        {
            if (!element.IsTextFlowElement ||
                element.LayoutObservation is not null ||
                element.NativeBlock is null)
            {
                continue;
            }

            if (seen.Add(
                    element.NativeBlock))
            {
                blocks.Add(
                    element.NativeBlock);
            }
        }

        return blocks;
    }

    private static bool IsHeading(
        NormalizedHybridDocumentElement element,
        NativeHeadingEvidenceRules nativeHeadingRules,
        HeadingHintMatcher headingHintMatcher)
    {
        if (element.Kind ==
            HybridDocumentElementKind.Heading)
        {
            return true;
        }

        if (element.Kind !=
            HybridDocumentElementKind.Text)
        {
            // Caption is authoritative textual evidence, but it is not a
            // structural heading candidate.
            return false;
        }

        if (headingHintMatcher.IsHeading(
                element.Text!,
                element.SourceText ??
                element.Text!))
        {
            return true;
        }

        // An explicit neutral layout Text classification is not silently
        // overridden by native typography. Strict typography is a fallback for
        // born-digital/layout-less native elements only.
        if (element.LayoutObservation is not null ||
            element.NativeBlock is null)
        {
            return false;
        }

        return nativeHeadingRules.IsHeading(
            element.NativeBlock,
            element.Text!);
    }

    private static void AddFallbackSegment(
        IReadOnlyList<NormalizedHybridDocumentElement> sourceElements,
        ICollection<HybridDocumentSegment> output)
    {
        if (!sourceElements.Any(
                element =>
                    element.IsTextFlowElement))
        {
            return;
        }

        AddSegment(
            headingText: null,
            sourceElements,
            output);
    }

    private static void FlushStructuredSegment(
        ICollection<HybridDocumentSegment> output,
        ref SegmentAccumulator? current)
    {
        if (current is null ||
            !current.Elements.Any(
                element =>
                    element.IsTextFlowElement))
        {
            current =
                null;

            return;
        }

        AddSegment(
            current.HeadingText,
            current.Elements,
            output);

        current =
            null;
    }

    private static void AddSegment(
        string? headingText,
        IReadOnlyList<NormalizedHybridDocumentElement> sourceElements,
        ICollection<HybridDocumentSegment> output)
    {
        var ordinal =
            output.Count;

        var firstPhysicalPageNumber =
            sourceElements[0]
                .PhysicalPageNumber;

        output.Add(
            new HybridDocumentSegment(
                CreateSegmentId(
                    firstPhysicalPageNumber,
                    ordinal),
                ordinal,
                sourceElements.ToArray(),
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

        public List<NormalizedHybridDocumentElement> Elements { get; } =
            [];

        public void Add(
            NormalizedHybridDocumentElement element)
        {
            ArgumentNullException.ThrowIfNull(
                element);

            Elements.Add(
                element);
        }
    }
}
