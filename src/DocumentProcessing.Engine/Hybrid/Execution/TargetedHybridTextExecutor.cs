using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Shared deterministic targeted-text execution primitive for hybrid pages.
///
/// It owns OCR target planning, target-centric native/layout pairing, targeted
/// region rendering/recognition, and native/OCR reconciliation.
///
/// It deliberately does not own full-page rasterization, layout execution,
/// visual preservation, page-level routing, or final page assembly.
/// </summary>
internal sealed class TargetedHybridTextExecutor
{
    #region Variables and Constants

    private readonly IRegionTextRecognizer _textRecognizer;

    #endregion


    #region ctor

    public TargetedHybridTextExecutor(
        IRegionTextRecognizer textRecognizer)
    {
        _textRecognizer =
            textRecognizer ??
            throw new ArgumentNullException(
                nameof(textRecognizer));
    }

    #endregion


    #region Methods

    public IReadOnlyDictionary<int, TargetedOcrRegion> CreateOcrTargets(
        LayoutAnalysisResult layout,
        RasterRenderResult pageRaster)
    {
        ArgumentNullException.ThrowIfNull(
            layout);

        ArgumentNullException.ThrowIfNull(
            pageRaster);

        return TargetedOcrPlanner
            .Create(
                layout,
                pageRaster.OutputPixelWidth,
                pageRaster.OutputPixelHeight)
            .ToDictionary(
                target =>
                    target
                        .SourceLayoutObservation
                        .ObservationSequence);
    }

    public IReadOnlyDictionary<int, NativeLayoutTextPairing>
        CreateNativePresentPairings(
            DocumentExtractionPage sourcePage,
            LayoutAnalysisResult layout)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        ArgumentNullException.ThrowIfNull(
            layout);

        var textLayoutObservations =
            layout.Observations
                .Where(
                    observation =>
                        LayoutTextPolicy.IsTextRecognitionCandidate(
                            observation.Kind))
                .ToArray();

        var pairings =
            NativeLayoutTextPairer
                .Pair(
                    sourcePage.Blocks,
                    textLayoutObservations)
                .ToDictionary(
                    pairing =>
                        pairing
                            .TargetLayoutObservation
                            .ObservationSequence);

        var ambiguousPairing =
            pairings.Values
                .FirstOrDefault(
                    pairing =>
                        pairing.Status ==
                        NativeLayoutTextPairingStatus
                            .AmbiguousWordOwnership);

        if (ambiguousPairing is not null)
        {
            throw new InvalidDataException(
                $"Native/layout pairing for physical page " +
                $"{sourcePage.PhysicalPageNumber}, layout observation " +
                $"{ambiguousPairing.TargetLayoutObservation.ObservationSequence} " +
                "has ambiguous native word ownership. Hybrid reconciliation " +
                "fails closed before OCR authority selection.");
        }

        return pairings;
    }

    public async ValueTask<HybridDocumentElement> ExecuteMissingAsync(
        DocumentExtractionPage sourcePage,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutObservation observation,
        IReadOnlyDictionary<int, TargetedOcrRegion> ocrTargets,
        CancellationToken cancellationToken = default)
    {
        var ocr =
            await RecognizeAsync(
                    sourcePage,
                    rasterSession,
                    pageRaster,
                    observation,
                    ocrTargets,
                    cancellationToken)
                .ConfigureAwait(false);

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        sourcePage.PhysicalPageNumber,
                        NativeTextStatus.Missing,
                        nativeBlock:
                            null,
                        ocr));

        return HybridDocumentElementFactory
            .FromReconciliation(
                reconciliation);
    }

    public async ValueTask<HybridDocumentElement> ExecuteNativePresentAsync(
        DocumentExtractionPage sourcePage,
        NativeTextStatus pageNativeStatus,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutObservation observation,
        NativeLayoutTextPairing pairing,
        IReadOnlyDictionary<int, TargetedOcrRegion> ocrTargets,
        CancellationToken cancellationToken = default)
    {
        if (pairing.Status ==
            NativeLayoutTextPairingStatus
                .AmbiguousWordOwnership)
        {
            throw new InvalidDataException(
                "Ambiguous native/layout pairing cannot enter OCR reconciliation.");
        }

        if (!ReferenceEquals(
                pairing.TargetLayoutObservation,
                observation))
        {
            throw new InvalidDataException(
                "Native/layout pairing must retain the exact source layout observation.");
        }

        var ocr =
            await RecognizeAsync(
                    sourcePage,
                    rasterSession,
                    pageRaster,
                    observation,
                    ocrTargets,
                    cancellationToken)
                .ConfigureAwait(false);

        TextReconciliationResult reconciliation;

        switch (pairing.Status)
        {
            case NativeLayoutTextPairingStatus.Comparable:
                var nativeEvidence =
                    pairing.ComparableNativeEvidence ??
                    throw new InvalidDataException(
                        "Comparable native/layout pairing has no comparable native evidence.");

                reconciliation =
                    NativeOcrTextReconciler
                        .ReconcileComparable(
                            new TextReconciliationInput(
                                sourcePage.PhysicalPageNumber,
                                pageNativeStatus,
                                nativeEvidence,
                                ocr),
                            nativeEvidence);

                break;

            case NativeLayoutTextPairingStatus.NoNativeEvidence:
                reconciliation =
                    NativeOcrTextReconciler
                        .Reconcile(
                            new TextReconciliationInput(
                                sourcePage.PhysicalPageNumber,
                                NativeTextStatus.Missing,
                                nativeBlock:
                                    null,
                                ocr));

                break;

            case NativeLayoutTextPairingStatus.AmbiguousWordOwnership:
                throw new InvalidDataException(
                    "Ambiguous native/layout pairing cannot enter OCR reconciliation.");

            default:
                throw new InvalidOperationException(
                    $"Unsupported native/layout pairing status {pairing.Status}.");
        }

        return HybridDocumentElementFactory
            .FromReconciliation(
                reconciliation);
    }

    private async ValueTask<OcrRegionResult> RecognizeAsync(
        DocumentExtractionPage sourcePage,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutObservation observation,
        IReadOnlyDictionary<int, TargetedOcrRegion> ocrTargets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        ArgumentNullException.ThrowIfNull(
            rasterSession);

        ArgumentNullException.ThrowIfNull(
            pageRaster);

        ArgumentNullException.ThrowIfNull(
            observation);

        ArgumentNullException.ThrowIfNull(
            ocrTargets);

        if (!ocrTargets.TryGetValue(
                observation.ObservationSequence,
                out var target))
        {
            throw new InvalidDataException(
                $"OCR-authorized layout observation " +
                $"{observation.ObservationSequence} has no targeted OCR plan.");
        }

        await using var cropBytes =
            new MemoryStream();

        var cropRaster =
            await rasterSession
                .RenderRegionAsync(
                    sourcePage.PhysicalPageNumber,
                    pageRaster.OutputPixelWidth,
                    pageRaster.OutputPixelHeight,
                    target.Crop,
                    cropBytes,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateCropRaster(
            sourcePage,
            pageRaster,
            target.Crop,
            cropRaster);

        Rewind(
            cropBytes);

        var ocr =
            await _textRecognizer
                .RecognizeAsync(
                    cropBytes,
                    observation,
                    target.Crop,
                    pageRaster.OutputPixelWidth,
                    pageRaster.OutputPixelHeight,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!ReferenceEquals(
                ocr.SourceLayoutObservation,
                observation))
        {
            throw new InvalidDataException(
                "OCR result must retain the exact source layout observation.");
        }

        return ocr;
    }

    private static void ValidateCropRaster(
        DocumentExtractionPage sourcePage,
        RasterRenderResult pageRaster,
        PixelRectangle expectedCrop,
        RasterRenderResult cropRaster)
    {
        if (cropRaster.PhysicalPageNumber !=
            sourcePage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Region raster belongs to a different physical page.");
        }

        if (cropRaster.Crop !=
            expectedCrop)
        {
            throw new InvalidDataException(
                "Region raster does not match the deterministic planned crop.");
        }

        if (cropRaster.SourcePagePixelWidth !=
                pageRaster.OutputPixelWidth ||
            cropRaster.SourcePagePixelHeight !=
                pageRaster.OutputPixelHeight)
        {
            throw new InvalidDataException(
                "Region raster source dimensions do not match the page raster.");
        }
    }

    private static void Rewind(
        Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException(
                "Internal hybrid execution buffer must be seekable.");
        }

        stream.Position =
            0;
    }

    #endregion
}
