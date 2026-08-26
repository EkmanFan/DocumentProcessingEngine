using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Common neutral contract for native document evidence produced by a document
/// format adapter.
/// </summary>
/// <remarks>
/// Only facts with the same meaning across native representations belong on this
/// contract. Representation-specific evidence remains on derived types, while
/// Engine assessment and treatment decisions remain outside the DTO.
/// </remarks>
public abstract record NativeDocumentEvidence
{
    #region Variables and Constants

    private readonly IReadOnlyList<NativeDocumentNote>
        _documentNotes;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the stable factual identity of the native-evidence acquisition
    /// component when that representation supplies one.
    /// </summary>
    public abstract ProcessingComponentIdentity?
        NativeExtractionIdentity { get; }

    /// <summary>
    /// Gets note relations conclusively established by the format adapter from
    /// its native representation.
    /// </summary>
    public IReadOnlyList<NativeDocumentNote> DocumentNotes =>
        _documentNotes;

    #endregion

    #region ctor

    private protected NativeDocumentEvidence(
        IReadOnlyList<NativeDocumentNote> documentNotes)
    {
        ArgumentNullException.ThrowIfNull(
            documentNotes);

        var notes =
            documentNotes.ToArray();

        if (notes.Any(
                note =>
                    note is null))
        {
            throw new ArgumentException(
                "Native document evidence cannot contain null notes.",
                nameof(documentNotes));
        }

        _documentNotes =
            Array.AsReadOnly(
                notes);
    }

    #endregion
}
