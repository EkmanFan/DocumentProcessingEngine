using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
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
/// evidence fails closed rather than being silently dropped or promoted.
/// </summary>
public sealed class HealthyNativeVisualPageExecutor
{
    private readonly IPageLayoutAnalyzer _layoutAnalyzer;
    private readonly HybridLayoutVisualExecutor _visualExecutor;

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

        var visualEvidence =
            _visualExecutor
                .Assess(
                    layout)
                .Values
                .OrderBy(
                    evidence =>
                        evidence.Observation.ReadingOrder ??
                        int.MaxValue)
                .ThenBy(
                    evidence =>
                        evidence.Observation.ObservationSequence)
                .ToArray();

        var unresolved =
            visualEvidence
                .FirstOrDefault(
                    evidence =>
                        VisualEvidenceDispositionPolicy
                            .Decide(
                                evidence.Kind) ==
                        VisualDisposition.RequiresVisualAnalysis);

        if (unresolved is not null)
        {
            throw new InvalidDataException(
                $"Healthy native visual execution encountered unresolved " +
                $"Figure evidence at observation " +
                $"{unresolved.Observation.ObservationSequence}; " +
                "the narrow preservation cutover fails closed.");
        }

        var preserving =
            visualEvidence
                .Where(
                    evidence =>
                        VisualEvidenceDispositionPolicy
                            .Decide(
                                evidence.Kind) ==
                        VisualDisposition.PreserveMeaningfulVisual)
                .ToArray();

        if (preserving.Length ==
            0)
        {
            throw new InvalidDataException(
                "Source visual planning requested meaningful preservation, " +
                "but layout analysis produced no preservable semantic Figure.");
        }

        if (openVisualDestinationAsync is null)
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
                layout,
                visualElements);
    }

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
                "Legacy and candidate decisions must belong to the source page.");
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
                "Healthy native visual execution accepts only legacy NativeOnly pages.");
        }

        if (candidatePlan.TextMode !=
                TextExecutionMode.NativeText ||
            candidatePlan.RequiresTargetedOcr ||
            candidatePlan.RequiresVisualAnalysis ||
            !candidatePlan.RequiresMeaningfulVisualPreservation)
        {
            throw new InvalidOperationException(
                "Healthy native visual execution requires candidate NativeText " +
                "plus resolved meaningful-visual preservation and no OCR or " +
                "unresolved visual analysis.");
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
}
