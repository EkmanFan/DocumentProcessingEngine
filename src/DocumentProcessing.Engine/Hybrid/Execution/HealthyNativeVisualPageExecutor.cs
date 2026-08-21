using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Executes the narrow authoritative visual-only branch for a Healthy native
/// page whose two-axis source evidence already requests meaningful visual
/// preservation.
///
/// Native text remains authoritative. This executor performs:
///
/// full-page raster -> layout -> semantic Figure assessment -> regional visual
/// preservation -> native/layout visual order merge.
///
/// It never performs OCR or native/OCR reconciliation. Unresolved layout Figure
/// evidence cannot create an asset by itself. Every already-resolved meaningful
/// source visual supplies exactly one Figure crop, and reliable source geometry
/// places that Figure in layout order even when PP fragments or omits it.
/// </summary>
public sealed class HealthyNativeVisualPageExecutor
{
    #region Variables and Constants

    private readonly IPageLayoutAnalyzer _layoutAnalyzer;
    private readonly HybridLayoutVisualExecutor _visualExecutor;

    #endregion

    #region ctor

    public HealthyNativeVisualPageExecutor(
        IPageLayoutAnalyzer layoutAnalyzer,
        VisualAssetPreserver visualAssetPreserver)
    {
        _layoutAnalyzer =
            layoutAnalyzer ??
            throw new ArgumentNullException(
                nameof(layoutAnalyzer));

        _visualExecutor =
            new HybridLayoutVisualExecutor(
                visualAssetPreserver ??
                throw new ArgumentNullException(
                    nameof(visualAssetPreserver)));
    }

    #endregion

    #region Methods Execution

    public async ValueTask<HybridDocumentPage> ExecuteAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision authoritativeDecision,
        PageExecutionPlan candidatePlan,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken = default)
    {
        if (candidatePlan.RequiresVisualAnalysis)
        {
            throw new InvalidOperationException(
                "Healthy native unresolved visual analysis requires aligned " +
                "source-visual observations.");
        }

        var prepared =
            await PrepareLayoutAsync(
                    sourcePage,
                    authoritativeDecision,
                    candidatePlan,
                    rasterSession,
                    sourceDocumentSha256,
                    cancellationToken)
                .ConfigureAwait(false);

        return await ExecutePreparedAsync(
                sourcePage,
                candidatePlan,
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
        PageProcessingDecision authoritativeDecision,
        PageExecutionPlan candidatePlan,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            sourcePage,
            authoritativeDecision,
            candidatePlan,
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
    /// Executes the authoritative healthy-native visual branch from an already
    /// acquired neutral layout result.
    ///
    /// The supplied full-page raster result is metadata captured during the
    /// earlier layout phase. No full-page raster bytes are required here; only
    /// targeted regional visual rendering remains for semantic visual custody.
    /// </summary>
    public async ValueTask<HybridDocumentPage> ExecuteWithPrecomputedLayoutAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision authoritativeDecision,
        PageExecutionPlan candidatePlan,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken = default) =>
        await ExecuteWithPrecomputedLayoutAsync(
                sourcePage,
                authoritativeDecision,
                candidatePlan,
                sourceVisualObservations:
                    [],
                rasterSession,
                pageRaster,
                layout,
                sourceDocumentSha256,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask<HybridDocumentPage> ExecuteWithPrecomputedLayoutAsync(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision authoritativeDecision,
        PageExecutionPlan candidatePlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout,
        string sourceDocumentSha256,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            sourcePage,
            authoritativeDecision,
            candidatePlan,
            rasterSession,
            sourceDocumentSha256);

        ArgumentNullException.ThrowIfNull(
            pageRaster);

        ArgumentNullException.ThrowIfNull(
            layout);

        ArgumentNullException.ThrowIfNull(
            sourceVisualObservations);

        if (candidatePlan.RequiresVisualAnalysis &&
            sourceVisualObservations.Count ==
                0)
        {
            throw new InvalidOperationException(
                "Healthy native unresolved visual analysis requires aligned " +
                "source-visual observations.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        ValidatePageRaster(
            sourcePage,
            pageRaster);

        ValidateLayout(
            sourcePage,
            layout);

        return await ExecutePreparedAsync(
                sourcePage,
                candidatePlan,
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
        PageExecutionPlan candidatePlan,
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
            sourceVisualObservations.Count ==
                0
                ? layout
                : SourceBackedLayoutVisualMatcher
                    .AddSourceFigures(
                        candidatePlan,
                        sourceVisualObservations,
                        layout);

        var visualEvidence =
            _visualExecutor
                .Assess(
                    executionLayout)
                .Values
                .OrderBy(
                    evidence =>
                        evidence.Observation.ReadingOrder ??
                        int.MaxValue)
                .ThenBy(
                    evidence =>
                        evidence.Observation.ObservationSequence)
                .ToArray();

        var preserving =
            ResolvePreservingEvidence(
                candidatePlan,
                sourceVisualObservations,
                executionLayout,
                visualEvidence);

        if (preserving.Length >
                0 &&
            openVisualDestinationAsync is null)
        {
            throw new InvalidOperationException(
                "Healthy native meaningful visual preservation requires a " +
                "caller-owned destination.");
        }

        var visualElements =
            new List<HybridDocumentElement>(
                preserving.Length);

        foreach (var evidence in
                 preserving)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            if (visualElement is null ||
                visualElement.Kind !=
                HybridDocumentElementKind.Visual)
            {
                throw new InvalidDataException(
                    $"Preservable Figure observation " +
                    $"{evidence.Observation.ObservationSequence} did not produce " +
                    "a resolved visual element.");
            }

            visualElements.Add(
                visualElement);
        }

        return NativeLayoutVisualPageAssembler
            .Assemble(
                sourcePage,
                executionLayout,
                visualElements);
    }

    private static LayoutVisualEvidence[] ResolvePreservingEvidence(
        PageExecutionPlan candidatePlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        LayoutAnalysisResult layout,
        IReadOnlyList<LayoutVisualEvidence> visualEvidence)
    {
        var preserving =
            visualEvidence
                .Where(
                    evidence =>
                        VisualEvidenceDispositionPolicy
                            .Decide(
                                evidence.Kind) is
                            VisualDisposition.PreserveMeaningfulVisual or
                            VisualDisposition.PreserveUnqualifiedVisual)
                .ToArray();

        var unresolved =
            visualEvidence
                .Where(
                    evidence =>
                        VisualEvidenceDispositionPolicy
                            .Decide(
                                evidence.Kind) ==
                        VisualDisposition.RequiresVisualAnalysis)
                .ToArray();

        if (sourceVisualObservations.Count >
            0)
        {
            if (SourceBackedLayoutVisualMatcher.TryResolve(
                    candidatePlan,
                    sourceVisualObservations,
                    layout,
                    out var sourceBacked))
            {
                return sourceBacked;
            }

            throw new InvalidDataException(
                $"Physical page {candidatePlan.PhysicalPageNumber} requested " +
                "meaningful source-visual preservation, but one source-backed " +
                "layout Figure could not be resolved for every planned source visual.");
        }

        if (unresolved.Length ==
            0)
        {
            if (preserving.Length ==
                0)
            {
                throw new InvalidDataException(
                    "Source visual planning requested meaningful preservation, " +
                    "but layout analysis produced no preservable semantic Figure.");
            }

            return preserving;
        }

        if (CanResolveSourceBackedSingletonFigure(
                candidatePlan,
                layout,
                visualEvidence,
                preserving,
                unresolved))
        {
            return
            [
                new LayoutVisualEvidence(
                    unresolved[0].Observation,
                    VisualEvidenceKind.SourceBackedMeaningfulVisual)
            ];
        }

        throw new InvalidDataException(
            $"Healthy native visual execution encountered unresolved " +
            $"Figure evidence at observation " +
            $"{unresolved[0].Observation.ObservationSequence}; " +
            "the narrow preservation cutover fails closed.");
    }

    private static bool CanResolveSourceBackedSingletonFigure(
        PageExecutionPlan candidatePlan,
        LayoutAnalysisResult layout,
        IReadOnlyList<LayoutVisualEvidence> visualEvidence,
        IReadOnlyList<LayoutVisualEvidence> preserving,
        IReadOnlyList<LayoutVisualEvidence> unresolved)
    {
        if (candidatePlan.VisualElements.Count !=
                1 ||
            candidatePlan.VisualElements[0].Action !=
                VisualExecutionAction.PreserveMeaningfulVisual ||
            visualEvidence.Count !=
                1 ||
            preserving.Count !=
                0 ||
            unresolved.Count !=
                1)
        {
            return false;
        }

        var figure =
            unresolved[0].Observation;

        if (figure.Kind !=
                LayoutObservationKind.Figure ||
            figure.ReadingOrder is null)
        {
            return false;
        }

        return !layout.Observations.Any(
            observation =>
                !ReferenceEquals(
                    observation,
                    figure) &&
                IsSemanticTextLike(
                    observation.Kind) &&
                Intersects(
                    figure.Bounds,
                    observation.Bounds));
    }

    private static bool IsSemanticTextLike(
        LayoutObservationKind kind) =>
        kind is
            LayoutObservationKind.Text or
            LayoutObservationKind.Heading or
            LayoutObservationKind.Caption or
            LayoutObservationKind.Table;

    private static bool Intersects(
        NormalizedRectangle first,
        NormalizedRectangle second) =>
        Math.Max(
            first.Left,
            second.Left) <
        Math.Min(
            first.Right,
            second.Right) &&
        Math.Max(
            first.Top,
            second.Top) <
        Math.Min(
            first.Bottom,
            second.Bottom);

    #endregion

    #region Methods Validation

    private static void ValidateRequest(
        DocumentExtractionPage sourcePage,
        PageProcessingDecision authoritativeDecision,
        PageExecutionPlan candidatePlan,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        ArgumentNullException.ThrowIfNull(
            authoritativeDecision);

        ArgumentNullException.ThrowIfNull(
            candidatePlan);

        ArgumentNullException.ThrowIfNull(
            rasterSession);

        if (authoritativeDecision.PhysicalPageNumber !=
                sourcePage.PhysicalPageNumber ||
            candidatePlan.PhysicalPageNumber !=
                sourcePage.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Authoritative and candidate decisions must belong to the source page.");
        }

        if (authoritativeDecision.Assessment.NativeTextStatus !=
            NativeTextStatus.Healthy)
        {
            throw new InvalidOperationException(
                "Healthy native visual execution requires NativeTextStatus.Healthy.");
        }

        if (authoritativeDecision.Plan.Route !=
            PageProcessingRoute.NativeOnly)
        {
            throw new InvalidOperationException(
                "Healthy native visual execution accepts only authoritative NativeOnly pages.");
        }

        if (candidatePlan.TextMode !=
                TextExecutionMode.NativeText ||
            candidatePlan.RequiresTargetedOcr ||
            (
                !candidatePlan.RequiresVisualAnalysis &&
                !candidatePlan.RequiresVisualPreservation
            ))
        {
            throw new InvalidOperationException(
                "Healthy native visual execution requires candidate NativeText " +
                "plus visual analysis or preservation and no OCR.");
        }

        if (sourcePage.Blocks.Count ==
            0)
        {
            throw new InvalidOperationException(
                "Healthy native visual execution requires authoritative native text blocks.");
        }

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
                "Healthy native visual execution requires a full-page raster " +
                "before layout analysis.");
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
                "Internal healthy-native visual buffer must be seekable.");
        }

        stream.Position =
            0;
    }

    #endregion
}
