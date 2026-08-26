using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Native document evidence whose authoritative source representation is
/// physically paged.
/// </summary>
/// <remarks>
/// Paged extraction and coordinated raster-observation evidence are kept
/// together because their invariants are page-correlated. This contract is not
/// tied to a concrete format such as PDF.
/// </remarks>
public sealed record PagedNativeDocumentEvidence
    : NativeDocumentEvidence
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

    public override ProcessingComponentIdentity?
        NativeExtractionIdentity { get; }

    #endregion

    #region ctor

    public PagedNativeDocumentEvidence(
        DocumentExtractionWithRasterObservationsResult currentEvidence)
        : this(
            currentEvidence,
            nativeExtractionIdentity:
                null)
    {
    }

    public PagedNativeDocumentEvidence(
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
