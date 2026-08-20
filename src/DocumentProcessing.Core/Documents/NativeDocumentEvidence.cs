using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Transitional neutral boundary around the current native document evidence
/// model.
/// </summary>
/// <remarks>
/// The container remains passive. Native-extraction identity is factual
/// provenance for the acquisition that produced this evidence; it is not an
/// Engine assessment or treatment decision.
/// </remarks>
public sealed class NativeDocumentEvidence
{
    #region Variables and Constants

    private readonly DocumentExtractionWithRasterObservationsResult
        _currentEvidence;

    #endregion

    #region Properties

    public DocumentExtractionResult Extraction =>
        _currentEvidence.Extraction;

    public IReadOnlyList<PageVisualRasterObservations>?
        RasterObservations =>
        _currentEvidence.RasterObservations;

    public RasterObservationAcquisitionFailure?
        RasterObservationFailure =>
        _currentEvidence.RasterObservationFailure;

    /// <summary>
    /// Stable factual identity of the native-evidence acquisition component when
    /// the producer supplies it.
    /// </summary>
    public ProcessingComponentIdentity?
        NativeExtractionIdentity { get; }

    #endregion

    #region ctor

    public NativeDocumentEvidence(
        DocumentExtractionWithRasterObservationsResult currentEvidence)
        : this(
            currentEvidence,
            nativeExtractionIdentity:
                null)
    {
    }

    public NativeDocumentEvidence(
        DocumentExtractionWithRasterObservationsResult currentEvidence,
        ProcessingComponentIdentity? nativeExtractionIdentity)
    {
        _currentEvidence =
            currentEvidence ??
            throw new ArgumentNullException(
                nameof(currentEvidence));

        NativeExtractionIdentity =
            nativeExtractionIdentity;
    }

    #endregion
}
