using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.UnitTests.DualRun.Transport;

public sealed class DocumentDualRunTransportJsonTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string SelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string ProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private const string VisualSha =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    #endregion

    #region Methods Request JSON

    [Fact]
    public void RequestJson_RoundTrips_StrictVersionedContract()
    {
        var request =
            Request(
                DocumentDualRunExecutionMode.Full);

        var json =
            DocumentDualRunTransportJson
                .SerializeRequestToUtf8Bytes(
                    request);

        var text =
            Encoding.UTF8.GetString(
                json);

        Assert.Contains(
            "\"schemaVersion\":\"document-dual-run-request-v1\"",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"executionMode\":\"Full\"",
            text,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "\"Content\"",
            text,
            StringComparison.Ordinal);

        var roundTrip =
            DocumentDualRunTransportJson
                .DeserializeRequest(
                    json);

        Assert.Equal(
            request.JobId,
            roundTrip.JobId);

        Assert.Equal(
            request.ExecutionMode,
            roundTrip.ExecutionMode);

        Assert.Equal(
            request.EngineVersion,
            roundTrip.EngineVersion);

        Assert.Equal(
            request.SourceSnapshotPath,
            roundTrip.SourceSnapshotPath);

        Assert.Equal(
            request.SourceDocumentSha256,
            roundTrip.SourceDocumentSha256);

        Assert.Equal(
            request.SourceByteLength,
            roundTrip.SourceByteLength);

        Assert.Equal(
            request.Format,
            roundTrip.Format);

        var page =
            Assert.Single(
                roundTrip.AuthoritativePages);

        Assert.Equal(
            NativeTextStatus.Healthy,
            page.NativeTextStatus);

        Assert.Equal(
            PageProcessingRoute.NativeOnly,
            page.AuthoritativeRoute);

        Assert.Equal(
            SelectedSha,
            page.SelectedTextSequenceSha256);

        Assert.Equal(
            ProjectionSha,
            page.TextProjectionSha256);
    }

    [Fact]
    public void RequestJson_UnknownSchema_IsRejectedExplicitly()
    {
        var json =
            Encoding.UTF8.GetString(
                DocumentDualRunTransportJson
                    .SerializeRequestToUtf8Bytes(
                        Request(
                            DocumentDualRunExecutionMode.PlanningOnly)))
                .Replace(
                    DocumentDualRunTransportSchema.RequestV1,
                    "document-dual-run-request-v999",
                    StringComparison.Ordinal);

        Assert.Throws<
            UnsupportedDocumentDualRunTransportSchemaException>(
                () =>
                    DocumentDualRunTransportJson
                        .DeserializeRequest(
                            Encoding.UTF8.GetBytes(
                                json)));
    }

    [Fact]
    public void RequestJson_UnknownProperty_FailsClosed()
    {
        var json =
            Encoding.UTF8.GetString(
                DocumentDualRunTransportJson
                    .SerializeRequestToUtf8Bytes(
                        Request(
                            DocumentDualRunExecutionMode.Full)));

        var modified =
            json.Insert(
                1,
                "\"unexpected\":true,");

        Assert.Throws<JsonException>(
            () =>
                DocumentDualRunTransportJson
                    .DeserializeRequest(
                        Encoding.UTF8.GetBytes(
                            modified)));
    }

    [Fact]
    public void RequestJson_NumericEnumAlias_FailsClosed()
    {
        var json =
            Encoding.UTF8.GetString(
                DocumentDualRunTransportJson
                    .SerializeRequestToUtf8Bytes(
                        Request(
                            DocumentDualRunExecutionMode.Full)));

        var modified =
            json.Replace(
                "\"executionMode\":\"Full\"",
                "\"executionMode\":\"1\"",
                StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () =>
                DocumentDualRunTransportJson
                    .DeserializeRequest(
                        Encoding.UTF8.GetBytes(
                            modified)));
    }

    [Fact]
    public void RequestJson_DuplicateProperty_FailsClosed()
    {
        var json =
            Encoding.UTF8.GetString(
                DocumentDualRunTransportJson
                    .SerializeRequestToUtf8Bytes(
                        Request(
                            DocumentDualRunExecutionMode.Full)));

        var modified =
            json.Replace(
                "\"jobId\":",
                "\"jobId\":\"11111111-1111-1111-1111-111111111111\",\"jobId\":",
                StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () =>
                DocumentDualRunTransportJson
                    .DeserializeRequest(
                        Encoding.UTF8.GetBytes(
                            modified)));
    }

    [Fact]
    public void Request_SourceSnapshotMustUseFixedSourceBinName()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentDualRunWorkerRequest(
                    Guid.NewGuid(),
                    DocumentDualRunExecutionMode.Full,
                    "test-engine-v1",
                    Path.Combine(
                        Path.GetTempPath(),
                        "wrong-name.pdf"),
                    SourceSha,
                    123,
                    DocumentFormatId.Pdf,
                    [
                        Baseline()
                    ]));
    }

    #endregion

    #region Methods Result JSON

    [Fact]
    public void ResultJson_CompletedFull_RoundTripsCompactComparisonAndVisualSummary()
    {
        var jobId =
            Guid.NewGuid();

        var result =
            new DocumentDualRunWorkerResult(
                jobId,
                DocumentDualRunExecutionMode.Full,
                "worker-engine-v1",
                SourceSha,
                DocumentDualRunWorkerResultStatus.Completed,
                [
                    new DocumentDualRunWorkerPageResult(
                        1,
                        authoritativePlanningAgreement:
                            true,
                        TextExecutionMode.TargetedOcrRecovery,
                        candidateRemovesAuthoritativeTextMl:
                            false,
                        candidateRequiresVisualAnalysis:
                            true,
                        candidateRequiresMeaningfulVisualPreservation:
                            false,
                        DocumentDualRunCandidateTextPageStatus
                            .ExecutedTargetedOcrRecovery,
                        selectedTextSequenceExact:
                            true,
                        textProjectionExact:
                            true,
                        authoritativeTextElementCount:
                            1,
                        candidateTextElementCount:
                            1,
                        authoritativeReconciliationEvidenceCount:
                            1,
                        candidateReconciliationEvidenceCount:
                            1,
                        candidateVisualEvidence:
                            [
                                new DocumentDualRunWorkerVisualEvidenceSummary(
                                    observationSequence:
                                        4,
                                    readingOrder:
                                        2,
                                    new NormalizedRectangle(
                                        0.1,
                                        0.2,
                                        0.8,
                                        0.9),
                                    VisualEvidenceKind.LargeIndependentVisual,
                                    preservedProfileId:
                                        "visual-profile-v1",
                                    preservedMediaType:
                                        "image/png",
                                    preservedContentLength:
                                        42,
                                    preservedContentSha256:
                                        VisualSha)
                            ])
                ]);

        var json =
            DocumentDualRunTransportJson
                .SerializeResultToUtf8Bytes(
                    result);

        var roundTrip =
            DocumentDualRunTransportJson
                .DeserializeResult(
                    json);

        Assert.Equal(
            jobId,
            roundTrip.JobId);

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Completed,
            roundTrip.Status);

        var page =
            Assert.Single(
                roundTrip.Pages);

        Assert.Equal(
            DocumentDualRunCandidateTextPageStatus
                .ExecutedTargetedOcrRecovery,
            page.CandidateExecutionStatus);

        Assert.True(
            page.SelectedTextSequenceExact);

        Assert.True(
            page.TextProjectionExact);

        Assert.True(
            page.CandidateRequiresVisualAnalysis);

        Assert.False(
            page.CandidateRequiresMeaningfulVisualPreservation);

        Assert.True(
            page.CandidateHasIndependentVisualWork);

        var visual =
            Assert.Single(
                page.CandidateVisualEvidence);

        Assert.Equal(
            4,
            visual.ObservationSequence);

        Assert.Equal(
            VisualEvidenceKind.LargeIndependentVisual,
            visual.EvidenceKind);

        Assert.True(
            visual.IsPreserved);

        Assert.Equal(
            VisualSha,
            visual.PreservedContentSha256);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ResultPage_VisualPlanningAxes_RemainIndependent(
        bool requiresVisualAnalysis,
        bool requiresMeaningfulVisualPreservation)
    {
        var page =
            new DocumentDualRunWorkerPageResult(
                1,
                authoritativePlanningAgreement:
                    true,
                TextExecutionMode.NativeText,
                candidateRemovesAuthoritativeTextMl:
                    false,
                candidateRequiresVisualAnalysis:
                    requiresVisualAnalysis,
                candidateRequiresMeaningfulVisualPreservation:
                    requiresMeaningfulVisualPreservation,
                DocumentDualRunCandidateTextPageStatus.ExecutedNativeText,
                selectedTextSequenceExact:
                    true,
                textProjectionExact:
                    true,
                authoritativeTextElementCount:
                    1,
                candidateTextElementCount:
                    1,
                authoritativeReconciliationEvidenceCount:
                    0,
                candidateReconciliationEvidenceCount:
                    0);

        Assert.Equal(
            requiresVisualAnalysis,
            page.CandidateRequiresVisualAnalysis);

        Assert.Equal(
            requiresMeaningfulVisualPreservation,
            page.CandidateRequiresMeaningfulVisualPreservation);

        Assert.Equal(
            requiresVisualAnalysis ||
            requiresMeaningfulVisualPreservation,
            page.CandidateHasIndependentVisualWork);
    }

    [Fact]
    public void ResultJson_Failed_RoundTripsSanitizedFailureWithoutPages()
    {
        var result =
            new DocumentDualRunWorkerResult(
                Guid.NewGuid(),
                DocumentDualRunExecutionMode.Full,
                "worker-engine-v1",
                SourceSha,
                DocumentDualRunWorkerResultStatus.Failed,
                pages:
                    [],
                new DocumentDualRunWorkerFailure(
                    DocumentDualRunWorkerFailureStage.SourceValidation,
                    "System.IO.InvalidDataException",
                    "Source SHA-256 mismatch."));

        var roundTrip =
            DocumentDualRunTransportJson
                .DeserializeResult(
                    DocumentDualRunTransportJson
                        .SerializeResultToUtf8Bytes(
                            result));

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Failed,
            roundTrip.Status);

        Assert.Empty(
            roundTrip.Pages);

        var failure =
            Assert.IsType<DocumentDualRunWorkerFailure>(
                roundTrip.Failure);

        Assert.Equal(
            DocumentDualRunWorkerFailureStage.SourceValidation,
            failure.Stage);
    }

    [Fact]
    public void Result_PlanningOnly_CannotCarryCandidateExecutionStatus()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentDualRunWorkerResult(
                    Guid.NewGuid(),
                    DocumentDualRunExecutionMode.PlanningOnly,
                    "worker-engine-v1",
                    SourceSha,
                    DocumentDualRunWorkerResultStatus.Completed,
                    [
                        ExecutedNativePage()
                    ]));
    }

    [Fact]
    public void Result_Full_RequiresCandidateExecutionStatusForEveryPage()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new DocumentDualRunWorkerResult(
                    Guid.NewGuid(),
                    DocumentDualRunExecutionMode.Full,
                    "worker-engine-v1",
                    SourceSha,
                    DocumentDualRunWorkerResultStatus.Completed,
                    [
                        new DocumentDualRunWorkerPageResult(
                            1,
                            authoritativePlanningAgreement:
                                true,
                            TextExecutionMode.NativeText,
                            candidateRemovesAuthoritativeTextMl:
                                false,
                            candidateRequiresVisualAnalysis:
                                false,
                            candidateRequiresMeaningfulVisualPreservation:
                                false)
                    ]));
    }

    #endregion

    #region Methods Test Data

    private static DocumentDualRunWorkerRequest Request(
        DocumentDualRunExecutionMode mode) =>
        new(
            Guid.Parse(
                "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            mode,
            "test-engine-v1",
            Path.Combine(
                Path.GetTempPath(),
                "dual-run-test-job",
                DocumentDualRunTransportSchema.SourceSnapshotFileName),
            SourceSha,
            123,
            DocumentFormatId.Pdf,
            [
                Baseline()
            ],
            "sample.pdf",
            "application/pdf");

    private static DocumentDualRunAuthoritativePageBaseline Baseline() =>
        new(
            1,
            NativeTextStatus.Healthy,
            PageProcessingRoute.NativeOnly,
            SelectedSha,
            ProjectionSha,
            authoritativeTextElementCount:
                1,
            authoritativeReconciliationEvidenceCount:
                0);

    private static DocumentDualRunWorkerPageResult ExecutedNativePage() =>
        new(
            1,
            authoritativePlanningAgreement:
                true,
            TextExecutionMode.NativeText,
            candidateRemovesAuthoritativeTextMl:
                false,
            candidateRequiresVisualAnalysis:
                false,
            candidateRequiresMeaningfulVisualPreservation:
                false,
            DocumentDualRunCandidateTextPageStatus.ExecutedNativeText,
            selectedTextSequenceExact:
                true,
            textProjectionExact:
                true,
            authoritativeTextElementCount:
                1,
            candidateTextElementCount:
                1,
            authoritativeReconciliationEvidenceCount:
                0,
            candidateReconciliationEvidenceCount:
                0);

    #endregion
}
