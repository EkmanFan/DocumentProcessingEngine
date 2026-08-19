using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DocumentProcessingEngineFormatRoutingTests
{
    #region Variables and Constants

    private const string SourceSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessDocumentAsync_ExecutesSelectedInjectedStrategy()
    {
        var expected =
            CreateCurrentResult();

        var processor =
            new StubFormatProcessor(
                DocumentFormatId.Pdf,
                expected);

        var engine =
            new DocumentProcessingEngine();

        await using var stream =
            new MemoryStream(
                "%PDF-test"u8.ToArray());

        var actual =
            await engine.ProcessDocumentAsync(
                new DocumentSource(
                    stream,
                    "fixture.pdf",
                    "application/pdf"),
                processor);

        Assert.Same(
            expected,
            actual);

        Assert.Equal(
            1,
            processor.ProcessCallCount);
    }

    [Fact]
    public async Task ProcessDocumentAsync_RejectsNullSelectedStrategy()
    {
        var engine =
            new DocumentProcessingEngine();

        await using var stream =
            new MemoryStream(
                [1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () =>
                engine.ProcessDocumentAsync(
                    new DocumentSource(
                        stream),
                    formatProcessor:
                        null!));
    }

    [Fact]
    public async Task ProcessDocumentAsync_PropagatesCallerCancellationBeforeStrategyExecution()
    {
        var processor =
            new StubFormatProcessor(
                DocumentFormatId.Pdf,
                CreateCurrentResult());

        var engine =
            new DocumentProcessingEngine();

        await using var stream =
            new MemoryStream(
                [1, 2, 3]);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                engine.ProcessDocumentAsync(
                    new DocumentSource(
                        stream),
                    processor,
                    cancellation.Token));

        Assert.Equal(
            0,
            processor.ProcessCallCount);
    }

    [Fact]
    public async Task ProcessDocumentAsync_RejectsNullStrategyResult()
    {
        var engine =
            new DocumentProcessingEngine();

        await using var stream =
            new MemoryStream(
                [1, 2, 3]);

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                engine.ProcessDocumentAsync(
                    new DocumentSource(
                        stream),
                    new NullResultFormatProcessor()));
    }

    #endregion

    #region Methods Fixtures

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

    private sealed class NullResultFormatProcessor
        : IDocumentFormatProcessor
    {
        public DocumentFormatId Format =>
            DocumentFormatId.Pdf;

        public Task<DocumentProcessingResult> ProcessDocumentAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<DocumentProcessingResult>(
                null!);
        }
    }

    #endregion
}
