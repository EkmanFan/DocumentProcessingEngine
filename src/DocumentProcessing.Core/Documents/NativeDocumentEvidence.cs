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
    #region Properties

    /// <summary>
    /// Gets the stable factual identity of the native-evidence acquisition
    /// component when that representation supplies one.
    /// </summary>
    public abstract ProcessingComponentIdentity?
        NativeExtractionIdentity { get; }

    #endregion
}
