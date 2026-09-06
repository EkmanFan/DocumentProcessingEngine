using DocumentProcessing.Core.Documents;
using UglyToad.PdfPig.Content;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Reads descriptive metadata from a PDF document information dictionary.
/// </summary>
/// <remarks>
/// Acquisition only. Values are taken verbatim: a <c>/Title</c> holding a
/// converter artefact such as "Microsoft Word - final3.docx" is acquired
/// unchanged, because judging it is a later reconciliation concern and cleaning
/// it here would destroy the evidence that judgement needs.
///
/// XMP is deliberately not read in this slice. The information dictionary is the
/// field the current library exposes directly, and adding a second, sometimes
/// contradicting source before the first is characterized would create exactly
/// the conflicting-value problem this acquisition step exists to measure.
/// </remarks>
public static class PdfNativeMetadataReader
{
    #region Variables and Constants

    /// <summary>
    /// Source hint for the PDF information-dictionary title.
    /// </summary>
    public const string TitleSourceHint =
        "pdf.info.title";

    /// <summary>
    /// Source hint for the PDF information-dictionary author.
    /// </summary>
    public const string AuthorSourceHint =
        "pdf.info.author";

    /// <summary>
    /// Source hint for the PDF information-dictionary subject.
    /// </summary>
    public const string SubjectSourceHint =
        "pdf.info.subject";

    /// <summary>
    /// Source hint for the PDF information-dictionary keywords.
    /// </summary>
    public const string KeywordsSourceHint =
        "pdf.info.keywords";

    /// <summary>
    /// Source hint for the PDF information-dictionary creation date.
    /// </summary>
    public const string CreationDateSourceHint =
        "pdf.info.creationDate";

    /// <summary>
    /// Source hint for the PDF information-dictionary modification date.
    /// </summary>
    public const string ModificationDateSourceHint =
        "pdf.info.modificationDate";

    #endregion

    #region Methods

    /// <summary>
    /// Reads native metadata from a document information dictionary.
    /// </summary>
    /// <param name="information">Document information supplied by the library.</param>
    /// <returns>
    /// Acquired native metadata, or <see cref="NativeDocumentMetadata.Empty"/>
    /// when the dictionary carries no usable descriptive field.
    /// </returns>
    public static NativeDocumentMetadata Read(
        DocumentInformation? information)
    {
        if (information is null)
        {
            return NativeDocumentMetadata.Empty;
        }

        var contributors =
            new List<NativeDocumentMetadataValue>();

        var author =
            NativeDocumentMetadataValue.TryCreate(
                information.Author,
                AuthorSourceHint);

        if (author is not null)
        {
            contributors.Add(
                author);
        }

        var dates =
            new List<NativeDocumentMetadataValue>();

        // PDF dates are acquired as the raw representation strings. Parsing
        // them would be interpretation, and malformed dates are common enough
        // that a parse failure must not discard the evidence.
        var creationDate =
            NativeDocumentMetadataValue.TryCreate(
                information.CreationDate,
                CreationDateSourceHint);

        if (creationDate is not null)
        {
            dates.Add(
                creationDate);
        }

        var modificationDate =
            NativeDocumentMetadataValue.TryCreate(
                information.ModifiedDate,
                ModificationDateSourceHint);

        if (modificationDate is not null)
        {
            dates.Add(
                modificationDate);
        }

        // /Subject is the closest information-dictionary field to a
        // description; /Keywords is retained beside it rather than merged,
        // because the two are not the same statement.
        var description =
            NativeDocumentMetadataValue.TryCreate(
                information.Subject,
                SubjectSourceHint) ??
            NativeDocumentMetadataValue.TryCreate(
                information.Keywords,
                KeywordsSourceHint);

        return new NativeDocumentMetadata(
            NativeDocumentMetadataValue.TryCreate(
                information.Title,
                TitleSourceHint),
            subtitle:
                null,
            description,
            contributors,
            publisher:
                null,
            language:
                null,
            dates);
    }

    #endregion
}
