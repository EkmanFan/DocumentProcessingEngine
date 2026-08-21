using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Executes the concrete V1 hybrid recovery route for a page with no usable
/// native text.
///
/// This executor deliberately owns only the missing-native route:
///
/// page raster -> layout -> independent text/visual policies -> targeted OCR /
/// semantic visual preservation / deferred evidence -> hybrid page.
///
/// Native-present reconciliation is intentionally a separate increment because
/// the current reconciliation boundary requires explicit native/layout spatial
/// pairing. Phase 21C.2A does not invent that matching policy.
///
/// Visual binary destinations remain caller-owned. The supplied factory is
/// asked for a destination only after semantic visual evidence resolves to
/// PreserveMeaningfulVisual.
/// </summary>
public sealed class MissingNativeHybridPageExecutor
{
    #region Variables and Constants

    private readonly IPageLayoutAnalyzer _layoutAnalyzer;
    private readonly TargetedHybridTextExecutor _textExecutor;
    private readonly HybridLayoutVisualExecutor _visualExecutor;

    #endregion

    #region ctor

    public MissingNativeHybridPageExecutor(
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

        _visualExecutor =
            new HybridLayoutVisualExecutor(
                visualAssetPreserver);
    }

    #endregion

    #region Methods Execution

    public async ValueTask<HybridDocumentPage> ExecuteAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision decision,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync = null,
        CancellationToken cancellationToken = default)
    {
        var prepared =
            await PrepareLayoutAsync(
                    sourcePage,
                    decision,
                    rasterSession,
                    sourceDocumentSha256,
                    cancellationToken)
                .ConfigureAwait(false);

        return await ExecutePreparedAsync(
                sourcePage,
                sourceVisualPlan:
                    null,
                sourceVisualObservations:
                    [],
                rasterSession,
                prepared.PageRaster,
                prepared.Layout,
                sourceDocumentSha256,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<(
        RasterRenderResult PageRaster,
        LayoutAnalysisResult Layout)> PrepareLayoutAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision decision,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            sourcePage,
            decision,
            rasterSession,
            sourceDocumentSha256);

        cancellationToken.ThrowIfCancellationRequested();

        await using var pageRasterBytes =
            new MemoryStream();

        var pageRaster =
            await rasterSession
                .RenderPageAsync(
                    sourcePage.PhysicalPageNumber,
                    pageRasterBytes,
                    cancellationToken)
                .ConfigureAwait(false);

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
                .ConfigureAwait(false);

        ValidateLayout(
            sourcePage,
            layout);

        return (
            pageRaster,
            layout);
    }

    /// <summary>
    /// Executes the authoritative missing-native route from an already acquired
    /// neutral layout result.
    ///
    /// The supplied full-page raster result is metadata captured during the
    /// earlier layout phase. No full-page raster bytes are required here; only
    /// targeted region rendering remains for OCR and semantic visual custody.
    /// </summary>
    public async ValueTask<HybridDocumentPage> ExecuteWithPrecomputedLayoutAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision decision,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithPrecomputedLayoutAsync(
                sourcePage,
                decision,
                sourceVisualPlan:
                    null,
                sourceVisualObservations:
                    [],
                rasterSession,
                pageRaster,
                layout,
                sourceDocumentSha256,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<HybridDocumentPage> ExecuteWithPrecomputedLayoutAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision decision,
        PageExecutionPlan? sourceVisualPlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout,
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

        ArgumentNullException.ThrowIfNull(
            pageRaster);

        ArgumentNullException.ThrowIfNull(
            layout);

        ArgumentNullException.ThrowIfNull(
            sourceVisualObservations);

        cancellationToken.ThrowIfCancellationRequested();

        ValidatePageRaster(
            sourcePage,
            pageRaster);

        ValidateLayout(
            sourcePage,
            layout);

        return await ExecutePreparedAsync(
                sourcePage,
                sourceVisualPlan,
                sourceVisualObservations,
                rasterSession,
                pageRaster,
                layout,
                sourceDocumentSha256,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<HybridDocumentPage> ExecutePreparedAsync(
        DocumentExtractionPage sourcePage,
        PageExecutionPlan? sourceVisualPlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken)
    {
        var executionLayout =
            layout;

        LayoutVisualEvidence[] sourceBacked =
            [];

        if (sourceVisualPlan is not null &&
            sourceVisualObservations.Count >
                0 &&
            !SourceBackedLayoutVisualMatcher
                .TryResolveWithSourceFigures(
                    sourceVisualPlan,
                    sourceVisualObservations,
                    layout,
                    out executionLayout,
                    out sourceBacked))
        {
            throw new InvalidDataException(
                $"Physical page {sourcePage.PhysicalPageNumber} could not " +
                "resolve every source-backed visual after layout analysis.");
        }

        var ocrTargets =
            _textExecutor
                .CreateOcrTargets(
                    executionLayout,
                    pageRaster);

        var visualEvidence =
            _visualExecutor
                .Assess(
                    executionLayout)
                .ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        pair.Value);

        foreach (var evidence in
                 sourceBacked)
        {
            visualEvidence[evidence.Observation.ObservationSequence] =
                evidence;
        }

        if (HybridLayoutVisualExecutor
                .RequiresPreservationDestination(
                    visualEvidence.Values) &&
            openVisualDestinationAsync is null)
        {
            throw new InvalidOperationException(
                "Hybrid recovery requires a caller-owned visual destination " +
                "for every semantically meaningful Figure region.");
        }

        var elements =
            new List<HybridDocumentElement>(
                executionLayout.Observations.Count);

        foreach (var observation in
                 executionLayout.Observations
                     .OrderBy(
                         item =>
                             item.ReadingOrder ??
                             int.MaxValue)
                     .ThenBy(
                         item =>
                             item.ObservationSequence))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (LayoutTextPolicy.IsTextRecognitionCandidate(
                    observation.Kind))
            {
                elements.Add(
                    await _textExecutor
                        .ExecuteMissingAsync(
                            sourcePage,
                            rasterSession,
                            pageRaster,
                            observation,
                            ocrTargets,
                            cancellationToken)
                        .ConfigureAwait(false));

                continue;
            }

            if (observation.Kind ==
                LayoutObservationKind.Figure)
            {
                if (SourceBackedLayoutVisualMatcher
                    .IsBackendFigureCoveredBySourceVisual(
                        observation,
                        sourceBacked))
                {
                    continue;
                }

                if (!visualEvidence.TryGetValue(
                        observation.ObservationSequence,
                        out var evidence))
                {
                    throw new InvalidDataException(
                        $"Figure layout observation " +
                        $"{observation.ObservationSequence} has no semantic " +
                        "visual evidence.");
                }

                var visualElement =
                    await _visualExecutor
                        .ExecuteAsync(
                            evidence,
                            rasterSession,
                            pageRaster,
                            sourceDocumentSha256,
                            openVisualDestinationAsync,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (visualElement is not null)
                {
                    elements.Add(
                        visualElement);
                }

                continue;
            }

            elements.Add(
                HybridDocumentElementFactory
                    .FromDeferred(
                        observation));
        }

        return HybridDocumentAssembler
            .AssemblePage(
                sourcePage,
                elements);
    }

    #endregion

    #region Methods Validation

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

        if (decision.Assessment.NativeTextStatus !=
            NativeTextStatus.Missing)
        {
            throw new InvalidOperationException(
                "Missing-native hybrid recovery requires NativeTextStatus.Missing.");
        }

        if (decision.Plan.Route !=
            PageProcessingRoute.LayoutWithTargetedOcrRecovery)
        {
            throw new InvalidOperationException(
                "Missing-native hybrid executor accepts only the targeted OCR recovery route.");
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
