using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Hybrid.Normalization;
using DocumentProcessing.Core.Normalization;

namespace DocumentProcessing.Engine.Footnotes;

/// <summary>
/// Removes format-concluded note payload blocks from the primary structural
/// text flow while retaining every normalized element and its source evidence.
/// This component does not create portable footnote objects.
/// </summary>
internal static class FootnoteTextFlowExcluder
{
    #region Methods

    public static HybridDocumentNormalizationResult Apply(
        HybridDocumentNormalizationResult normalization,
        IReadOnlyList<NativeDocumentNote> notes)
    {
        ArgumentNullException.ThrowIfNull(
            normalization);

        ArgumentNullException.ThrowIfNull(
            notes);

        if (notes.Count ==
            0)
        {
            return normalization;
        }

        var noteSourceBlocks =
            notes
                .OfType<PagedNativeDocumentNote>()
                .SelectMany(
                    note =>
                        note.SourceBlocks)
                .ToHashSet();

        if (noteSourceBlocks.Count ==
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
                                            noteSourceBlocks))
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
        IReadOnlySet<PagedNativeNoteSourceBlock> noteSourceBlocks)
    {
        if (element.IsExcluded ||
            !element.HasAuthoritativeText ||
            element.NativeBlock is not
                { } nativeBlock ||
            !noteSourceBlocks.Contains(
                new PagedNativeNoteSourceBlock(
                    physicalPageNumber,
                    nativeBlock.SourceSequence)))
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
