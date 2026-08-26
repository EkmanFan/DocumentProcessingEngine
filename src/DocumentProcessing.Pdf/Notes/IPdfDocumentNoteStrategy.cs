using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Pdf.Notes;

/// <summary>
/// One PDF-native strategy capable of concluding note relations from source
/// representation evidence.
/// </summary>
internal interface IPdfDocumentNoteStrategy
{
    #region Methods

    IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        IReadOnlySet<PdfNativeNoteReferenceKey> claimedReferences,
        CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Coordinates independent PDF note strategies without allowing two
/// strategies to claim the same inline reference.
/// </summary>
internal sealed class PdfDocumentNoteAnalyzer
{
    #region Variables and Constants

    private readonly IReadOnlyList<IPdfDocumentNoteStrategy>
        _strategies;

    #endregion

    #region ctor

    public PdfDocumentNoteAnalyzer()
    {
        _strategies =
            [
                new PdfBottomOfPageNoteAnalyzer(),
                new PdfChapterEndNoteAnalyzer()
            ];
    }

    #endregion

    #region Methods

    public IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        cancellationToken.ThrowIfCancellationRequested();

        var claimedReferences =
            new HashSet<PdfNativeNoteReferenceKey>();

        var notes =
            new List<PagedNativeDocumentNote>();

        foreach (var strategy in
                 _strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var concluded =
                strategy.Analyze(
                    extraction,
                    claimedReferences,
                    cancellationToken);

            foreach (var note in
                     concluded)
            {
                var referenceKeys =
                    note.References
                        .Select(
                            PdfNativeNoteReferenceKey.From)
                        .ToArray();

                if (referenceKeys.Any(
                        claimedReferences.Contains))
                {
                    continue;
                }

                notes.Add(
                    note);

                claimedReferences.UnionWith(
                    referenceKeys);
            }
        }

        return notes
            .OrderBy(
                note =>
                    note.References.Min(
                        reference =>
                            reference.PhysicalPageNumber))
            .ThenBy(
                note =>
                    note.References.Min(
                        reference =>
                            reference.SourceBlockSequence))
            .ThenBy(
                note =>
                    note.References.Min(
                        reference =>
                            reference.WordSourceSequence))
            .ToArray();
    }

    #endregion
}

/// <summary>
/// Stable PDF-native identity of one inline note reference.
/// </summary>
internal readonly record struct PdfNativeNoteReferenceKey(
    int PhysicalPageNumber,
    int SourceBlockSequence,
    int WordSourceSequence)
{
    #region Methods

    public static PdfNativeNoteReferenceKey From(
        PagedNativeNoteReference reference)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        return new PdfNativeNoteReferenceKey(
            reference.PhysicalPageNumber,
            reference.SourceBlockSequence,
            reference.WordSourceSequence);
    }

    #endregion
}
