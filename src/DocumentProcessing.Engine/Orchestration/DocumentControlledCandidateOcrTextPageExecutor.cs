using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Executes the OCR-backed controlled candidate page path.
///
/// It reuses <see cref="TargetedHybridTextExecutor"/> for OCR planning,
/// target-centric pairing, recognition, and reconciliation. Neutral layout
/// visual evidence is assessed independently, and only semantically authorized
/// regions are materialized as non-authoritative preservation evidence.
/// </summary>
internal sealed class DocumentControlledCandidateOcrTextPageExecutor
{
    private readonly IPageLayoutAnalyzer _layoutAnalyzer;
    private readonly TargetedHybridTextExecutor _textExecutor;

    public DocumentControlledCandidateOcrTextPageExecutor(
        IPageLayoutAnalyzer layoutAnalyzer,
        DocumentProcessing.Core.Ocr.IRegionTextRecognizer textRecognizer)
    {
        _layoutAnalyzer =
            layoutAnalyzer ??
            throw new ArgumentNullException(
                nameof(layoutAnalyzer));

        _textExecutor =
            new TargetedHybridTextExecutor(
                textRecognizer);
    }

    public async ValueTask<(
        HybridDocumentPage Page,
        IReadOnlyList<LayoutVisualEvidence> LayoutVisualEvidence,
        IReadOnlyList<PreservedVisualEvidence> PreservedLayoutVisuals)> ExecuteAsync(
        DocumentExtractionPage sourcePage,
        NativeTextStatus nativeTextStatus,
        TextExecutionMode textMode,
        IDocumentRasterizationSession rasterSession,
        string sourceDocumentSha256,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            sourcePage,
            nativeTextStatus,
            textMode,
            rasterSession);

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

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

        IReadOnlyList<HybridDocumentElement> elements;

        if (textMode ==
            TextExecutionMode.TargetedOcrRecovery)
        {
            elements =
                await ExecuteRecoveryAsync(
                        sourcePage,
                        rasterSession,
                        pageRaster,
                        layout,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            elements =
                await ExecuteNativePresentAsync(
                        sourcePage,
                        nativeTextStatus,
                        rasterSession,
                        pageRaster,
                        layout,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        elements =
            RetainNeutralDeferredLayoutEvidence(
                elements,
                layout);

        var candidatePage =
            HybridDocumentAssembler
                .AssemblePage(
                    sourcePage,
                    elements);

        var layoutVisualEvidence =
            new DefaultLayoutVisualEvidenceAssessor()
                .Assess(
                    layout);

        var preservedLayoutVisuals =
            await PreserveLayoutVisualsAsync(
                    layoutVisualEvidence,
                    rasterSession,
                    pageRaster,
                    sourceDocumentSha256,
                    cancellationToken)
                .ConfigureAwait(false);

        return (
            candidatePage,
            layoutVisualEvidence,
            preservedLayoutVisuals);
    }

    private static async ValueTask<IReadOnlyList<PreservedVisualEvidence>>
        PreserveLayoutVisualsAsync(
            IReadOnlyList<LayoutVisualEvidence> layoutVisualEvidence,
            IDocumentRasterizationSession rasterSession,
            RasterRenderResult pageRaster,
            string sourceDocumentSha256,
            CancellationToken cancellationToken)
    {
        var meaningful =
            layoutVisualEvidence
                .Where(
                    evidence =>
                        VisualEvidenceDispositionPolicy.Decide(
                            evidence.Kind) ==
                        VisualDisposition.PreserveMeaningfulVisual)
                .ToArray();

        if (meaningful.Length ==
            0)
        {
            return [];
        }

        var preserver =
            new LayoutVisualRegionPreserver();

        var preserved =
            new List<PreservedVisualEvidence>(
                meaningful.Length);

        foreach (var evidence in meaningful)
        {
            cancellationToken.ThrowIfCancellationRequested();

            preserved.Add(
                await preserver
                    .PreserveAsync(
                        evidence,
                        rasterSession,
                        pageRaster,
                        sourceDocumentSha256,
                        Stream.Null,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        return preserved;
    }

    private async ValueTask<IReadOnlyList<HybridDocumentElement>>
        ExecuteRecoveryAsync(
            DocumentExtractionPage sourcePage,
            IDocumentRasterizationSession rasterSession,
            RasterRenderResult pageRaster,
            LayoutAnalysisResult layout,
            CancellationToken cancellationToken)
    {
        var ocrTargets =
            _textExecutor
                .CreateOcrTargets(
                    layout,
                    pageRaster);

        var elements =
            new List<HybridDocumentElement>();

        foreach (var observation in
                 OrderedTextObservations(
                     layout))
        {
            cancellationToken.ThrowIfCancellationRequested();

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
        }

        return elements;
    }

    private async ValueTask<IReadOnlyList<HybridDocumentElement>>
        ExecuteNativePresentAsync(
            DocumentExtractionPage sourcePage,
            NativeTextStatus nativeTextStatus,
            IDocumentRasterizationSession rasterSession,
            RasterRenderResult pageRaster,
            LayoutAnalysisResult layout,
            CancellationToken cancellationToken)
    {
        // Keep the legacy fail-closed ordering: pairing/ambiguity is resolved
        // before OCR targets and before any recognition call.
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

        var elements =
            new List<HybridDocumentElement>();

        foreach (var observation in
                 OrderedTextObservations(
                     layout))
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                        nativeTextStatus,
                        rasterSession,
                        pageRaster,
                        observation,
                        pairing,
                        ocrTargets,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        return elements;
    }

    private static IReadOnlyList<HybridDocumentElement>
        RetainNeutralDeferredLayoutEvidence(
            IReadOnlyList<HybridDocumentElement> textElements,
            LayoutAnalysisResult layout)
    {
        var deferred =
            layout.Observations
                .Where(
                    observation =>
                        !LayoutTextPolicy.IsTextRecognitionCandidate(
                            observation.Kind) &&
                        observation.Kind !=
                        LayoutObservationKind.Figure)
                .Select(
                    HybridDocumentElementFactory.FromDeferred)
                .ToArray();

        if (deferred.Length ==
            0)
        {
            return textElements;
        }

        return textElements
            .Concat(
                deferred)
            .ToArray();
    }

    private static IEnumerable<LayoutObservation> OrderedTextObservations(
        LayoutAnalysisResult layout) =>
        layout.Observations
            .Where(
                observation =>
                    LayoutTextPolicy.IsTextRecognitionCandidate(
                        observation.Kind))
            .OrderBy(
                observation =>
                    observation.ReadingOrder ??
                    int.MaxValue)
            .ThenBy(
                observation =>
                    observation.ObservationSequence);

    private static void ValidateRequest(
        DocumentExtractionPage sourcePage,
        NativeTextStatus nativeTextStatus,
        TextExecutionMode textMode,
        IDocumentRasterizationSession rasterSession)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        ArgumentNullException.ThrowIfNull(
            rasterSession);

        var valid =
            textMode switch
            {
                TextExecutionMode.TargetedOcrRecovery =>
                    nativeTextStatus ==
                    NativeTextStatus.Missing,

                TextExecutionMode.TargetedOcrVerification =>
                    nativeTextStatus ==
                    NativeTextStatus.Unverified,

                TextExecutionMode.TargetedOcrReconciliation =>
                    nativeTextStatus ==
                    NativeTextStatus.Suspicious,

                _ =>
                    false
            };

        if (!valid)
        {
            throw new InvalidOperationException(
                $"Controlled candidate text mode '{textMode}' is not valid for " +
                $"native-text status '{nativeTextStatus}'.");
        }

        if ((textMode is
                 TextExecutionMode.TargetedOcrVerification or
                 TextExecutionMode.TargetedOcrReconciliation) &&
            sourcePage.Blocks.Count ==
            0)
        {
            throw new InvalidOperationException(
                "Controlled native-present OCR execution requires native text blocks.");
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
                "Controlled candidate page raster belongs to a different physical page.");
        }

        if (!pageRaster.IsFullPage)
        {
            throw new InvalidDataException(
                "Controlled candidate OCR requires a full-page raster before layout analysis.");
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
                "Controlled candidate layout result belongs to a different physical page.");
        }
    }

    private static void Rewind(
        Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException(
                "Internal controlled candidate buffer must be seekable.");
        }

        stream.Position =
            0;
    }
}
