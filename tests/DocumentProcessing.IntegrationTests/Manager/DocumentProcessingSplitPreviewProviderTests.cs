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
using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

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

        Assert.Equal(
            NativeNavigationPartitionStrategy.NativeNavigationStrategyId,
            proposal.StrategyId);

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

    [Fact]
    public async Task InspectAsync_PdfWithoutOutlineFallsBackToStructuralHeadings()
    {
        var sourceBytes =
            CreatePdfWithStructuralHeadings();

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
                            'b',
                            64)),
                    sourceBytes.Length),
                "headings-without-outline.pdf",
                "application/pdf",
                "structural fallback integration fixture",
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
                    "split-heading-integration-v1",
                    new PpStructureV3Options(
                        new Uri(
                            "http://127.0.0.1:1/layout-parsing")),
                    new PaddleOcrOptions(
                        new Uri(
                            "http://127.0.0.1:1/ocr"),
                        "split-heading-integration-ocr"),
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
                new ByteSourceArtifactReader(
                    sourceBytes),
                complexDocumentPageThreshold:
                    100);

        var preview =
            await provider.InspectAsync(
                unitId);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                preview.SuggestedProposal);

        Assert.True(
            preview.SplitSuggested);

        Assert.Equal(
            StructuralHeadingPartitionStrategy.StructuralHeadingStrategyId,
            proposal.StrategyId);

        Assert.Equal(
            DocumentPartitionProposalReliability.Fallback,
            proposal.Reliability);

        Assert.Collection(
            proposal.Segments,
            segment =>
            {
                Assert.Null(
                    segment.SuggestedTitle);

                Assert.Equal(
                    1,
                    Assert.IsType<DocumentPartitionPosition.PhysicalPage>(
                            segment.Extent.Start)
                        .PhysicalPageNumber);

                Assert.Equal(
                    1,
                    Assert.IsType<DocumentPartitionPosition.PhysicalPage>(
                            segment.Extent.End)
                        .PhysicalPageNumber);
            },
            segment =>
                Assert.Equal(
                    "Chapter One",
                    segment.SuggestedTitle),
            segment =>
                Assert.Equal(
                    "Chapter Two",
                    segment.SuggestedTitle));
    }

    [Fact]
    public async Task InspectAsync_EpubWithoutNavigationFallsBackToNativeHeadings()
    {
        var sourceBytes =
            CreateEpubWithStructuralHeadings();

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
                            'c',
                            64)),
                    sourceBytes.Length),
                "headings-without-navigation.epub",
                "application/epub+zip",
                "structural EPUB fallback integration fixture",
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
                    "split-epub-heading-integration-v1",
                    new PpStructureV3Options(
                        new Uri(
                            "http://127.0.0.1:1/layout-parsing")),
                    new PaddleOcrOptions(
                        new Uri(
                            "http://127.0.0.1:1/ocr"),
                        "split-epub-heading-integration-ocr"),
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
                new ByteSourceArtifactReader(
                    sourceBytes));

        var preview =
            await provider.InspectAsync(
                unitId);

        var axis =
            Assert.IsType<DocumentPartitionAxis.ContentUnits>(
                preview.Axis);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                preview.SuggestedProposal);

        Assert.Equal(
            StructuralHeadingPartitionStrategy.StructuralHeadingStrategyId,
            proposal.StrategyId);

        Assert.Equal(
            3,
            axis.ContentUnitIds.Count);

        Assert.Equal(
            2,
            preview.ContentUnitLabels.Count);

        Assert.Equal(
            0,
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                    proposal.Segments[0].Extent.Start)
                .ContentUnitIndex);

        Assert.Equal(
            2,
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                    proposal.Segments[^1].Extent.End)
                .ContentUnitIndex);
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

    private static byte[] CreatePdfWithStructuralHeadings()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        for (var pageNumber = 1;
             pageNumber <= 5;
             pageNumber++)
        {
            var page =
                builder.AddPage(
                    PageSize.A4);

            if (pageNumber is 2 or 4)
            {
                page.AddText(
                    pageNumber ==
                        2
                        ? "Chapter One"
                        : "Chapter Two",
                    24,
                    new PdfPoint(
                        72,
                        700),
                    font);
            }

            page.AddText(
                "This ordinary paragraph contains enough native body words to establish the document baseline.",
                12,
                new PdfPoint(
                    72,
                    620),
                font);
        }

        return builder.Build();
    }

    private static byte[] CreateEpubWithStructuralHeadings()
    {
        using var output =
            new MemoryStream();

        using (var archive =
               new ZipArchive(
                   output,
                   ZipArchiveMode.Create,
                   leaveOpen:
                       true))
        {
            WriteArchiveEntry(
                archive,
                "mimetype",
                "application/epub+zip",
                CompressionLevel.NoCompression);

            WriteArchiveEntry(
                archive,
                "META-INF/container.xml",
                """
                <?xml version="1.0"?>
                <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml" /></rootfiles>
                </container>
                """);

            WriteArchiveEntry(
                archive,
                "OPS/package.opf",
                """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="book-id">urn:test:manager-headings</dc:identifier><dc:title>Manager heading fixture</dc:title><dc:language>en</dc:language></metadata>
                  <manifest>
                    <item id="front" href="text/front.xhtml" media-type="application/xhtml+xml" />
                    <item id="one" href="text/one.xhtml" media-type="application/xhtml+xml" />
                    <item id="two" href="text/two.xhtml" media-type="application/xhtml+xml" />
                  </manifest>
                  <spine><itemref idref="front" /><itemref idref="one" /><itemref idref="two" /></spine>
                </package>
                """);

            WriteArchiveEntry(
                archive,
                "OPS/text/front.xhtml",
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Front</title></head><body><p>Front matter.</p></body></html>");

            WriteArchiveEntry(
                archive,
                "OPS/text/one.xhtml",
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>One</title></head><body><h1>Chapter One</h1><p>Body.</p></body></html>");

            WriteArchiveEntry(
                archive,
                "OPS/text/two.xhtml",
                "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Two</title></head><body><h1>Chapter Two</h1><p>Body.</p></body></html>");
        }

        return output.ToArray();
    }

    private static void WriteArchiveEntry(
        ZipArchive archive,
        string path,
        string content,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var entry =
            archive.CreateEntry(
                path,
                compressionLevel);

        using var stream =
            entry.Open();

        using var writer =
            new StreamWriter(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false));

        writer.Write(
            content);
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

    private sealed class ByteSourceArtifactReader(
        byte[] sourceBytes)
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
                new MemoryStream(
                    sourceBytes,
                    writable:
                        false));
        }
    }

    #endregion
}
