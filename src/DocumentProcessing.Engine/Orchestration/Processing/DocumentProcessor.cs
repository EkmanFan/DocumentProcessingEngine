using System.Buffers;
using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Results;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Engine.DualRun.InProcess;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Public end-to-end deterministic document-processing entry point.
///
/// Phase 21C.3 connects the already-proven page planner to all currently
/// supported V1 execution routes:
///
/// source
///   -> selected format execution
///   -> native extraction
///   -> preflight
///   -> deterministic page assessment / route planning
///   -> per-page NativeOnly / missing-native recovery / native-present
///      reconciliation execution
///   -> common hybrid assembly
///   -> normalization
///   -> segmentation
///   -> provenance / quality projection
///   -> DocumentIngestionResult
///
/// The processor orchestrates existing components. It does not reproduce
/// layout, OCR, pairing, reconciliation, normalization, or segmentation logic.
/// </summary>
public sealed class DocumentProcessor
{
    #region Variables and Constants

    private readonly DocumentFormatId _format;
    private readonly IDocumentExtractor? _nativeExtractor;
    private readonly IDocumentPreflightAnalyzer _preflightAnalyzer;
    private readonly DocumentPageProcessingPlanner _pageProcessingPlanner;
    private readonly DocumentHybridExecutionDependencies? _hybridExecution;
    private readonly DocumentAuthoritativeVisualPlanningRunner?
        _authoritativeVisualPlanningRunner;
    private readonly DocumentDualRunPlanningDependencies? _dualRunPlanningDependencies;
    private readonly DocumentDualRunPlanningRunner? _dualRunPlanningRunner;
    private readonly DocumentDualRunCandidateTextExecutionRunner?
        _dualRunCandidateTextExecutionRunner;
    private readonly string _engineVersion;
    private readonly ProcessingComponentIdentity _nativeExtractionIdentity;

    #endregion

    #region ctor

    /// <summary>
    /// Backward-compatible native-capable composition.
    ///
    /// Page planning still occurs. If a real document selects a hybrid route,
    /// processing fails explicitly because no hybrid execution dependencies
    /// were configured.
    /// </summary>
    public DocumentProcessor(
        DocumentFormatId format,
        IDocumentExtractor nativeExtractor,
        IDocumentPreflightAnalyzer preflightAnalyzer,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity,
        DocumentDualRunPlanningDependencies? dualRunPlanning = null,
        DocumentDualRunCandidateTextExecutionDependencies?
            dualRunCandidateTextExecution = null)
        : this(
            format,
            nativeExtractor ??
                throw new ArgumentNullException(
                    nameof(nativeExtractor)),
            preflightAnalyzer,
            DocumentPageProcessingPlanner.CreateDefault(),
            hybridExecution:
                null,
            engineVersion,
            nativeExtractionIdentity,
            requireHybridExecution:
                false,
            dualRunPlanning,
            dualRunCandidateTextExecution)
    {
    }

    /// <summary>
    /// Full V1 hybrid composition with an explicit deterministic planner.
    /// </summary>
    public DocumentProcessor(
        DocumentFormatId format,
        IDocumentExtractor nativeExtractor,
        IDocumentPreflightAnalyzer preflightAnalyzer,
        DocumentPageProcessingPlanner pageProcessingPlanner,
        DocumentHybridExecutionDependencies hybridExecution,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity,
        DocumentDualRunPlanningDependencies? dualRunPlanning = null,
        DocumentDualRunCandidateTextExecutionDependencies?
            dualRunCandidateTextExecution = null)
        : this(
            format,
            nativeExtractor ??
                throw new ArgumentNullException(
                    nameof(nativeExtractor)),
            preflightAnalyzer,
            pageProcessingPlanner,
            hybridExecution ??
                throw new ArgumentNullException(
                    nameof(hybridExecution)),
            engineVersion,
            nativeExtractionIdentity,
            requireHybridExecution:
                true,
            dualRunPlanning,
            dualRunCandidateTextExecution)
    {
    }

    /// <summary>
    /// Engine-internal composition for already acquired native evidence.
    /// </summary>
    internal DocumentProcessor(
        DocumentFormatId format,
        IDocumentPreflightAnalyzer preflightAnalyzer,
        DocumentPageProcessingPlanner pageProcessingPlanner,
        DocumentHybridExecutionDependencies? hybridExecution,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity)
        : this(
            format,
            nativeExtractor:
                null,
            preflightAnalyzer,
            pageProcessingPlanner,
            hybridExecution,
            engineVersion,
            nativeExtractionIdentity,
            requireHybridExecution:
                hybridExecution is not null)
    {
    }

    private DocumentProcessor(
        DocumentFormatId format,
        IDocumentExtractor? nativeExtractor,
        IDocumentPreflightAnalyzer preflightAnalyzer,
        DocumentPageProcessingPlanner pageProcessingPlanner,
        DocumentHybridExecutionDependencies? hybridExecution,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity,
        bool requireHybridExecution = false,
        DocumentDualRunPlanningDependencies? dualRunPlanning = null,
        DocumentDualRunCandidateTextExecutionDependencies?
            dualRunCandidateTextExecution = null)
    {
        if (string.IsNullOrWhiteSpace(
                format.Value))
        {
            throw new ArgumentException(
                "Selected document format cannot be empty.",
                nameof(format));
        }

        _format =
            format;

        _nativeExtractor =
            nativeExtractor;

        _preflightAnalyzer =
            preflightAnalyzer ??
            throw new ArgumentNullException(
                nameof(preflightAnalyzer));

        _pageProcessingPlanner =
            pageProcessingPlanner ??
            throw new ArgumentNullException(
                nameof(pageProcessingPlanner));

        if (requireHybridExecution &&
            hybridExecution is null)
        {
            throw new ArgumentNullException(
                nameof(hybridExecution));
        }

        _hybridExecution =
            hybridExecution;

        _authoritativeVisualPlanningRunner =
            hybridExecution?.AuthoritativeVisualPlanning is null
                ? null
                : new DocumentAuthoritativeVisualPlanningRunner(
                    hybridExecution.AuthoritativeVisualPlanning);

        _dualRunPlanningDependencies =
            dualRunPlanning;

        _dualRunPlanningRunner =
            dualRunPlanning is null
                ? null
                : new DocumentDualRunPlanningRunner(
                    dualRunPlanning);

        if (dualRunCandidateTextExecution is not null &&
            dualRunPlanning is null)
        {
            throw new ArgumentException(
                "Dual Run candidate execution requires Dual Run planning.",
                nameof(dualRunCandidateTextExecution));
        }

        _dualRunCandidateTextExecutionRunner =
            dualRunCandidateTextExecution is null
                ? null
                : new DocumentDualRunCandidateTextExecutionRunner(
                    dualRunCandidateTextExecution);

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        _engineVersion =
            engineVersion.Trim();

        _nativeExtractionIdentity =
            nativeExtractionIdentity ??
            throw new ArgumentNullException(
                nameof(nativeExtractionIdentity));
    }

    #endregion

    #region Methods Public Processing

    public Task<DocumentIngestionResult> ProcessAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default) =>
        ProcessCoreAsync(
            source,
            openVisualDestinationAsync:
                null,
            cancellationToken);

    /// <summary>
    /// Processes a document while allowing the caller to provide destinations
    /// for Figure evidence selected by deterministic layout policy.
    ///
    /// The engine writes preserved visual bytes to the returned stream but does
    /// not choose or own the caller's storage system.
    /// </summary>
    public Task<DocumentIngestionResult> ProcessAsync(
        DocumentSource source,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>
            openVisualDestinationAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            openVisualDestinationAsync);

        return ProcessCoreAsync(
            source,
            openVisualDestinationAsync,
            cancellationToken);
    }

    private async Task<DocumentIngestionResult> ProcessCoreAsync(
        DocumentSource source,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var nativeExtractor =
            _nativeExtractor ??
            throw new InvalidOperationException(
                "This document processor is configured for already acquired native evidence only.");

        await using var prepared =
            await PreparedDocumentSource
                .CreateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        var format =
            _format;

        if (!nativeExtractor.CanExtract(
                format))
        {
            throw new NotSupportedException(
                $"The configured native extractor cannot process format '{format}'.");
        }

        if (!_preflightAnalyzer.CanAnalyze(
                format))
        {
            throw new NotSupportedException(
                $"The configured preflight analyzer cannot process format '{format}'.");
        }

        DocumentExtractionWithRasterObservationsResult?
            coordinatedExtraction =
                null;

        DocumentExtractionResult extraction;

        prepared.ResetForRead();

        if (_dualRunPlanningDependencies is not null &&
            nativeExtractor is
                IDocumentExtractorWithRasterObservations
                    coordinatedExtractor &&
            coordinatedExtractor
                .CanExtractWithRasterObservations(
                    format,
                    _dualRunPlanningDependencies
                        .VisualRasterObservationSource))
        {
            coordinatedExtraction =
                await coordinatedExtractor
                    .ExtractWithRasterObservationsAsync(
                        prepared.Source,
                        format,
                        _dualRunPlanningDependencies
                            .VisualRasterObservationSource,
                        cancellationToken)
                    .ConfigureAwait(false);

            extraction =
                coordinatedExtraction
                    .Extraction;
        }
        else
        {
            extraction =
                await nativeExtractor
                    .ExtractAsync(
                        prepared.Source,
                        format,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        return await ProcessPreparedEvidenceCoreAsync(
                prepared,
                format,
                extraction,
                coordinatedExtraction?
                    .RasterObservations,
                coordinatedExtraction?
                    .RasterObservationFailure,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal Task<DocumentIngestionResult> ProcessPreparedEvidenceAsync(
        PreparedDocumentSource prepared,
        DocumentFormatId selectedFormat,
        NativeDocumentEvidence evidence,
        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            prepared);

        ArgumentNullException.ThrowIfNull(
            evidence);

        cancellationToken.ThrowIfCancellationRequested();

        if (selectedFormat !=
            _format)
        {
            throw new InvalidOperationException(
                $"Selected document format '{selectedFormat}' does not match " +
                $"the processor composition format '{_format}'.");
        }

        return ProcessPreparedEvidenceCoreAsync(
            prepared,
            selectedFormat,
            evidence.Extraction,
            evidence.RasterObservations,
            evidence.RasterObservationFailure,
            openVisualDestinationAsync,
            cancellationToken);
    }

    internal async Task<DocumentProcessingResult>
        ProcessPreparedEvidencePortableAsync(
            PreparedDocumentSource prepared,
            DocumentFormatId selectedFormat,
            NativeDocumentEvidence evidence,
            Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
                openVisualDestinationAsync,
            CancellationToken cancellationToken = default)
    {
        var ingestionResult =
            await ProcessPreparedEvidenceAsync(
                    prepared,
                    selectedFormat,
                    evidence,
                    openVisualDestinationAsync,
                    cancellationToken)
                .ConfigureAwait(false);

        return DocumentProcessingResultProjector
            .Project(
                ingestionResult);
    }


    private async Task<DocumentIngestionResult>
        ProcessPreparedEvidenceCoreAsync(
            PreparedDocumentSource prepared,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            IReadOnlyList<PageVisualRasterObservations>?
                precomputedRasterObservations,
            RasterObservationAcquisitionFailure?
                rasterObservationFailure,
            Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
                openVisualDestinationAsync,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            prepared);

        ArgumentNullException.ThrowIfNull(
            extraction);

        cancellationToken.ThrowIfCancellationRequested();

        ValidateExtraction(
            format,
            extraction);

        var preflight =
            _preflightAnalyzer
                .Analyze(
                    extraction);

        ValidatePreflight(
            extraction,
            preflight);

        var decisions =
            _pageProcessingPlanner
                .Plan(
                    extraction);

        ValidatePageDecisions(
            extraction,
            preflight,
            decisions);

        IReadOnlyList<GuardedPagePlanningDecision>?
            authoritativeVisualPlanning =
                null;

        IReadOnlyList<PageVisualRasterObservations>?
            authoritativeVisualRasterObservations =
                null;

        if (_authoritativeVisualPlanningRunner is not null &&
            RequiresAuthoritativeVisualPlanning(
                extraction,
                decisions))
        {
            prepared.ResetForRead();

            try
            {
                var authoritativeVisualPlanningResult =
                    await _authoritativeVisualPlanningRunner
                        .RunAsync(
                            prepared.Source,
                            format,
                            extraction,
                            cancellationToken)
                        .ConfigureAwait(false);

                authoritativeVisualPlanning =
                    authoritativeVisualPlanningResult.Decisions;

                authoritativeVisualRasterObservations =
                    authoritativeVisualPlanningResult.RasterObservations;
            }
            finally
            {
                // Authoritative source-visual evidence acquisition must not leak
                // stream position into Dual Run or page execution.
                prepared.ResetForRead();
            }

            ValidateAuthoritativeVisualPlanning(
                decisions,
                authoritativeVisualPlanning);
        }

        var requiresAuthoritativeHybridExecution =
            decisions.Any(
                decision =>
                    decision.Plan.Route !=
                    PageProcessingRoute.NativeOnly);

        var requiresHealthyNativeVisualExecution =
            authoritativeVisualPlanning is not null &&
            decisions
                .Select(
                    (decision, index) =>
                        ShouldExecuteHealthyNativeVisual(
                            decision,
                            authoritativeVisualPlanning[index]))
                .Any(
                    selected =>
                        selected);

        var requiresHybridExecution =
            requiresAuthoritativeHybridExecution ||
            requiresHealthyNativeVisualExecution;

        var hybridExecution =
            ResolveHybridExecution(
                format,
                decisions,
                requiresHybridExecution);

        DocumentDualRunPlanningReport? dualRunPlanningReport =
            null;

        if (_dualRunPlanningRunner is not null)
        {
            prepared.ResetForRead();

            try
            {
                dualRunPlanningReport =
                    await _dualRunPlanningRunner
                    .RunAsync(
                        prepared.Source,
                        format,
                        extraction,
                        decisions,
                        prepared.Sha256,
                        precomputedRasterObservations:
                            precomputedRasterObservations,
                        rasterObservationFailure:
                            rasterObservationFailure,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                // Dual Run evidence acquisition must not leak source-position
                // state into the current authoritative execution path.
                prepared.ResetForRead();
            }
        }

        var assembledPages =
            new List<HybridDocumentPage>(
                extraction.Pages.Count);

        IDocumentRasterizationSession? rasterSession =
            null;

        AuthoritativeLayoutSpool? layoutSpool =
            null;

        ProcessingComponentIdentity? rasterizationIdentity =
            null;

        try
        {
            if (requiresHybridExecution)
            {
                prepared.ResetForRead();

                rasterSession =
                    await hybridExecution!
                        .DocumentRasterizer
                        .OpenAsync(
                            prepared.Source,
                            format,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (rasterSession is null)
                {
                    throw new InvalidDataException(
                        "Configured document rasterizer returned no rasterization session.");
                }

                rasterizationIdentity =
                    new ProcessingComponentIdentity(
                        rasterSession.BackendId,
                        rasterSession.ProfileId);

                layoutSpool =
                    AuthoritativeLayoutSpool.Create();

                for (var index = 0;
                     index <
                     extraction.Pages.Count;
                     index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page =
                        extraction.Pages[index];

                    var decision =
                        decisions[index];

                    var authoritativeVisualDecision =
                        authoritativeVisualPlanning is null
                            ? null
                            : authoritativeVisualPlanning[index];

                    if (!RequiresPreparedLayout(
                            decision,
                            authoritativeVisualDecision))
                    {
                        continue;
                    }

                    var preparedLayout =
                        await PrepareAuthoritativeLayoutPageAsync(
                                page,
                                decision,
                                authoritativeVisualDecision,
                                rasterSession,
                                hybridExecution,
                                prepared.Sha256,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await layoutSpool
                        .WriteAsync(
                            preparedLayout.PageRaster,
                            preparedLayout.Layout,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            for (var index = 0;
                 index <
                 extraction.Pages.Count;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page =
                    extraction.Pages[index];

                var decision =
                    decisions[index];

                assembledPages.Add(
                    await ExecutePageWithPreparedLayoutAsync(
                            page,
                            decision,
                            authoritativeVisualPlanning is null
                                ? null
                                : authoritativeVisualPlanning[index],
                            authoritativeVisualRasterObservations is null
                                ? null
                                : authoritativeVisualRasterObservations[index],
                            rasterSession,
                            hybridExecution,
                            layoutSpool,
                            prepared.Sha256,
                            openVisualDestinationAsync,
                            cancellationToken)
                        .ConfigureAwait(false));
            }
        }
        finally
        {
            try
            {
                if (layoutSpool is not null)
                {
                    await layoutSpool
                        .DisposeAsync()
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                if (rasterSession is not null)
                {
                    await rasterSession
                        .DisposeAsync()
                        .ConfigureAwait(false);
                }
            }
        }

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    assembledPages);

        var normalization =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly,
                    cancellationToken);

        var segmentation =
            new HybridDocumentSegmenter()
                .Segment(
                    normalization,
                    cancellationToken);

        var hasReconciliationEvidence =
            assembledPages
                .SelectMany(
                    page =>
                        page.Elements)
                .Any(
                    element =>
                        element.Reconciliation is not null);

        var provenanceContext =
            new DocumentProcessingProvenanceContext(
                new DocumentSourceIdentity(
                    format,
                    prepared.Sha256,
                    prepared.ByteLength,
                    extraction.Pages.Count,
                    prepared.Source.FileName,
                    prepared.Source.DeclaredMediaType),
                _engineVersion,
                _nativeExtractionIdentity,
                rasterization:
                    requiresHybridExecution
                        ? rasterizationIdentity
                        : null,
                layoutAnalysis:
                    requiresHybridExecution
                        ? hybridExecution!
                            .LayoutAnalysisIdentity
                        : null,
                reconciliation:
                    hasReconciliationEvidence
                        ? hybridExecution!
                            .ReconciliationIdentity
                        : null);

        var authoritativeResult =
            DocumentIngestionResultBuilder
                .Build(
                    segmentation,
                    provenanceContext);

        DocumentDualRunCandidateTextExecutionReport?
            dualRunCandidateTextExecutionReport =
                null;

        if (_dualRunCandidateTextExecutionRunner is not null)
        {
            if (dualRunPlanningReport is null)
            {
                throw new InvalidOperationException(
                    "Dual Run candidate execution was configured without a Dual Run planning report.");
            }

            if (_dualRunCandidateTextExecutionRunner
                .CanExecuteOcrBackedText)
            {
                prepared.ResetForRead();

                try
                {
                    dualRunCandidateTextExecutionReport =
                        await _dualRunCandidateTextExecutionRunner
                            .RunAsync(
                            prepared.Source,
                            format,
                            extraction,
                            assembledPages,
                            dualRunPlanningReport,
                            prepared.Sha256,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    // Dual Run candidate source access must not leak stream
                    // position into caller-visible source custody.
                    prepared.ResetForRead();
                }
            }
            else
            {
                dualRunCandidateTextExecutionReport =
                    await _dualRunCandidateTextExecutionRunner
                        .RunAsync(
                        extraction,
                        assembledPages,
                        dualRunPlanningReport,
                        prepared.Sha256,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return authoritativeResult;
    }

    #endregion

    #region Methods Page Planning and Route Execution

    private bool RequiresAuthoritativeVisualPlanning(
        DocumentExtractionResult extraction,
        IReadOnlyList<PageProcessingDecision> decisions)
    {
        if (_authoritativeVisualPlanningRunner is null)
        {
            return false;
        }

        if (decisions.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                "Authoritative visual planning selection requires aligned page decisions.");
        }

        for (var index = 0;
             index <
             decisions.Count;
             index++)
        {
            var decision =
                decisions[index];

            var page =
                extraction.Pages[index];

            if (page.RasterImageCount >
                    0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateAuthoritativeVisualPlanning(
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions,
        IReadOnlyList<GuardedPagePlanningDecision> guardedDecisions)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeDecisions);

        ArgumentNullException.ThrowIfNull(
            guardedDecisions);

        if (guardedDecisions.Count !=
            authoritativeDecisions.Count)
        {
            throw new InvalidDataException(
                $"Authoritative visual planning returned {guardedDecisions.Count} " +
                $"decision(s) for {authoritativeDecisions.Count} authoritative " +
                "page decision(s).");
        }

        for (var index = 0;
             index <
             authoritativeDecisions.Count;
             index++)
        {
            var authoritative =
                authoritativeDecisions[index];

            var guarded =
                guardedDecisions[index];

            if (guarded.PhysicalPageNumber !=
                    authoritative.PhysicalPageNumber ||
                guarded.Authoritative.Assessment.NativeTextStatus !=
                    authoritative.Assessment.NativeTextStatus ||
                guarded.Authoritative.Plan.Route !=
                    authoritative.Plan.Route)
            {
                throw new InvalidDataException(
                    $"Authoritative visual planning decision at index {index} " +
                    "does not agree with the already-selected authoritative authoritative route.");
            }
        }
    }

    private static bool ShouldExecuteHealthyNativeVisual(
        PageProcessingDecision authoritativeDecision,
        GuardedPagePlanningDecision? guardedDecision)
    {
        if (guardedDecision is null)
        {
            return false;
        }

        var candidate =
            guardedDecision.Candidate.Plan;

        return authoritativeDecision.Plan.Route ==
                   PageProcessingRoute.NativeOnly &&
               authoritativeDecision.Assessment.NativeTextStatus ==
                   NativeTextStatus.Healthy &&
               candidate.TextMode ==
                   TextExecutionMode.NativeText &&
               !candidate.RequiresTargetedOcr &&
               (
                   candidate.RequiresVisualAnalysis ||
                   candidate.RequiresVisualPreservation
               );
    }

    private DocumentHybridExecutionDependencies? ResolveHybridExecution(
        DocumentFormatId format,
        IReadOnlyList<PageProcessingDecision> decisions,
        bool requiresHybridExecution)
    {
        if (!requiresHybridExecution)
        {
            return _hybridExecution;
        }

        if (_hybridExecution is null)
        {
            var firstHybrid =
                decisions.First(
                    decision =>
                        decision.Plan.Route !=
                        PageProcessingRoute.NativeOnly);

            throw new NotSupportedException(
                $"Physical page {firstHybrid.PhysicalPageNumber} selected route " +
                $"'{firstHybrid.Plan.Route}' for native status " +
                $"'{firstHybrid.Assessment.NativeTextStatus}', but this " +
                "DocumentProcessor was constructed without hybrid execution dependencies.");
        }

        if (!_hybridExecution
                .DocumentRasterizer
                .CanRasterize(
                    format))
        {
            throw new NotSupportedException(
                $"The configured document rasterizer cannot process format '{format}'.");
        }

        return _hybridExecution;
    }

    private static bool RequiresPreparedLayout(
        PageProcessingDecision decision,
        GuardedPagePlanningDecision? authoritativeVisualDecision) =>
        decision.Plan.Route !=
            PageProcessingRoute.NativeOnly ||
        ShouldExecuteHealthyNativeVisual(
            decision,
            authoritativeVisualDecision);

    private static async ValueTask<AuthoritativePreparedLayoutPage>
        PrepareAuthoritativeLayoutPageAsync(
            DocumentExtractionPage page,
            PageProcessingDecision decision,
            GuardedPagePlanningDecision? authoritativeVisualDecision,
            IDocumentRasterizationSession? rasterSession,
            DocumentHybridExecutionDependencies? hybridExecution,
            string sourceDocumentSha256,
            CancellationToken cancellationToken)
    {
        if (decision.PhysicalPageNumber !=
            page.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                $"Page decision {decision.PhysicalPageNumber} does not match " +
                $"extraction page {page.PhysicalPageNumber}.");
        }

        var resolved =
            RequireHybridExecution(
                hybridExecution,
                rasterSession);

        (RasterRenderResult PageRaster, LayoutAnalysisResult Layout) prepared =
            decision.Plan.Route switch
            {
                PageProcessingRoute.NativeOnly
                    when ShouldExecuteHealthyNativeVisual(
                        decision,
                        authoritativeVisualDecision) =>
                    await RequireHealthyNativeVisualExecutor(
                            hybridExecution,
                            rasterSession)
                        .PrepareLayoutAsync(
                            page,
                            decision,
                            authoritativeVisualDecision!
                                .Candidate
                                .Plan,
                            rasterSession!,
                            sourceDocumentSha256,
                            cancellationToken)
                        .ConfigureAwait(false),

                PageProcessingRoute.LayoutWithTargetedOcrRecovery =>
                    await resolved
                        .MissingNativeExecutor
                        .PrepareLayoutAsync(
                            page,
                            decision,
                            rasterSession!,
                            sourceDocumentSha256,
                            cancellationToken)
                        .ConfigureAwait(false),

                PageProcessingRoute.LayoutWithTargetedOcrReconciliation =>
                    await resolved
                        .NativePresentExecutor
                        .PrepareLayoutAsync(
                            page,
                            decision,
                            rasterSession!,
                            sourceDocumentSha256,
                            cancellationToken)
                        .ConfigureAwait(false),

                PageProcessingRoute.NativeOnly =>
                    throw new InvalidOperationException(
                        $"Physical page {page.PhysicalPageNumber} does not require prepared layout execution."),

                _ =>
                    throw new InvalidOperationException(
                        $"Unsupported page-processing route '{decision.Plan.Route}'.")
            };

        return new AuthoritativePreparedLayoutPage(
            prepared.PageRaster,
            prepared.Layout);
    }

    private static async ValueTask<HybridDocumentPage>
        ExecutePageWithPreparedLayoutAsync(
            DocumentExtractionPage page,
            PageProcessingDecision decision,
            GuardedPagePlanningDecision? authoritativeVisualDecision,
            PageVisualRasterObservations?
                authoritativeVisualRasterObservations,
            IDocumentRasterizationSession? rasterSession,
            DocumentHybridExecutionDependencies? hybridExecution,
            AuthoritativeLayoutSpool? layoutSpool,
            string sourceDocumentSha256,
            Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
                openVisualDestinationAsync,
            CancellationToken cancellationToken)
    {
        if (decision.PhysicalPageNumber !=
            page.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                $"Page decision {decision.PhysicalPageNumber} does not match " +
                $"extraction page {page.PhysicalPageNumber}.");
        }

        AuthoritativePreparedLayoutPage? preparedLayout =
            null;

        if (RequiresPreparedLayout(
                decision,
                authoritativeVisualDecision))
        {
            if (layoutSpool is null)
            {
                throw new InvalidOperationException(
                    "Authoritative prepared-layout execution requires a layout spool.");
            }

            preparedLayout =
                await layoutSpool
                    .ReadAsync(
                        page.PhysicalPageNumber,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        return decision.Plan.Route switch
        {
            PageProcessingRoute.NativeOnly
                when ShouldExecuteHealthyNativeVisual(
                    decision,
                    authoritativeVisualDecision) =>
                await RequireHealthyNativeVisualExecutor(
                        hybridExecution,
                        rasterSession)
                    .ExecuteWithPrecomputedLayoutAsync(
                        page,
                        decision,
                        authoritativeVisualDecision!
                            .Candidate
                            .Plan,
                        authoritativeVisualRasterObservations?
                            .VisualElements ??
                        [],
                        rasterSession!,
                        preparedLayout!.PageRaster,
                        preparedLayout!.Layout,
                        sourceDocumentSha256,
                        openVisualDestinationAsync,
                        cancellationToken)
                    .ConfigureAwait(false),

            PageProcessingRoute.NativeOnly =>
                NativeHybridPageAssembler
                    .Assemble(
                        page),

            PageProcessingRoute.LayoutWithTargetedOcrRecovery =>
                await RequireHybridExecution(
                        hybridExecution,
                        rasterSession)
                    .MissingNativeExecutor
                    .ExecuteWithPrecomputedLayoutAsync(
                        page,
                        decision,
                        authoritativeVisualDecision?
                            .Candidate
                            .Plan,
                        authoritativeVisualRasterObservations?
                            .VisualElements ??
                        [],
                        rasterSession!,
                        preparedLayout!.PageRaster,
                        preparedLayout!.Layout,
                        sourceDocumentSha256,
                        openVisualDestinationAsync,
                        cancellationToken)
                    .ConfigureAwait(false),

            PageProcessingRoute.LayoutWithTargetedOcrReconciliation =>
                await RequireHybridExecution(
                        hybridExecution,
                        rasterSession)
                    .NativePresentExecutor
                    .ExecuteWithPrecomputedLayoutAsync(
                        page,
                        decision,
                        authoritativeVisualDecision?
                            .Candidate
                            .Plan,
                        authoritativeVisualRasterObservations?
                            .VisualElements ??
                        [],
                        rasterSession!,
                        preparedLayout!.PageRaster,
                        preparedLayout!.Layout,
                        sourceDocumentSha256,
                        openVisualDestinationAsync,
                        cancellationToken)
                    .ConfigureAwait(false),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported page-processing route '{decision.Plan.Route}'.")
        };
    }

    private static HealthyNativeVisualPageExecutor
        RequireHealthyNativeVisualExecutor(
            DocumentHybridExecutionDependencies? hybridExecution,
            IDocumentRasterizationSession? rasterSession)
    {
        var resolved =
            RequireHybridExecution(
                hybridExecution,
                rasterSession);

        return resolved.HealthyNativeVisualExecutor ??
               throw new InvalidOperationException(
                   "Healthy native visual execution was selected without a " +
                   "configured HealthyNativeVisualPageExecutor.");
    }

    private static DocumentHybridExecutionDependencies RequireHybridExecution(
        DocumentHybridExecutionDependencies? hybridExecution,
        IDocumentRasterizationSession? rasterSession)
    {
        if (hybridExecution is null ||
            rasterSession is null)
        {
            throw new InvalidOperationException(
                "Hybrid page route reached execution without a configured document-scoped raster runtime.");
        }

        return hybridExecution;
    }

    private static void ValidatePageDecisions(
        DocumentExtractionResult extraction,
        DocumentPreflightResult preflight,
        IReadOnlyList<PageProcessingDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(
            decisions);

        if (decisions.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Page planner returned {decisions.Count} decisions for " +
                $"{extraction.Pages.Count} extracted pages.");
        }

        for (var index = 0;
             index <
             decisions.Count;
             index++)
        {
            var page =
                extraction.Pages[index];

            var decision =
                decisions[index];

            if (decision.PhysicalPageNumber !=
                page.PhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Page planner returned physical page " +
                    $"{decision.PhysicalPageNumber} at index {index}; " +
                    $"expected page {page.PhysicalPageNumber}.");
            }
        }

        if (preflight.Classification !=
                DocumentPreflightClassification.HealthyBornDigital &&
            decisions.All(
                decision =>
                    decision.Plan.Route ==
                    PageProcessingRoute.NativeOnly))
        {
            throw new InvalidDataException(
                $"Document preflight classification '{preflight.Classification}' " +
                "conflicts with page-level routing because every page selected NativeOnly.");
        }
    }

    #endregion

    #region Methods Validation

    private static void ValidateExtraction(
        DocumentFormatId selectedFormat,
        DocumentExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        if (extraction.Format !=
            selectedFormat)
        {
            throw new InvalidDataException(
                $"Native extraction format '{extraction.Format}' does not match selected format '{selectedFormat}'.");
        }

        if (extraction.Pages.Count ==
            0)
        {
            throw new InvalidDataException(
                "Native extraction returned no physical pages.");
        }

        for (var index = 0;
             index <
             extraction.Pages.Count;
             index++)
        {
            var expectedPhysicalPageNumber =
                index +
                1;

            var actualPhysicalPageNumber =
                extraction.Pages[index]
                    .PhysicalPageNumber;

            if (actualPhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Native extraction page sequence must be contiguous and one-based. " +
                    $"Expected physical page {expectedPhysicalPageNumber}, observed {actualPhysicalPageNumber}.");
            }
        }
    }

    private static void ValidatePreflight(
        DocumentExtractionResult extraction,
        DocumentPreflightResult preflight)
    {
        ArgumentNullException.ThrowIfNull(
            preflight);

        if (preflight.Format !=
            extraction.Format)
        {
            throw new InvalidDataException(
                $"Preflight format '{preflight.Format}' does not match extraction format '{extraction.Format}'.");
        }

        if (preflight.PageCount !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Preflight page count {preflight.PageCount} does not match extraction page count {extraction.Pages.Count}.");
        }
    }

    #endregion

}
