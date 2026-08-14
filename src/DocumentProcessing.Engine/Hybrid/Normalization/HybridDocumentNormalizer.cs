using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.Engine.Hybrid.Normalization;

/// <summary>
/// Deterministically normalizes the already-unified hybrid stream.
///
/// Authority selection, OCR, layout analysis, reconciliation, visual
/// preservation and structural segmentation are deliberately outside this
/// component.
/// </summary>
public sealed class HybridDocumentNormalizer
{
    public const string NormalizationProfileId =
        "hybrid-unicode-nfc-whitespace-source-dehyphenation-recurring-margins-v1";


    private const int MaximumRecurringCandidateLength =
        160;

    public HybridDocumentNormalizationResult Normalize(
        HybridDocumentAssemblyResult assembly,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            assembly);

        cancellationToken.ThrowIfCancellationRequested();

        var provisionalPages =
            new List<NormalizedHybridDocumentPage>(
                assembly.Pages.Count);

        foreach (var page in assembly.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elements =
                page.Elements
                    .Select(
                        NormalizeElement)
                    .ToArray();

            provisionalPages.Add(
                new NormalizedHybridDocumentPage(
                    page,
                    elements));
        }

        var recurringMarginKeys =
            FindRecurringMarginKeys(
                provisionalPages,
                cancellationToken);

        var finalPages =
            provisionalPages
                .Select(
                    page =>
                        ApplyMarginExclusions(
                            page,
                            recurringMarginKeys))
                .ToArray();

        return new HybridDocumentNormalizationResult(
            assembly,
            NormalizationProfileId,
            finalPages);
    }

    private static NormalizedHybridDocumentElement NormalizeElement(
        HybridDocumentElement sourceElement)
    {
        if (!sourceElement.HasAuthoritativeText)
        {
            return new NormalizedHybridDocumentElement(
                sourceElement,
                text: null);
        }

        var normalizationSourceText =
            sourceElement.Text!;

        TextDehyphenationResult? normalizationDehyphenation =
            null;

        if (sourceElement.TextOrigin ==
                TextSelectionOrigin.Ocr &&
            sourceElement.Reconciliation
                ?.Input.OcrRegion is { } ocrRegion &&
            sourceElement.Reconciliation
                .OcrTextPreparation is null)
        {
            var candidate =
                ReconciliationTextDehyphenator
                    .DehyphenateOcr(
                        ocrRegion);

            if (candidate.Changed)
            {
                normalizationSourceText =
                    candidate.Text;

                normalizationDehyphenation =
                    candidate;
            }
        }

        var normalizedText =
            DeterministicTextNormalizationRules
                .Normalize(
                    normalizationSourceText);

        return new NormalizedHybridDocumentElement(
            sourceElement,
            normalizedText,
            exclusionReason: null,
            normalizationDehyphenation);
    }

    private static HashSet<MarginKey> FindRecurringMarginKeys(
        IReadOnlyCollection<NormalizedHybridDocumentPage> pages,
        CancellationToken cancellationToken)
    {
        var pagesByKey =
            new Dictionary<
                MarginKey,
                HashSet<int>>();

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var element in page.Elements)
            {
                if (!element.HasAuthoritativeText ||
                    string.IsNullOrWhiteSpace(
                        element.Text) ||
                    element.Text.Length >
                    MaximumRecurringCandidateLength)
                {
                    continue;
                }

                var zone =
                    GetMarginZone(
                        page,
                        element);

                if (zone is null)
                {
                    continue;
                }

                var recurrenceKey =
                    DeterministicTextNormalizationRules
                        .CanonicalizeRecurringText(
                            element.Text);

                if (recurrenceKey.Length == 0)
                {
                    continue;
                }

                var key =
                    new MarginKey(
                        zone.Value,
                        recurrenceKey);

                if (!pagesByKey.TryGetValue(
                        key,
                        out var pageNumbers))
                {
                    pageNumbers = [];

                    pagesByKey.Add(
                        key,
                        pageNumbers);
                }

                pageNumbers.Add(
                    page.PhysicalPageNumber);
            }
        }

        var minimumOccurrenceCount =
            GetMinimumOccurrenceCount(
                pages.Count);

        return pagesByKey
            .Where(
                pair =>
                    pair.Value.Count >=
                    minimumOccurrenceCount)
            .Select(
                pair =>
                    pair.Key)
            .ToHashSet();
    }

    private static NormalizedHybridDocumentPage ApplyMarginExclusions(
        NormalizedHybridDocumentPage page,
        IReadOnlySet<MarginKey> recurringMarginKeys)
    {
        var elements =
            page.Elements
                .Select(
                    element =>
                    {
                        if (!element.HasAuthoritativeText)
                        {
                            return element;
                        }

                        var zone =
                            GetMarginZone(
                                page,
                                element);

                        if (zone is null)
                        {
                            return element;
                        }

                        var key =
                            new MarginKey(
                                zone.Value,
                                DeterministicTextNormalizationRules
                                    .CanonicalizeRecurringText(
                                        element.Text!));

                        if (!recurringMarginKeys.Contains(
                                key))
                        {
                            return element;
                        }

                        return new NormalizedHybridDocumentElement(
                            element.SourceElement,
                            element.Text,
                            zone ==
                                RecurringMarginZone.Header
                                ? DocumentBlockExclusionReason.RepeatedHeader
                                : DocumentBlockExclusionReason.RepeatedFooter,
                            element.NormalizationDehyphenation);
                    })
                .ToArray();

        return new NormalizedHybridDocumentPage(
            page.SourcePage,
            elements);
    }

    private static RecurringMarginZone? GetMarginZone(
        NormalizedHybridDocumentPage page,
        NormalizedHybridDocumentElement element) =>
        RecurringMarginGeometry.GetZone(
            element.Bounds,
            page.SourcePage.ContentViewport);

    private static int GetMinimumOccurrenceCount(
        int pageCount)
    {
        var proportionalCount =
            (int)Math.Ceiling(
                pageCount *
                0.02);

        return Math.Max(
            3,
            Math.Min(
                10,
                proportionalCount));
    }

    private readonly record struct MarginKey(
        RecurringMarginZone Zone,
        string Text);
}
