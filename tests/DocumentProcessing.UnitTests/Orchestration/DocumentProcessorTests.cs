using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessorTests
{
    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    [Fact]
    public async Task ProcessAsync_HealthyBornDigital_BuildsCompleteNativeResult()
    {
        var bytes =
            "%PDF-test-native-document"u8.ToArray();

        var extraction =
            CreateHealthyExtraction();

        var detector =
            new StubDetector(
                new DocumentTypeDetectionResult(
                    DocumentFormatId.Pdf,
                    "application/pdf",
                    IsSupported:
                        true));

        var extractor =
            new StubExtractor(
                extraction);

        var preflight =
            new StubPreflightAnalyzer(
                DocumentPreflightClassification.HealthyBornDigital);

        await using var stream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var source =
            new DocumentSource(
                stream,
                "sample.pdf",
                "application/pdf");

        var processor =
            CreateProcessor(
                detector,
                extractor,
                preflight);

        var result =
            await processor.ProcessAsync(
                source);

        Assert.Equal(
            DocumentFormatId.Pdf,
            result.Source.Format);

        Assert.Equal(
            ComputeSha256(
                bytes),
            result.Source.Sha256);

        Assert.Equal(
            bytes.Length,
            result.Source.ByteLength);

        Assert.Equal(
            2,
            result.Source.PhysicalPageCount);

        Assert.Equal(
            "sample.pdf",
            result.Source.FileName);

        Assert.Equal(
            "application/pdf",
            result.Source.DeclaredMediaType);

        Assert.Equal(
            2,
            result.Pages.Count);

        Assert.Equal(
            2,
            result.Elements.Count);

        Assert.NotEmpty(
            result.StructuralSegments);

        Assert.Empty(
            result.QualityObservations
                .OcrConfidenceObservations);

        Assert.Equal(
            "test-engine-v1",
            result.ProcessingManifest.EngineVersion);

        Assert.Equal(
            NativeIdentity,
            result.ProcessingManifest.NativeExtraction);

        Assert.Null(
            result.ProcessingManifest.Rasterization);

        Assert.Null(
            result.ProcessingManifest.LayoutAnalysis);

        Assert.Empty(
            result.ProcessingManifest.Ocr);

        Assert.Null(
            result.ProcessingManifest.Reconciliation);

        Assert.Empty(
            result.ProcessingManifest.VisualPreservationProfileIds);

        Assert.All(
            result.Elements,
            element =>
            {
                Assert.Equal(
                    TextSelectionOrigin.NativePdf,
                    element.TextOrigin);

                Assert.NotNull(
                    element.NormalizedText);

                Assert.NotNull(
                    element.NativeBlockSourceSequence);

                Assert.Null(
                    element.LayoutObservationSequence);

                Assert.Null(
                    element.OcrBackendId);

                Assert.Null(
                    element.OcrProfileId);

                Assert.Null(
                    element.ReconciliationDecision);

                Assert.Null(
                    element.PreservedVisual);
            });

        Assert.True(
            detector.SawSeekableSource);

        Assert.True(
            extractor.SawSeekableSource);

        Assert.Equal(
            1,
            detector.CallCount);

        Assert.Equal(
            1,
            extractor.CallCount);

        Assert.Equal(
            1,
            preflight.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_SeekableSource_RestoresCallerPosition()
    {
        var bytes =
            "%PDF-position-test"u8.ToArray();

        await using var stream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        stream.Position =
            5;

        var processor =
            CreateProcessor(
                extraction:
                    CreateHealthyExtraction());

        await processor.ProcessAsync(
            new DocumentSource(
                stream,
                "position.pdf",
                "application/pdf"));

        Assert.Equal(
            5,
            stream.Position);
    }

    [Fact]
    public async Task ProcessAsync_NonSeekableSource_BuffersWithoutChangingCustodyBytes()
    {
        var bytes =
            "%PDF-non-seekable-test"u8.ToArray();

        await using var stream =
            new NonSeekableReadStream(
                bytes);

        var detector =
            new StubDetector(
                new DocumentTypeDetectionResult(
                    DocumentFormatId.Pdf,
                    "application/pdf",
                    IsSupported:
                        true));

        var extractor =
            new StubExtractor(
                CreateHealthyExtraction());

        var processor =
            CreateProcessor(
                detector,
                extractor,
                new StubPreflightAnalyzer(
                    DocumentPreflightClassification.HealthyBornDigital));

        var result =
            await processor.ProcessAsync(
                new DocumentSource(
                    stream,
                    "streamed.pdf",
                    "application/pdf"));

        Assert.Equal(
            ComputeSha256(
                bytes),
            result.Source.Sha256);

        Assert.Equal(
            bytes.Length,
            result.Source.ByteLength);

        Assert.True(
            detector.SawSeekableSource);

        Assert.True(
            extractor.SawSeekableSource);
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedDetection_StopsBeforeExtraction()
    {
        var detector =
            new StubDetector(
                DocumentTypeDetectionResult.Unknown);

        var extractor =
            new StubExtractor(
                CreateHealthyExtraction());

        var processor =
            CreateProcessor(
                detector,
                extractor,
                new StubPreflightAnalyzer(
                    DocumentPreflightClassification.HealthyBornDigital));

        await using var stream =
            CreateSourceStream();

        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                processor.ProcessAsync(
                    new DocumentSource(
                        stream)));

        Assert.Equal(
            0,
            extractor.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ExtractorCannotHandleDetectedFormat_RejectsExplicitly()
    {
        var extractor =
            new StubExtractor(
                CreateHealthyExtraction(),
                canExtract:
                    false);

        var processor =
            CreateProcessor(
                new StubDetector(
                    new DocumentTypeDetectionResult(
                        DocumentFormatId.Pdf,
                        "application/pdf",
                        IsSupported:
                            true)),
                extractor,
                new StubPreflightAnalyzer(
                    DocumentPreflightClassification.HealthyBornDigital));

        await using var stream =
            CreateSourceStream();

        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                processor.ProcessAsync(
                    new DocumentSource(
                        stream)));

        Assert.Equal(
            0,
            extractor.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_PreflightCannotHandleDetectedFormat_RejectsExplicitly()
    {
        var preflight =
            new StubPreflightAnalyzer(
                DocumentPreflightClassification.HealthyBornDigital,
                canAnalyze:
                    false);

        var processor =
            CreateProcessor(
                new StubDetector(
                    new DocumentTypeDetectionResult(
                        DocumentFormatId.Pdf,
                        "application/pdf",
                        IsSupported:
                            true)),
                new StubExtractor(
                    CreateHealthyExtraction()),
                preflight);

        await using var stream =
            CreateSourceStream();

        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                processor.ProcessAsync(
                    new DocumentSource(
                        stream)));

        Assert.Equal(
            0,
            preflight.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ExtractionFormatMismatch_RejectsInvalidEvidence()
    {
        var wrongFormat =
            new DocumentFormatId(
                "wrong");

        var processor =
            CreateProcessor(
                extraction:
                    new DocumentExtractionResult(
                        wrongFormat,
                        CreateHealthyExtraction()
                            .Pages));

        await using var stream =
            CreateSourceStream();

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                processor.ProcessAsync(
                    new DocumentSource(
                        stream)));
    }

    [Theory]
    [InlineData(
        DocumentPreflightClassification.Hybrid)]
    [InlineData(
        DocumentPreflightClassification.RasterOrScanned)]
    [InlineData(
        DocumentPreflightClassification.Problematic)]
    public async Task ProcessAsync_NonNativeOnlyPreflight_RejectsRatherThanReturningPartialResult(
        DocumentPreflightClassification classification)
    {
        var processor =
            CreateProcessor(
                new StubDetector(
                    new DocumentTypeDetectionResult(
                        DocumentFormatId.Pdf,
                        "application/pdf",
                        IsSupported:
                            true)),
                new StubExtractor(
                    CreateHealthyExtraction()),
                new StubPreflightAnalyzer(
                    classification));

        await using var stream =
            CreateSourceStream();

        var exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () =>
                    processor.ProcessAsync(
                        new DocumentSource(
                            stream)));

        Assert.Contains(
            nameof(DocumentPreflightClassification.HealthyBornDigital),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_CancelledBeforeProcessing_PropagatesCancellation()
    {
        var processor =
            CreateProcessor(
                extraction:
                    CreateHealthyExtraction());

        await using var stream =
            CreateSourceStream();

        using var cancellation =
            new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                processor.ProcessAsync(
                    new DocumentSource(
                        stream),
                    cancellation.Token));
    }

    [Fact]
    public void PdfPreflightAnalyzer_AdvertisesOnlyPdfCapability()
    {
        IDocumentPreflightAnalyzer analyzer =
            new PdfPreflightAnalyzer();

        Assert.True(
            analyzer.CanAnalyze(
                DocumentFormatId.Pdf));

        Assert.False(
            analyzer.CanAnalyze(
                new DocumentFormatId(
                    "docx")));
    }

    private static DocumentProcessor CreateProcessor(
        DocumentExtractionResult extraction) =>
        CreateProcessor(
            new StubDetector(
                new DocumentTypeDetectionResult(
                    DocumentFormatId.Pdf,
                    "application/pdf",
                    IsSupported:
                        true)),
            new StubExtractor(
                extraction),
            new StubPreflightAnalyzer(
                DocumentPreflightClassification.HealthyBornDigital));

    private static DocumentProcessor CreateProcessor(
        StubDetector detector,
        StubExtractor extractor,
        StubPreflightAnalyzer preflight) =>
        new(
            detector,
            extractor,
            preflight,
            "test-engine-v1",
            NativeIdentity);

    private static DocumentExtractionResult CreateHealthyExtraction() =>
        new(
            DocumentFormatId.Pdf,
            [
                CreatePage(
                    1,
                    "Alpha native paragraph."),
                CreatePage(
                    2,
                    "Beta native paragraph.")
            ]);

    private static DocumentExtractionPage CreatePage(
        int physicalPageNumber,
        string text)
    {
        var tokens =
            text.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var words =
            tokens
                .Select(
                    (token, index) =>
                        new DocumentWord(
                            index,
                            token,
                            new NormalizedRectangle(
                                0.10 +
                                index *
                                0.05,
                                0.20,
                                0.14 +
                                index *
                                0.05,
                                0.24),
                            "Body",
                            10))
                .ToArray();

        var block =
            new DocumentTextBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                text,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.90,
                    0.40),
                words,
                dominantFontName:
                    "Body",
                medianPointSize:
                    10,
                lineCount:
                    1);

        return new DocumentExtractionPage(
            physicalPageNumber,
            text,
            new NormalizedRectangle(
                0,
                0,
                1,
                1),
            wordCount:
                words.Length,
            rasterImageCount:
                0,
            largestRasterImageAreaRatio:
                0,
            sourceWidth:
                612,
            sourceHeight:
                792,
            words,
            blocks:
                [block]);
    }

    private static MemoryStream CreateSourceStream() =>
        new(
            "%PDF-unit-test"u8.ToArray(),
            writable:
                false);

    private static string ComputeSha256(
        ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(
                SHA256.HashData(
                    bytes))
            .ToLowerInvariant();

    private sealed class StubDetector(
        DocumentTypeDetectionResult result)
        : IDocumentTypeDetector
    {
        public int CallCount { get; private set; }

        public bool SawSeekableSource { get; private set; }

        public ValueTask<DocumentTypeDetectionResult> DetectAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            SawSeekableSource =
                source.Content.CanSeek;

            return ValueTask.FromResult(
                result);
        }
    }

    private sealed class StubExtractor
        : IDocumentExtractor
    {
        private readonly DocumentExtractionResult _result;
        private readonly bool _canExtract;

        public StubExtractor(
            DocumentExtractionResult result,
            bool canExtract = true)
        {
            _result =
                result;

            _canExtract =
                canExtract;
        }

        public int CallCount { get; private set; }

        public bool SawSeekableSource { get; private set; }

        public bool CanExtract(
            DocumentFormatId format) =>
            _canExtract;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            SawSeekableSource =
                source.Content.CanSeek;

            return ValueTask.FromResult(
                _result);
        }
    }

    private sealed class StubPreflightAnalyzer
        : IDocumentPreflightAnalyzer
    {
        private readonly DocumentPreflightClassification _classification;
        private readonly bool _canAnalyze;

        public StubPreflightAnalyzer(
            DocumentPreflightClassification classification,
            bool canAnalyze = true)
        {
            _classification =
                classification;

            _canAnalyze =
                canAnalyze;
        }

        public int CallCount { get; private set; }

        public bool CanAnalyze(
            DocumentFormatId format) =>
            _canAnalyze;

        public DocumentPreflightResult Analyze(
            DocumentExtractionResult extraction)
        {
            CallCount++;

            var pageCount =
                extraction.Pages.Count;

            var healthy =
                _classification ==
                DocumentPreflightClassification.HealthyBornDigital;

            return new DocumentPreflightResult(
                extraction.Format,
                pageCount,
                healthy
                    ? pageCount
                    : Math.Max(
                        0,
                        pageCount -
                        1),
                healthy
                    ? 0
                    : Math.Min(
                        1,
                        pageCount),
                healthy
                    ? 100
                    : 50,
                healthy ||
                pageCount ==
                0
                    ? []
                    : [pageCount],
                [],
                _classification);
        }
    }

    private sealed class NonSeekableReadStream
        : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableReadStream(
            byte[] bytes)
        {
            _inner =
                new MemoryStream(
                    bytes,
                    writable:
                        false);
        }

        public override bool CanRead =>
            true;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get =>
                throw new NotSupportedException();

            set =>
                throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            _inner.Read(
                buffer,
                offset,
                count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(
                buffer,
                cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(
            long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(
                disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner
                .DisposeAsync()
                .ConfigureAwait(false);

            GC.SuppressFinalize(
                this);
        }
    }
}
