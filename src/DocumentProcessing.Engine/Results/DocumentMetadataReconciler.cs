using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Concludes neutral document metadata from acquired native evidence.
/// </summary>
/// <remarks>
/// The format layer reads what a representation states; this decides what the
/// document's metadata is. V1 is deliberately the smallest policy the evidence
/// supports.
///
/// Characterization over the qualified corpus found a native title on 15 of 17
/// sources — every publisher-produced one — with no converter artefact of the
/// "Microsoft Word - final3.docx" kind, and with PDF and EPUB agreeing on the
/// title for all four works present in both formats. On that evidence a native
/// title is taken as concluded, unfiltered: a quality heuristic would be
/// rejecting values that this corpus shows are good.
///
/// Nothing is inferred from document structure. Where no native value exists the
/// field stays absent rather than guessed, and the filename is never promoted:
/// a filename that reads like a title is still a filename, and a consumer must
/// be able to tell the difference.
/// </remarks>
internal static class DocumentMetadataReconciler
{
    #region Methods

    /// <summary>
    /// Concludes portable metadata from native acquisition evidence.
    /// </summary>
    /// <param name="native">Acquired native metadata.</param>
    /// <returns>
    /// Concluded metadata, or <see cref="DocumentMetadata.Empty"/> when the
    /// source stated nothing.
    /// </returns>
    public static DocumentMetadata Reconcile(
        NativeDocumentMetadata? native)
    {
        if (native is null ||
            native.IsEmpty)
        {
            return DocumentMetadata.Empty;
        }

        return new DocumentMetadata(
            Value(
                native.Title),
            // No format supported today states a subtitle in a way that can be
            // read without interpretation, so the field stays absent.
            subtitle:
                null,
            Value(
                native.Description),
            native.Contributors
                .Select(
                    contributor =>
                        new DocumentMetadataContributor(
                            contributor.Value,
                            DocumentMetadataOrigin.Native,
                            contributor.SourceHint))
                .ToArray(),
            Value(
                native.Publisher),
            Value(
                native.Language),
            native.Dates
                .Select(
                    date =>
                        new DocumentMetadataDate(
                            date.Value,
                            KindOf(
                                date.SourceHint),
                            DocumentMetadataOrigin.Native,
                            date.SourceHint))
                .ToArray());
    }

    #endregion

    #region Methods Conclusion

    private static DocumentMetadataValue? Value(
        NativeDocumentMetadataValue? native) =>
        native is null
            ? null
            : new DocumentMetadataValue(
                native.Value,
                DocumentMetadataOrigin.Native,
                native.SourceHint);

    /// <summary>
    /// Reads the kind a date's own source field states.
    /// </summary>
    /// <remarks>
    /// Only what the field name asserts is used. A PDF information dictionary
    /// distinguishes creation from modification; an EPUB <c>dc:date</c> does
    /// not say which event it denotes, so it stays
    /// <see cref="DocumentMetadataDateKind.Unspecified"/> rather than being
    /// assumed to be publication.
    /// </remarks>
    private static DocumentMetadataDateKind KindOf(
        string? sourceHint) =>
        sourceHint switch
        {
            "pdf.info.creationDate" =>
                DocumentMetadataDateKind.Created,
            "pdf.info.modificationDate" =>
                DocumentMetadataDateKind.Modified,
            _ =>
                DocumentMetadataDateKind.Unspecified
        };

    #endregion
}
