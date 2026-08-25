using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Footnotes;

/// <summary>
/// Removes Engine-recognized footnote source blocks from the primary structural
/// text flow while retaining every normalized element and its source evidence.
///
/// This component does not create portable footnote objects. That projection is
/// intentionally deferred to F1b.6B.
/// </summary>
internal static class FootnoteTextFlowExcluder
{
    #region Methods

    public static HybridDocumentNormalizationResult Apply(
        HybridDocumentNormalizationResult normalization,
        FootnoteTopologyAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(
            normalization);

        ArgumentNullException.ThrowIfNull(
            analysis);

        if (analysis.Entries.Count ==
            0)
        {
            return normalization;
        }

        var pages =
            normalization.Pages
                .Select(
                    page =>
                        new NormalizedHybridDocumentPage(
                            page.SourcePage,
                            page.Elements
                                .Select(
                                    element =>
                                        ExcludeIfFootnote(
                                            page.PhysicalPageNumber,
                                            element,
                                            analysis))
                                .ToArray()))
                .ToArray();

        return new HybridDocumentNormalizationResult(
            normalization.SourceAssembly,
            normalization.NormalizationProfileId,
            pages);
    }

    private static NormalizedHybridDocumentElement ExcludeIfFootnote(
        int physicalPageNumber,
        NormalizedHybridDocumentElement element,
        FootnoteTopologyAnalysis analysis)
    {
        if (element.IsExcluded ||
            !element.HasAuthoritativeText ||
            element.NativeBlock is not
                { } nativeBlock ||
            !analysis.ContainsSourceBlock(
                physicalPageNumber,
                nativeBlock.SourceSequence))
        {
            return element;
        }

        return new NormalizedHybridDocumentElement(
            element.SourceElement,
            element.Text,
            DocumentBlockExclusionReason.FootnoteContent,
            element.NormalizationDehyphenation);
    }

    #endregion
}
