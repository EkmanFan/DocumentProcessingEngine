using DocumentProcessing.Epub;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.DPEngine;
using DocumentProcessing.Manager.Partitioning;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Submissions;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;
using DocumentProcessing.ProviderLifecycle;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class DocumentProcessingSplitPreviewProviderTests
{
    #region Tests

    [Fact]
    public async Task InspectAsync_RealEpubProducesCompleteStructuredProposal()
    {
        var sourcePath =
            Path.Combine(
                FindRepositoryRoot(),
                "tests",
                "document_corpus",
                "epub",
                "habermas-case-for-resurrection.epub");

        if (!File.Exists(
                sourcePath))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "The qualified Habermas EPUB fixture is unavailable.");
        }

        var unitId =
            ProcessingUnitId.New();

        var submissionId =
            DocumentSubmissionId.New();

        var now =
            DateTimeOffset.UtcNow;

        var submission =
            new DocumentSubmission(
                submissionId,
                new SourceArtifact(
                    new Sha256Digest(
                        new string(
                            'a',
                            64)),
                    new FileInfo(
                            sourcePath)
                        .Length),
                Path.GetFileName(
                    sourcePath),
                "application/epub+zip",
                "qualified integration corpus",
                now);

        var queued =
            new ProcessingQueueItemSnapshot(
                new ProcessingWorkItem(
                    unitId,
                    submissionId,
                    new ProcessingUnitScope.WholeDocument(),
                    attemptNumber:
                        1),
                submission.OriginalFileName,
                ProcessingUnitStatus.Pending,
                ProcessingUnitDispatchState.Shelved,
                queuePosition:
                    1,
                resultReference:
                    null,
                lastFailure:
                    null,
                lastInterruptionReason:
                    null,
                now,
                now);

        using var host =
            new global::DocumentProcessing.DocumentProcessingHost(
                new global::DocumentProcessing.DocumentProcessingHostOptions(
                    "split-preview-integration-v1",
                    new PpStructureV3Options(
                        new Uri(
                            "http://127.0.0.1:1/layout-parsing")),
                    new PaddleOcrOptions(
                        new Uri(
                            "http://127.0.0.1:1/ocr"),
                        "split-preview-integration-ocr"),
                    epub:
                        new EpubDocumentFormatOptions(),
                    providerLifecycle:
                        ProcessingProviderLifecycleOptions.External));

        var provider =
            new DocumentProcessingSplitPreviewProvider(
                host,
                new StubQueueReader(
                    new ProcessingQueueSnapshot(
                        1,
                        [queued])),
                new StubSubmissionReader(
                    submission),
                new FileSourceArtifactReader(
                    sourcePath));

        var preview =
            await provider.InspectAsync(
                unitId);

        var axis =
            Assert.IsType<DocumentPartitionAxis.ContentUnits>(
                preview.Axis);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                preview.SuggestedProposal);

        Assert.True(
            preview.SplitSuggested);

        Assert.True(
            proposal.Segments.Count >=
            2);

        var first =
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                proposal.Segments[0].Extent.Start);

        var last =
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                proposal.Segments[^1].Extent.End);

        Assert.Equal(
            0,
            first.ContentUnitIndex);

        Assert.Equal(
            axis.ContentUnitIds.Count -
            1,
            last.ContentUnitIndex);

        Assert.NotEmpty(
            preview.ContentUnitLabels);
    }

    #endregion

    #region Helpers

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

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the integration-test output directory.");
    }

    private sealed class StubQueueReader(
        ProcessingQueueSnapshot snapshot)
        : IProcessingQueueReader
    {
        public ValueTask<ProcessingQueueSnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                snapshot);
        }
    }

    private sealed class StubSubmissionReader(
        DocumentSubmission submission)
        : IDocumentSubmissionReader
    {
        public ValueTask<DocumentSubmission?> GetAsync(
            DocumentSubmissionId submissionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult<DocumentSubmission?>(
                submissionId ==
                submission.SubmissionId
                    ? submission
                    : null);
        }
    }

    private sealed class FileSourceArtifactReader(
        string sourcePath)
        : ISourceArtifactReader
    {
        public ValueTask<bool> VerifyAsync(
            SourceArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                true);
        }

        public ValueTask<Stream> OpenReadAsync(
            SourceArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult<Stream>(
                new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize:
                        128 * 1024,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan));
        }
    }

    #endregion
}
