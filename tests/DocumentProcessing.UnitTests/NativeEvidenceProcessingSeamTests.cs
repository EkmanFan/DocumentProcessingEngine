using Xunit;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests;

public sealed class NativeEvidenceProcessingSeamTests
{
    #region Variables and Constants

    private static readonly DocumentFormatId AlternateFormat =
        new(
            "native-evidence-seam-alternate");

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessPreparedEvidenceAsync_DoesNotInvokeNativeExtractor()
    {
        var extractor =
            new ThrowingDocumentExtractor();

        var preflight =
            new CountingPreflightAnalyzer();

        var processor =
            CreateProcessor(
                extractor,
                preflight);

        var evidence =
            CreateEvidence(
                AlternateFormat);

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "prepared evidence fixture"u8.ToArray())),
                CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await processor
                    .ProcessPreparedEvidenceAsync(
                        prepared,
                        DocumentFormatId.Pdf,
                        evidence,
                        openVisualDestinationAsync:
                            null,
                        CancellationToken.None));

        Assert.Equal(
            0,
            extractor.ExtractCalls);

        Assert.Equal(
            0,
            preflight.AnalyzeCalls);
    }

    [Fact]
    public async Task ProcessPreparedEvidenceAsync_RejectsFormatOutsideProcessorComposition()
    {
        var extractor =
            new ThrowingDocumentExtractor();

        var preflight =
            new CountingPreflightAnalyzer();

        var processor =
            CreateProcessor(
                extractor,
                preflight);

        var evidence =
            CreateEvidence(
                AlternateFormat);

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "prepared evidence fixture"u8.ToArray())),
                CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await processor
                    .ProcessPreparedEvidenceAsync(
                        prepared,
                        AlternateFormat,
                        evidence,
                        openVisualDestinationAsync:
                            null,
                        CancellationToken.None));

        Assert.Equal(
            0,
            extractor.ExtractCalls);

        Assert.Equal(
            0,
            preflight.AnalyzeCalls);
    }

    [Fact]
    public async Task ProcessPreparedEvidencePortableAsync_DoesNotInvokeNativeExtractor()
    {
        var extractor =
            new ThrowingDocumentExtractor();

        var preflight =
            new CountingPreflightAnalyzer();

        var processor =
            CreateProcessor(
                extractor,
                preflight);

        var evidence =
            CreateEvidence(
                AlternateFormat);

        await using var prepared =
            await PreparedDocumentSource.CreateAsync(
                new DocumentSource(
                    new MemoryStream(
                        "portable prepared evidence fixture"u8.ToArray())),
                CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await processor
                    .ProcessPreparedEvidencePortableAsync(
                        prepared,
                        DocumentFormatId.Pdf,
                        evidence,
                        openVisualDestinationAsync:
                            null,
                        progressReporter:
                            null,
                        CancellationToken.None));

        Assert.Equal(
            0,
            extractor.ExtractCalls);

        Assert.Equal(
            0,
            preflight.AnalyzeCalls);
    }

    #endregion

    #region Methods Fixtures

    private static DocumentProcessor CreateProcessor(
        ThrowingDocumentExtractor extractor,
        CountingPreflightAnalyzer preflight) =>
        new(
            DocumentFormatId.Pdf,
            extractor,
            preflight,
            "native-evidence-seam-test",
            new ProcessingComponentIdentity(
                "native-evidence-seam-extractor",
                "test-v1"));

    private static PagedNativeDocumentEvidence CreateEvidence(
        DocumentFormatId format)
    {
        var extraction =
            new DocumentExtractionResult(
                format);

        var coordinated =
            new DocumentExtractionWithRasterObservationsResult(
                extraction,
                Array.Empty<PageVisualRasterObservations>(),
                rasterObservationFailure:
                    null);

        return new PagedNativeDocumentEvidence(
            coordinated);
    }

    #endregion

    #region Types

    private sealed class ThrowingDocumentExtractor
        : IDocumentExtractor
    {
        #region Properties

        public int ExtractCalls { get; private set; }

        #endregion

        #region Methods Extraction

        public bool CanExtract(
            DocumentFormatId format) =>
            true;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            ExtractCalls++;

            throw new InvalidOperationException(
                "Native extractor must not be invoked by the preacquired-evidence seam.");
        }

        #endregion
    }

    private sealed class CountingPreflightAnalyzer
        : IDocumentPreflightAnalyzer
    {
        #region Properties

        public int AnalyzeCalls { get; private set; }

        #endregion

        #region Methods Preflight

        public bool CanAnalyze(
            DocumentFormatId format) =>
            true;

        public DocumentPreflightResult Analyze(
            DocumentExtractionResult extraction)
        {
            AnalyzeCalls++;

            throw new InvalidOperationException(
                "The invalid fixture must fail extraction validation before preflight.");
        }

        #endregion
    }

    #endregion
}
