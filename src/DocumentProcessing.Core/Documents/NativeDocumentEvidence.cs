using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Transitional neutral boundary around the current native PDF evidence model.
///
/// This type deliberately reuses the already-validated coordinated extraction
/// result and its invariants. It does not redesign, reinterpret, or duplicate
/// the underlying evidence during the architecture migration.
/// </summary>
public sealed class NativeDocumentEvidence
{
    #region Variables and Constants

    private readonly DocumentExtractionWithRasterObservationsResult
        _currentEvidence;

    #endregion

    #region ctor

    public NativeDocumentEvidence(
        DocumentExtractionWithRasterObservationsResult currentEvidence)
    {
        _currentEvidence =
            currentEvidence ??
            throw new ArgumentNullException(
                nameof(currentEvidence));
    }

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

    #endregion
}
