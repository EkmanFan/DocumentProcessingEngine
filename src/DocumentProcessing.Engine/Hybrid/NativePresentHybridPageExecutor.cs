using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Executes the V1 hybrid reconciliation route when native PDF text exists but
/// deterministic preflight evidence does not permit trusting it directly.
///
/// Route:
/// page raster -> layout -> deterministic region policy -> target-centric
/// native/layout pairing -> targeted OCR -> native/OCR reconciliation -> hybrid
/// page.
///
/// No fuzzy matcher, OCR-confidence authority threshold, max-overlap winner, or
/// LLM arbitration is introduced here.
/// </summary>
public sealed class NativePresentHybridPageExecutor
{
    private readonly IPageLayoutAnalyzer _layoutAnalyzer;
    private readonly TargetedHybridTextExecutor _textExecutor;
    private readonly VisualAssetPreserver _visualAssetPreserver;

    #region Construction

    public NativePresentHybridPageExecutor(
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer,
        VisualAssetPreserver visualAssetPreserver)
    {
        _layoutAnalyzer =
            layoutAnalyzer ??
            throw new ArgumentNullException(
                nameof(layoutAnalyzer));

        _textExecutor =
            new TargetedHybridTextExecutor(
                textRecognizer);

        _visualAssetPreserver =
            visualAssetPreserver ??
            throw new ArgumentNullException(
                nameof(visualAssetPreserver));
    }

    #endregion

    #region Public execution

    public async ValueTask<HybridDocumentPage> ExecuteAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision decision,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            sourcePage,
            decision,
            rasterSession,
            sourceDocumentSha256);

        cancellationToken
            .ThrowIfCancellationRequested();

        await using var pageRasterBytes =
            new MemoryStream();

        var pageRaster =
            await rasterSession
                .RenderPageAsync(
                    sourcePage.PhysicalPageNumber,
                    pageRasterBytes,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        ValidatePageRaster(
            sourcePage,
            pageRaster);

        Rewind(
            pageRasterBytes);

        var layout =
            await _layoutAnalyzer
                .AnalyzeAsync(
                    pageRasterBytes,
                    sourcePage.PhysicalPageNumber,
                    pageRaster.OutputPixelWidth,
                    pageRaster.OutputPixelHeight,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        ValidateLayout(
            sourcePage,
            layout);

        var pairings =
            _textExecutor
                .CreateNativePresentPairings(
                    sourcePage,
                    layout);

        var ocrTargets =
            _textExecutor
                .CreateOcrTargets(
                    layout,
                    pageRaster);

        var visualTargets =
            VisualPreservationPlanner
                .Create(
                    layout,
                    pageRaster.OutputPixelWidth,
                    pageRaster.OutputPixelHeight)
                .ToDictionary(
                    target =>
                        target
                            .SourceLayoutObservation
                            .ObservationSequence);

        if (visualTargets.Count >
                0 &&
            openVisualDestinationAsync is null)
        {
            throw new InvalidOperationException(
                "Hybrid reconciliation requires a caller-owned visual destination " +
                "for every Figure region.");
        }

        var elements =
            new List<HybridDocumentElement>(
                layout.Observations.Count);

        foreach (var observation in
                 layout.Observations
                     .OrderBy(
                         item =>
                             item.ReadingOrder ??
                             int.MaxValue)
                     .ThenBy(
                         item =>
                             item.ObservationSequence))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            switch (LayoutTreatmentPolicy.Decide(
                        observation.Kind))
            {
                case LayoutTreatment.RecognizeText:
                    if (!pairings.TryGetValue(
                            observation.ObservationSequence,
                            out var pairing))
                    {
                        throw new InvalidDataException(
                            $"OCR-authorized layout observation " +
                            $"{observation.ObservationSequence} has no native/layout " +
                            "pairing result.");
                    }

                    elements.Add(
                        await _textExecutor
                            .ExecuteNativePresentAsync(
                                sourcePage,
                                decision.Assessment.NativeTextStatus,
                                rasterSession,
                                pageRaster,
                                observation,
                                pairing,
                                ocrTargets,
                                cancellationToken)
                            .ConfigureAwait(
                                false));

                    break;

                case LayoutTreatment.PreserveVisualWithoutOcr:
                    elements.Add(
                        await ExecuteVisualRegionAsync(
                                sourcePage,
                                rasterSession,
                                pageRaster,
                                sourceDocumentSha256,
                                observation,
                                visualTargets,
                                openVisualDestinationAsync!,
                                cancellationToken)
                            .ConfigureAwait(
                                false));

                    break;

                case LayoutTreatment.Deferred:
                    elements.Add(
                        HybridDocumentElementFactory
                            .FromDeferred(
                                observation));

                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported layout treatment " +
                        $"{LayoutTreatmentPolicy.Decide(observation.Kind)}.");
            }
        }

        return HybridDocumentAssembler
            .AssemblePage(
                sourcePage,
                elements);
    }

    #endregion

    #region Visual preservation execution

    private async ValueTask<HybridDocumentElement> ExecuteVisualRegionAsync(
        DocumentExtractionPage sourcePage,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        string sourceDocumentSha256,
        LayoutObservation observation,
        IReadOnlyDictionary<int, VisualPreservationTarget> visualTargets,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>
            openVisualDestinationAsync,
        CancellationToken cancellationToken)
    {
        if (!visualTargets.TryGetValue(
                observation.ObservationSequence,
                out var target))
        {
            throw new InvalidDataException(
                $"Visual-preservation layout observation " +
                $"{observation.ObservationSequence} has no preservation plan.");
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
                .ConfigureAwait(
                    false);

        ValidateCropRaster(
            sourcePage,
            pageRaster,
            target.Crop,
            cropRaster);

        Rewind(
            cropBytes);

        var destination =
            await openVisualDestinationAsync(
                    observation,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        if (destination is null)
        {
            throw new InvalidOperationException(
                "Visual destination factory returned null.");
        }

        var preserved =
            await _visualAssetPreserver
                .PreserveAsync(
                    cropBytes,
                    destination,
                    sourceDocumentSha256,
                    cropRaster.ProfileId,
                    cropRaster.MediaType,
                    observation,
                    target.Crop,
                    pageRaster.OutputPixelWidth,
                    pageRaster.OutputPixelHeight,
                    cancellationToken)
                .ConfigureAwait(
                    false);

        return HybridDocumentElementFactory
            .FromPreservedVisual(
                preserved);
    }

    #endregion

    #region Validation

    private static void ValidateRequest(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision decision,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        ArgumentNullException.ThrowIfNull(
            decision);

        ArgumentNullException.ThrowIfNull(
            rasterSession);

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        var normalizedSha =
            sourceDocumentSha256
                .Trim();

        if (normalizedSha.Length !=
                64 ||
            normalizedSha.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Source document SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(sourceDocumentSha256));
        }

        if (decision.PhysicalPageNumber !=
            sourcePage.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Page-processing decision must belong to the source page.",
                nameof(decision));
        }

        if (decision.Assessment.NativeTextStatus is
            NativeTextStatus.Missing or
            NativeTextStatus.Healthy)
        {
            throw new InvalidOperationException(
                "Native-present hybrid reconciliation requires " +
                "Suspicious or Unverified native text.");
        }

        if (decision.Plan.Route !=
            PageProcessingRoute
                .LayoutWithTargetedOcrReconciliation)
        {
            throw new InvalidOperationException(
                "Native-present hybrid executor accepts only the targeted OCR " +
                "reconciliation route.");
        }

        if (sourcePage.Blocks.Count ==
            0)
        {
            throw new InvalidOperationException(
                "Native-present hybrid reconciliation requires native text blocks.");
        }
    }

    private static void ValidatePageRaster(
        DocumentExtractionPage sourcePage,
        RasterRenderResult pageRaster)
    {
        if (pageRaster.PhysicalPageNumber !=
            sourcePage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Page raster belongs to a different physical page.");
        }

        if (!pageRaster.IsFullPage)
        {
            throw new InvalidDataException(
                "Hybrid page execution requires a full-page raster before layout analysis.");
        }
    }

    private static void ValidateLayout(
        DocumentExtractionPage sourcePage,
        LayoutAnalysisResult layout)
    {
        ArgumentNullException.ThrowIfNull(
            layout);

        if (layout.PhysicalPageNumber !=
            sourcePage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Layout result belongs to a different physical page.");
        }
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
