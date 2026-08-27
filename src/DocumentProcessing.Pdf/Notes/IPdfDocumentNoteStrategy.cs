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

    private readonly PdfBottomOfPageNoteAnalyzer
        _bottomOfPageStrategy;

    private readonly PdfLinkedNumericNoteAnalyzer
        _linkedNumericStrategy;

    private readonly PdfChapterEndNoteAnalyzer
        _chapterEndStrategy;

    #endregion

    #region ctor

    public PdfDocumentNoteAnalyzer()
    {
        _bottomOfPageStrategy =
            new PdfBottomOfPageNoteAnalyzer();

        _linkedNumericStrategy =
            new PdfLinkedNumericNoteAnalyzer();

        _chapterEndStrategy =
            new PdfChapterEndNoteAnalyzer();
    }

    #endregion

    #region Methods

    public IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default) =>
        Analyze(
            extraction,
            [],
            cancellationToken);

    public IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        IReadOnlyList<PdfNativeNumericLinkObservation> nativeNumericLinks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            nativeNumericLinks);

        cancellationToken.ThrowIfCancellationRequested();

        var claimedReferences =
            new HashSet<PdfNativeNoteReferenceKey>();

        var notes =
            new List<PagedNativeDocumentNote>();

        AddConcluded(
            _bottomOfPageStrategy.Analyze(
                extraction,
                claimedReferences,
                cancellationToken),
            notes,
            claimedReferences);

        cancellationToken.ThrowIfCancellationRequested();

        AddConcluded(
            _linkedNumericStrategy.Analyze(
                extraction,
                nativeNumericLinks,
                claimedReferences,
                cancellationToken),
            notes,
            claimedReferences);

        cancellationToken.ThrowIfCancellationRequested();

        AddConcluded(
            _chapterEndStrategy.Analyze(
                extraction,
                claimedReferences,
                cancellationToken),
            notes,
            claimedReferences);

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

    private static void AddConcluded(
        IReadOnlyList<PagedNativeDocumentNote> concluded,
        ICollection<PagedNativeDocumentNote> notes,
        ISet<PdfNativeNoteReferenceKey> claimedReferences)
    {
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
