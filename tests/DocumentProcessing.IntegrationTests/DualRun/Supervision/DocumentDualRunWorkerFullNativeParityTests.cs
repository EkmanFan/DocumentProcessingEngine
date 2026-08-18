using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.DualRun.Dispatch;
using DocumentProcessing.Engine.DualRun.InProcess;
using DocumentProcessing.Engine.DualRun.Isolation;
using DocumentProcessing.Engine.DualRun.Supervision;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.DualRun.Supervision;

public sealed class DocumentDualRunWorkerFullNativeParityTests
{
    #region Variables and Constants

    private const string PlaceholderSelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string PlaceholderProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private const long TestFileBoundary =
        16L *
        1024L *
        1024L;

    #endregion

    #region Methods Native Full Parity

    [Fact]
    public async Task RunAsync_FullAllNative_MatchesCurrentInProcessCandidateExecutionWithoutMlRuntime()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var pdfBytes =
            CreateAllNativeFixture();

        var sourceSha256 =
            Sha256(
                pdfBytes);

        var inProcess =
            await RunCurrentInProcessFullNativeAsync(
                pdfBytes,
                sourceSha256);

        Assert.Equal(
            DocumentDualRunPlanningStatus.Completed,
            inProcess.Planning.Status);

        Assert.Equal(
            DocumentDualRunCandidateTextExecutionStatus.Completed,
            inProcess.Candidate.Status);

        Assert.All(
            inProcess.Planning.Pages,
            page =>
                Assert.Equal(
                    TextExecutionMode.NativeText,
                    page.DualRun.Candidate.Plan.TextMode));

        var baselines =
            inProcess
                .AuthoritativeDecisions
                .Zip(
                    inProcess.AuthoritativePages,
                    DocumentDualRunAuthoritativePageBaseline.From)
                .ToArray();

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                pdfBytes,
                sourceSha256,
                DocumentDualRunExecutionMode.Full,
                baselines);

        var jobDirectory =
            job.JobDirectoryPath;

        var result =
            await Supervisor()
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.ResultReceived,
            result.Outcome);

        Assert.Equal(
            0,
            result.ExitCode);

        Assert.NotNull(
            result.WorkerResult);

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Completed,
            result.WorkerResult!.Status);

        Assert.Null(
            result.WorkerResult.Failure);

        Assert.Equal(
            inProcess.Candidate.Pages.Count,
            result.WorkerResult.Pages.Count);

        for (var index = 0;
             index <
             inProcess.Candidate.Pages.Count;
             index++)
        {
            var expectedPlanning =
                inProcess
                    .Planning
                    .Pages[index];

            var expectedCandidate =
                inProcess
                    .Candidate
                    .Pages[index];

            var actual =
                result
                    .WorkerResult
                    .Pages[index];

            Assert.Equal(
                expectedPlanning.AuthoritativePlanningAgreement,
                actual.AuthoritativePlanningAgreement);

            Assert.Equal(
                expectedCandidate.CandidateTextMode,
                actual.CandidateTextMode);

            Assert.Equal(
                expectedCandidate.Status,
                actual.CandidateExecutionStatus);

            Assert.Equal(
                expectedCandidate.CandidateRemovesAuthoritativeTextMl,
                actual.CandidateRemovesAuthoritativeTextMl);

            Assert.Equal(
                expectedCandidate.SelectedTextSequenceExact,
                actual.SelectedTextSequenceExact);

            Assert.Equal(
                expectedCandidate.TextProjectionExact,
                actual.TextProjectionExact);

            Assert.Equal(
                expectedCandidate.AuthoritativeTextElementCount,
                actual.AuthoritativeTextElementCount);

            Assert.Equal(
                expectedCandidate.CandidateTextElementCount,
                actual.CandidateTextElementCount);

            Assert.Equal(
                expectedCandidate.AuthoritativeReconciliationEvidenceCount,
                actual.AuthoritativeReconciliationEvidenceCount);

            Assert.Equal(
                expectedCandidate.CandidateReconciliationEvidenceCount,
                actual.CandidateReconciliationEvidenceCount);

            Assert.Equal(
                expectedPlanning.DualRun.Candidate.Plan.RequiresVisualAnalysis,
                actual.CandidateRequiresVisualAnalysis);

            Assert.Equal(
                expectedPlanning.DualRun.Candidate.Plan.RequiresMeaningfulVisualPreservation,
                actual.CandidateRequiresMeaningfulVisualPreservation);

            Assert.Empty(
                actual.CandidateVisualEvidence);
        }

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    #endregion

    #region Methods Lazy ML Gate

    [Fact]
    public async Task RunAsync_FullWithOcrBackedCandidate_FailsClosedBeforeMlComposition()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var pdfBytes =
            CreateNativePlusBlankFixture();

        var sourceSha256 =
            Sha256(
                pdfBytes);

        var planning =
            await RunCurrentInProcessPlanningAsync(
                pdfBytes,
                sourceSha256);

        Assert.Contains(
            planning.Report.Pages,
            page =>
                page.DualRun.Candidate.Plan.TextMode !=
                TextExecutionMode.NativeText);

        var baselines =
            BuildPlanningBaselines(
                planning.AuthoritativeDecisions);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                pdfBytes,
                sourceSha256,
                DocumentDualRunExecutionMode.Full,
                baselines);

        var result =
            await Supervisor()
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.ResultReceived,
            result.Outcome);

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Failed,
            result.WorkerResult?.Status);

        Assert.Equal(
            DocumentDualRunWorkerFailureStage.CandidateExecution,
            result.WorkerResult?.Failure?.Stage);

        Assert.Equal(
            "System.InvalidOperationException",
            result.WorkerResult?.Failure?.ExceptionType);

        Assert.Contains(
            "requires lazy OCR-backed runtime",
            result.WorkerResult?.Failure?.Message,
            StringComparison.Ordinal);

        Assert.Empty(
            result.WorkerResult!.Pages);
    }

    #endregion

    #region Methods Current In-Process Evidence

    private static async Task<InProcessFullNativeEvidence> RunCurrentInProcessFullNativeAsync(
        byte[] pdfBytes,
        string sourceSha256)
    {
        await using var sourceStream =
            new MemoryStream(
                pdfBytes,
                writable:
                    false);

        var source =
            new DocumentSource(
                sourceStream,
                "full-native-parity.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        var authoritativeDecisions =
            BuildAuthoritativeDecisions(
                extraction.Pages);

        var authoritativePages =
            extraction
                .Pages
                .Select(
                    AssembleNativePage)
                .ToArray();

        var planning =
            await new DocumentDualRunPlanningRunner(
                    new DocumentDualRunPlanningDependencies(
                        new PdfPigVisualRasterObservationSource(),
                        NoOpPlanningObserver.Instance))
                .RunAsync(
                    source,
                    DocumentFormatId.Pdf,
                    extraction,
                    authoritativeDecisions,
                    sourceSha256);

        var candidate =
            await new DocumentDualRunCandidateTextExecutionRunner(
                    new DocumentDualRunCandidateTextExecutionDependencies(
                        NoOpCandidateObserver.Instance))
                .RunAsync(
                    extraction,
                    authoritativePages,
                    planning,
                    sourceSha256);

        return new InProcessFullNativeEvidence(
            authoritativeDecisions,
            authoritativePages,
            planning,
            candidate);
    }

    private static async Task<InProcessPlanningEvidence> RunCurrentInProcessPlanningAsync(
        byte[] pdfBytes,
        string sourceSha256)
    {
        await using var sourceStream =
            new MemoryStream(
                pdfBytes,
                writable:
                    false);

        var source =
            new DocumentSource(
                sourceStream,
                "full-lazy-gate.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        var authoritativeDecisions =
            BuildAuthoritativeDecisions(
                extraction.Pages);

        var planning =
            await new DocumentDualRunPlanningRunner(
                    new DocumentDualRunPlanningDependencies(
                        new PdfPigVisualRasterObservationSource(),
                        NoOpPlanningObserver.Instance))
                .RunAsync(
                    source,
                    DocumentFormatId.Pdf,
                    extraction,
                    authoritativeDecisions,
                    sourceSha256);

        return new InProcessPlanningEvidence(
            authoritativeDecisions,
            planning);
    }

    private static IReadOnlyList<PageProcessingDecision> BuildAuthoritativeDecisions(
        IReadOnlyList<DocumentProcessing.Core.Extraction.DocumentExtractionPage> pages)
    {
        var assessor =
            new DefaultPageProcessingAssessor();

        var policy =
            new DefaultPageProcessingPolicy();

        return pages
            .Select(
                page =>
                {
                    var assessment =
                        assessor
                            .Assess(
                                page);

                    return new PageProcessingDecision(
                        assessment,
                        policy.Decide(
                            assessment));
                })
            .ToArray();
    }

    private static HybridDocumentPage AssembleNativePage(
        DocumentProcessing.Core.Extraction.DocumentExtractionPage sourcePage)
    {
        var elements =
            sourcePage
                .Blocks
                .Select(
                    block =>
                        HybridDocumentElementFactory
                            .FromNative(
                                sourcePage.PhysicalPageNumber,
                                block))
                .ToArray();

        return HybridDocumentAssembler
            .AssemblePage(
                sourcePage,
                elements);
    }

    private static IReadOnlyList<DocumentDualRunAuthoritativePageBaseline> BuildPlanningBaselines(
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions) =>
        authoritativeDecisions
            .Select(
                decision =>
                    new DocumentDualRunAuthoritativePageBaseline(
                        decision.PhysicalPageNumber,
                        decision.Assessment.NativeTextStatus,
                        decision.Plan.Route,
                        PlaceholderSelectedSha,
                        PlaceholderProjectionSha,
                        authoritativeTextElementCount:
                            0,
                        authoritativeReconciliationEvidenceCount:
                            0))
            .ToArray();

    #endregion

    #region Methods Job Preparation

    private static async Task<DocumentDualRunPreparedJob> CreatePreparedJobAsync(
        string spoolRoot,
        byte[] pdfBytes,
        string sourceSha256,
        DocumentDualRunExecutionMode executionMode,
        IReadOnlyList<DocumentDualRunAuthoritativePageBaseline> authoritativePages)
    {
        await using var source =
            new MemoryStream(
                pdfBytes,
                writable:
                    false);

        var snapshot =
            await new DocumentDualRunSourceSnapshotFactory(
                    spoolRoot)
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    sourceSha256,
                    pdfBytes.LongLength);

        var request =
            new DocumentDualRunWorkerRequest(
                snapshot.JobId,
                executionMode,
                "test-engine-v1",
                snapshot.SourceSnapshotPath,
                snapshot.SourceDocumentSha256,
                snapshot.SourceByteLength,
                DocumentFormatId.Pdf,
                authoritativePages,
                "full-native-parity.pdf",
                "application/pdf");

        try
        {
            return await new DocumentDualRunRequestMaterializer()
                .CreateAsync(
                    snapshot,
                    request);
        }
        catch
        {
            await snapshot
                .DisposeAsync();

            throw;
        }
    }

    private static DocumentDualRunWorkerProcessSupervisor Supervisor() =>
        new(
            new DocumentDualRunWorkerProcessConfiguration(
                WorkerExecutablePath(),
                timeout:
                    TimeSpan.FromSeconds(
                        10),
                terminationGracePeriod:
                    TimeSpan.FromSeconds(
                        2),
                maximumRequestFileBytes:
                    TestFileBoundary,
                maximumResultFileBytes:
                    TestFileBoundary,
                maximumCapturedStandardErrorCharacters:
                    4096));

    #endregion

    #region Methods Fixtures

    private static byte[] CreateAllNativeFixture()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var firstPage =
            builder.AddPage(
                PageSize.A4);

        firstPage.AddText(
            "First deterministic native page.",
            12,
            new PdfPoint(
                72,
                720),
            font);

        var secondPage =
            builder.AddPage(
                PageSize.A4);

        secondPage.AddText(
            "Second deterministic native page.",
            12,
            new PdfPoint(
                72,
                720),
            font);

        return builder.Build();
    }

    private static byte[] CreateNativePlusBlankFixture()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var nativePage =
            builder.AddPage(
                PageSize.A4);

        nativePage.AddText(
            "Native page before OCR-backed page.",
            12,
            new PdfPoint(
                72,
                720),
            font);

        _ =
            builder.AddPage(
                PageSize.A4);

        return builder.Build();
    }

    private static string Sha256(
        byte[] source) =>
        Convert
            .ToHexString(
                SHA256.HashData(
                    source))
            .ToLowerInvariant();

    private static string WorkerExecutablePath()
    {
        var root =
            FindRepositoryRoot();

        var testOutput =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        var configuration =
            testOutput
                .Parent
                ?.Name ??
            throw new InvalidOperationException(
                "Unable to determine test build configuration.");

        var executableName =
            OperatingSystem.IsWindows()
                ? "DocumentProcessing.DualRunWorker.exe"
                : "DocumentProcessing.DualRunWorker";

        var workerPath =
            Path.Combine(
                root,
                "src",
                "DocumentProcessing.DualRunWorker",
                "bin",
                configuration,
                "net10.0",
                executableName);

        Assert.True(
            File.Exists(
                workerPath),
            $"Expected built worker executable at '{workerPath}'.");

        return workerPath;
    }

    private static string FindRepositoryRoot()
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "DocumentProcessingEngine.sln")))
            {
                return current.FullName;
            }

            current =
                current.Parent;
        }

        throw new InvalidOperationException(
            "Unable to locate DocumentProcessingEngine.sln from test output.");
    }

    #endregion

    #region Test Types

    private sealed record InProcessFullNativeEvidence(
        IReadOnlyList<PageProcessingDecision> AuthoritativeDecisions,
        IReadOnlyList<HybridDocumentPage> AuthoritativePages,
        DocumentDualRunPlanningReport Planning,
        DocumentDualRunCandidateTextExecutionReport Candidate);

    private sealed record InProcessPlanningEvidence(
        IReadOnlyList<PageProcessingDecision> AuthoritativeDecisions,
        DocumentDualRunPlanningReport Report);

    private sealed class NoOpPlanningObserver
        : IDocumentDualRunPlanningObserver
    {
        #region Variables and Constants

        public static readonly NoOpPlanningObserver Instance =
            new();

        #endregion

        #region ctor

        private NoOpPlanningObserver()
        {
        }

        #endregion

        #region Methods Observation

        public ValueTask ObserveAsync(
            DocumentDualRunPlanningReport report,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                report);

            cancellationToken
                .ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }

        #endregion
    }

    private sealed class NoOpCandidateObserver
        : IDocumentDualRunCandidateTextExecutionObserver
    {
        #region Variables and Constants

        public static readonly NoOpCandidateObserver Instance =
            new();

        #endregion

        #region ctor

        private NoOpCandidateObserver()
        {
        }

        #endregion

        #region Methods Observation

        public ValueTask ObserveAsync(
            DocumentDualRunCandidateTextExecutionReport report,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                report);

            cancellationToken
                .ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }

        #endregion
    }

    private sealed class TemporaryDirectoryScope
        : IDisposable
    {
        #region ctor

        public TemporaryDirectoryScope()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"dpe-dual-run-full-native-test-{Guid.NewGuid():N}");
        }

        #endregion

        #region Properties

        public string Path { get; }

        #endregion

        #region Methods Lifecycle

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(
                        Path))
                {
                    Directory.Delete(
                        Path,
                        recursive:
                            true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        #endregion
    }

    #endregion
}
