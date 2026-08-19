using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

/// <summary>
/// Verifies the format-neutral strategy seam without exercising any concrete
/// document-format implementation.
/// </summary>
public sealed class DocumentProcessingEngineFormatRoutingTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessDocumentAsync_DelegatesToMatchingInjectedStrategy()
    {
        var pdfResult =
            CreateCurrentResult();

        var pdfProcessor =
            new StubFormatProcessor(
                DocumentFormatId.Pdf,
                pdfResult);

        var unexpectedProcessor =
            new UnexpectedFormatProcessor(
                new DocumentFormatId(
                    "epub"));

        var engine =
            new DocumentProcessingEngine(
                new StubDocumentTypeDetector(
                    new DocumentTypeDetectionResult(
                        DocumentFormatId.Pdf,
                        "application/pdf",
                        IsSupported: true)),
                [
                    unexpectedProcessor,
                    pdfProcessor
                ]);

        await using var stream =
            new MemoryStream(
                "%PDF-test"u8.ToArray());

        var source =
            new DocumentSource(
                stream,
                "fixture.pdf",
                "application/pdf");

        var actual =
            await engine
                .ProcessDocumentAsync(
                    source);

        Assert.Same(
            pdfResult,
            actual);

        Assert.Equal(
            1,
            pdfProcessor.ProcessCallCount);

        Assert.Equal(
            0,
            unexpectedProcessor.ProcessCallCount);
    }

    [Fact]
    public async Task ProcessDocumentAsync_RejectsDetectedFormatWithoutStrategy()
    {
        var engine =
            new DocumentProcessingEngine(
                new StubDocumentTypeDetector(
                    new DocumentTypeDetectionResult(
                        new DocumentFormatId(
                            "epub"),
                        "application/epub+zip",
                        IsSupported: true)),
                [
                    new StubFormatProcessor(
                        DocumentFormatId.Pdf,
                        CreateCurrentResult())
                ]);

        await using var stream =
            new MemoryStream(
                [1, 2, 3]);

        var error =
            await Assert.ThrowsAsync<NotSupportedException>(
                () =>
                    engine.ProcessDocumentAsync(
                        new DocumentSource(
                            stream,
                            "fixture.epub")));

        Assert.Contains(
            "epub",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessDocumentAsync_RejectsUnsupportedDetection()
    {
        var pdfProcessor =
            new StubFormatProcessor(
                DocumentFormatId.Pdf,
                CreateCurrentResult());

        var engine =
            new DocumentProcessingEngine(
                new StubDocumentTypeDetector(
                    DocumentTypeDetectionResult.Unknown),
                [pdfProcessor]);

        await using var stream =
            new MemoryStream(
                [1, 2, 3]);

        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                engine.ProcessDocumentAsync(
                    new DocumentSource(
                        stream)));

        Assert.Equal(
            0,
            pdfProcessor.ProcessCallCount);
    }

    [Fact]
    public void Constructor_RejectsDuplicateStrategiesForSameFormat()
    {
        var first =
            new StubFormatProcessor(
                DocumentFormatId.Pdf,
                CreateCurrentResult());

        var second =
            new StubFormatProcessor(
                DocumentFormatId.Pdf,
                CreateCurrentResult());

        var error =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentProcessingEngine(
                        new StubDocumentTypeDetector(
                            DocumentTypeDetectionResult.Unknown),
                        [
                            first,
                            second
                        ]));

        Assert.Contains(
            "one document format processor",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Methods Fixtures

    /// <summary>
    /// Creates a minimal format-neutral result for strategy-routing tests.
    /// </summary>
    private static DocumentProcessingResult CreateCurrentResult()
    {
        var source =
            new DocumentSourceDescriptor(
                DocumentFormatId.Pdf,
                SourceSha,
                byteLength:
                    1);

        var manifest =
            new DocumentProcessingManifest(
                engineVersion:
                    "test-engine",
                nativeExtraction:
                    new ProcessingComponentIdentity(
                        "test-native",
                        "test-profile"),
                rasterization:
                    null,
                layoutAnalysis:
                    null,
                ocr:
                    [],
                reconciliation:
                    null,
                visualPreservationProfileIds:
                    [],
                assemblyProfileId:
                    "test-assembly",
                normalizationProfileId:
                    "test-normalization",
                segmentationProfileId:
                    "test-segmentation");

        return new DocumentProcessingResult(
            source,
            manifest,
            elements:
                [],
            elementProcessingEvidence:
                [],
            structuralSegments:
                [],
            segmentProcessingEvidence:
                [],
            visualAssets:
                [],
            DocumentProcessingQualityObservations.Empty,
            sourceStructure:
                null);
    }

    #endregion

    #region Test Types

    private sealed class StubDocumentTypeDetector(
        DocumentTypeDetectionResult result)
        : IDocumentTypeDetector
    {
        public ValueTask<DocumentTypeDetectionResult> DetectAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                result);
        }
    }

    private sealed class StubFormatProcessor(
        DocumentFormatId format,
        DocumentProcessingResult result)
        : IDocumentFormatProcessor
    {
        public DocumentFormatId Format { get; } =
            format;

        public int ProcessCallCount { get; private set; }

        public Task<DocumentProcessingResult> ProcessDocumentAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            ProcessCallCount++;

            return Task.FromResult(
                result);
        }
    }

    private sealed class UnexpectedFormatProcessor(
        DocumentFormatId format)
        : IDocumentFormatProcessor
    {
        public DocumentFormatId Format { get; } =
            format;

        public int ProcessCallCount { get; private set; }

        public Task<DocumentProcessingResult> ProcessDocumentAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            ProcessCallCount++;

            throw new InvalidOperationException(
                "The non-matching format processor must not be invoked.");
        }
    }

    #endregion
}
