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

    private const double HeaderZoneFraction = 0.12;
    private const double FooterZoneFraction = 0.12;
    private const double MaximumMarginBlockHeightFraction = 0.20;
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
                    GetMarginZone(block);

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
                    GetMarginZone(block);

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
                    zone == MarginZone.Header
                        ? DocumentBlockExclusionReason.RepeatedHeader
                        : DocumentBlockExclusionReason.RepeatedFooter);
            })
            .ToArray();

        return new NormalizedDocumentPage(
            page.SourcePage,
            blocks);
    }

    private static MarginZone? GetMarginZone(
        NormalizedDocumentTextBlock block)
    {
        var bounds =
            block.SourceBlock.Bounds;

        var height =
            bounds.Bottom -
            bounds.Top;

        if (height <= 0 ||
            height >
            MaximumMarginBlockHeightFraction)
        {
            return null;
        }

        // Core geometry has a normalized top-left origin.
        // Historical PdfPig geometry used a bottom-left origin.
        if (bounds.Top <=
            HeaderZoneFraction)
        {
            return MarginZone.Header;
        }

        if (bounds.Bottom >=
            1.0 -
            FooterZoneFraction)
        {
            return MarginZone.Footer;
        }

        return null;
    }

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

    private enum MarginZone
    {
        Header = 0,
        Footer = 1
    }

    private readonly record struct MarginKey(
        MarginZone Zone,
        string Text);
}
