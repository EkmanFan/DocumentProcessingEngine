using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.DualRun.Dispatch;
using DocumentProcessing.Engine.DualRun.InProcess;
using DocumentProcessing.Engine.DualRun.Isolation;
using DocumentProcessing.Engine.DualRun.Supervision;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.DualRun.Supervision;

public sealed class DocumentDualRunWorkerPlanningOnlyParityTests
{
    #region Variables and Constants

    private const string SelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string ProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private const long TestFileBoundary =
        16L *
        1024L *
        1024L;

    #endregion

    #region Methods PlanningOnly Parity

    [Fact]
    public async Task RunAsync_PlanningOnly_MatchesCurrentInProcessPlanningForGeneratedTwoPagePdf()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var pdfBytes =
            CreateTwoPagePlanningFixture();

        var sourceSha256 =
            Sha256(
                pdfBytes);

        var inProcess =
            await RunCurrentInProcessPlanningAsync(
                pdfBytes,
                sourceSha256);

        Assert.Equal(
            DocumentDualRunPlanningStatus.Completed,
            inProcess.Report.Status);

        Assert.Equal(
            2,
            inProcess.Report.Pages.Count);

        Assert.Equal(
            NativeTextStatus.Healthy,
            inProcess.AuthoritativeDecisions[0]
                .Assessment
                .NativeTextStatus);

        Assert.Equal(
            NativeTextStatus.Missing,
            inProcess.AuthoritativeDecisions[1]
                .Assessment
                .NativeTextStatus);

        var baselines =
            BuildBaselines(
                inProcess.AuthoritativeDecisions);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                pdfBytes,
                sourceSha256,
                DocumentDualRunExecutionMode.PlanningOnly,
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
            inProcess.Report.Pages.Count,
            result.WorkerResult.Pages.Count);

        for (var index = 0;
             index <
             inProcess.Report.Pages.Count;
             index++)
        {
            var expected =
                inProcess.Report.Pages[index];

            var actual =
                result.WorkerResult.Pages[index];

            Assert.Equal(
                expected.PhysicalPageNumber,
                actual.PhysicalPageNumber);

            Assert.Equal(
                expected.AuthoritativePlanningAgreement,
                actual.AuthoritativePlanningAgreement);

            Assert.Equal(
                expected.DualRun.Candidate.Plan.TextMode,
                actual.CandidateTextMode);

            Assert.Equal(
                expected.CandidateRemovesAuthoritativeTextMl,
                actual.CandidateRemovesAuthoritativeTextMl);

            Assert.Equal(
                expected.DualRun.Candidate.Plan.RequiresVisualAnalysis,
                actual.CandidateRequiresVisualAnalysis);

            Assert.Equal(
                expected.DualRun.Candidate.Plan.RequiresMeaningfulVisualPreservation,
                actual.CandidateRequiresMeaningfulVisualPreservation);

            Assert.Null(
                actual.CandidateExecutionStatus);

            Assert.Empty(
                actual.CandidateVisualEvidence);
        }

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task RunAsync_PlanningOnly_ComparesAgainstTransportedAuthoritativeBaseline()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var pdfBytes =
            CreateTwoPagePlanningFixture();

        var sourceSha256 =
            Sha256(
                pdfBytes);

        var inProcess =
            await RunCurrentInProcessPlanningAsync(
                pdfBytes,
                sourceSha256);

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            inProcess.AuthoritativeDecisions[0]
                .Plan
                .Route);

        var baselines =
            BuildBaselines(
                inProcess.AuthoritativeDecisions)
                .ToArray();

        var first =
            baselines[0];

        baselines[0] =
            new DocumentDualRunAuthoritativePageBaseline(
                first.PhysicalPageNumber,
                first.NativeTextStatus,
                PageProcessingRoute.LayoutWithTargetedOcrRecovery,
                first.SelectedTextSequenceSha256,
                first.TextProjectionSha256,
                first.AuthoritativeTextElementCount,
                first.AuthoritativeReconciliationEvidenceCount);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                pdfBytes,
                sourceSha256,
                DocumentDualRunExecutionMode.PlanningOnly,
                baselines);

        var result =
            await Supervisor()
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.ResultReceived,
            result.Outcome);

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Completed,
            result.WorkerResult?.Status);

        var firstPage =
            Assert.Single(
                result.WorkerResult!.Pages,
                page =>
                    page.PhysicalPageNumber ==
                    1);

        Assert.False(
            firstPage.AuthoritativePlanningAgreement);

        Assert.Equal(
            inProcess.Report.Pages[0]
                .DualRun
                .Candidate
                .Plan
                .TextMode,
            firstPage.CandidateTextMode);

        Assert.True(
            firstPage.CandidateRemovesAuthoritativeTextMl);
    }

    #endregion

    #region Methods Full Mode Guard

    [Fact]
    public async Task RunAsync_Full_RemainsExplicitlyUnimplemented()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var pdfBytes =
            CreateTwoPagePlanningFixture();

        var sourceSha256 =
            Sha256(
                pdfBytes);

        var inProcess =
            await RunCurrentInProcessPlanningAsync(
                pdfBytes,
                sourceSha256);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                pdfBytes,
                sourceSha256,
                DocumentDualRunExecutionMode.Full,
                BuildBaselines(
                    inProcess.AuthoritativeDecisions));

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
            "FullExecutionNotImplemented",
            result.WorkerResult?.Failure?.ExceptionType);

        Assert.Empty(
            result.WorkerResult!.Pages);
    }

    #endregion

    #region Methods Current In-Process Baseline

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
                "planning-parity.pdf",
                "application/pdf");

        var extraction =
            await new PdfPigDocumentExtractor()
                .ExtractAsync(
                    source,
                    DocumentFormatId.Pdf);

        var assessor =
            new DefaultPageProcessingAssessor();

        var policy =
            new DefaultPageProcessingPolicy();

        var authoritativeDecisions =
            extraction
                .Pages
                .Select(
                    page =>
                    {
                        var assessment =
                            assessor.Assess(
                                page);

                        return new PageProcessingDecision(
                            assessment,
                            policy.Decide(
                                assessment));
                    })
                .ToArray();

        var runner =
            new DocumentDualRunPlanningRunner(
                new DocumentDualRunPlanningDependencies(
                    new PdfPigVisualRasterObservationSource(),
                    NoOpPlanningObserver.Instance));

        var report =
            await runner
                .RunAsync(
                    source,
                    DocumentFormatId.Pdf,
                    extraction,
                    authoritativeDecisions,
                    sourceSha256);

        return new InProcessPlanningEvidence(
            authoritativeDecisions,
            report);
    }

    #endregion

    #region Methods Job Preparation

    private static IReadOnlyList<DocumentDualRunAuthoritativePageBaseline> BuildBaselines(
        IReadOnlyList<PageProcessingDecision> authoritativeDecisions) =>
        authoritativeDecisions
            .Select(
                decision =>
                    new DocumentDualRunAuthoritativePageBaseline(
                        decision.PhysicalPageNumber,
                        decision.Assessment.NativeTextStatus,
                        decision.Plan.Route,
                        SelectedSha,
                        ProjectionSha,
                        authoritativeTextElementCount:
                            0,
                        authoritativeReconciliationEvidenceCount:
                            0))
            .ToArray();

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
                "planning-parity.pdf",
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

    private static byte[] CreateTwoPagePlanningFixture()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var textPage =
            builder.AddPage(
                PageSize.A4);

        textPage.AddText(
            "Deterministic native text for Dual Run PlanningOnly parity.",
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

    private sealed class TemporaryDirectoryScope
        : IDisposable
    {
        #region ctor

        public TemporaryDirectoryScope()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"dpe-dual-run-planning-parity-test-{Guid.NewGuid():N}");
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
