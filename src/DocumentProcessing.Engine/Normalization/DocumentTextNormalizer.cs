using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Normalization;

/// <summary>
/// Applies deterministic text normalization and recurring margin detection
/// without mutating extracted source evidence.
/// </summary>
public sealed class DocumentTextNormalizer
{
    public const string NormalizationProfileId =
        "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1";

    private const int MaximumRecurringCandidateLength = 160;

    public DocumentTextNormalizationResult Normalize(
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        cancellationToken.ThrowIfCancellationRequested();

        var provisionalPages =
            new List<NormalizedDocumentPage>(
                extraction.Pages.Count);

        foreach (var page in extraction.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blocks = page.Blocks
                .Select(block =>
                    new NormalizedDocumentTextBlock(
                        block,
                        DeterministicTextNormalizationRules
                            .Normalize(block.Text)))
                .ToArray();

            provisionalPages.Add(
                new NormalizedDocumentPage(
                    page,
                    blocks));
        }

        var recurringMarginKeys =
            FindRecurringMarginKeys(
                provisionalPages,
                cancellationToken);

        var finalPages = provisionalPages
            .Select(page =>
                ApplyMarginExclusions(
                    page,
                    recurringMarginKeys))
            .ToArray();

        return new DocumentTextNormalizationResult(
            extraction,
            NormalizationProfileId,
            finalPages);
    }

    private static HashSet<MarginKey>
        FindRecurringMarginKeys(
            IReadOnlyCollection<NormalizedDocumentPage> pages,
            CancellationToken cancellationToken)
    {
        var pagesByKey =
            new Dictionary<
                MarginKey,
                HashSet<int>>();

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var block in page.Blocks)
            {
                if (string.IsNullOrWhiteSpace(block.Text) ||
                    block.Text.Length >
                    MaximumRecurringCandidateLength)
                {
                    continue;
                }

                var zone =
                    GetMarginZone(
                        page,
                        block);

                if (zone is null)
                {
                    continue;
                }

                var recurrenceKey =
                    CanonicalizeRecurringText(
                        block.Text);

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
            .Where(pair =>
                pair.Value.Count >=
                minimumOccurrenceCount)
            .Select(pair =>
                pair.Key)
            .ToHashSet();
    }

    private static NormalizedDocumentPage
        ApplyMarginExclusions(
            NormalizedDocumentPage page,
            IReadOnlySet<MarginKey> recurringMarginKeys)
    {
        var blocks = page.Blocks
            .Select(block =>
            {
                var zone =
                    GetMarginZone(
                        page,
                        block);

                if (zone is null)
                {
                    return block;
                }

                var key =
                    new MarginKey(
                        zone.Value,
                        CanonicalizeRecurringText(
                            block.Text));

                if (!recurringMarginKeys.Contains(key))
                {
                    return block;
                }

                return new NormalizedDocumentTextBlock(
                    block.SourceBlock,
                    block.Text,
                    zone == RecurringMarginZone.Header
                        ? DocumentBlockExclusionReason.RepeatedHeader
                        : DocumentBlockExclusionReason.RepeatedFooter);
            })
            .ToArray();

        return new NormalizedDocumentPage(
            page.SourcePage,
            blocks);
    }

    private static RecurringMarginZone? GetMarginZone(
        NormalizedDocumentPage page,
        NormalizedDocumentTextBlock block) =>
        RecurringMarginGeometry.GetZone(
            block.SourceBlock.Bounds,
            page.SourcePage.ContentViewport);

    private static string
        CanonicalizeRecurringText(
        string text) =>
        DeterministicTextNormalizationRules
            .CanonicalizeRecurringText(
                text);
    private static int GetMinimumOccurrenceCount(
        int pageCount)
    {
        var proportionalCount =
            (int)Math.Ceiling(
                pageCount * 0.02);

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
