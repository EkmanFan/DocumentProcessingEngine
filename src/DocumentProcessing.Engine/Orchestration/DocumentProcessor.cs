using System.Buffers;
using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Public end-to-end document-processing entry point.
///
/// Phase 21A intentionally implements only the proven native-only vertical:
///
/// source
///   -> type detection
///   -> native extraction
///   -> preflight
///   -> native hybrid elements
///   -> assembly
///   -> normalization
///   -> segmentation
///   -> provenance context
///   -> DocumentIngestionResult
///
/// Hybrid/raster documents are rejected explicitly rather than silently
/// returning an incomplete result. Phase 21B/21C will extend the execution
/// decision boundary through the already-defined page-processing policy.
/// </summary>
public sealed class DocumentProcessor
{
    private readonly IDocumentTypeDetector _documentTypeDetector;
    private readonly IDocumentExtractor _nativeExtractor;
    private readonly IDocumentPreflightAnalyzer _preflightAnalyzer;
    private readonly string _engineVersion;
    private readonly ProcessingComponentIdentity _nativeExtractionIdentity;

    public DocumentProcessor(
        IDocumentTypeDetector documentTypeDetector,
        IDocumentExtractor nativeExtractor,
        IDocumentPreflightAnalyzer preflightAnalyzer,
        string engineVersion,
        ProcessingComponentIdentity nativeExtractionIdentity)
    {
        _documentTypeDetector =
            documentTypeDetector ??
            throw new ArgumentNullException(
                nameof(documentTypeDetector));

        _nativeExtractor =
            nativeExtractor ??
            throw new ArgumentNullException(
                nameof(nativeExtractor));

        _preflightAnalyzer =
            preflightAnalyzer ??
            throw new ArgumentNullException(
                nameof(preflightAnalyzer));

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

    public async Task<DocumentIngestionResult> ProcessAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        await using var prepared =
            await PreparedDocumentSource
                .CreateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        prepared.ResetForRead();

        var detection =
            await _documentTypeDetector
                .DetectAsync(
                    prepared.Source,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!detection.IsSupported)
        {
            throw new NotSupportedException(
                "The document format is not supported by the configured document processor.");
        }

        if (detection.Format is not { } format)
        {
            throw new InvalidDataException(
                "Document type detection reported a supported document without a format identifier.");
        }

        if (!_nativeExtractor.CanExtract(
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

        prepared.ResetForRead();

        var extraction =
            await _nativeExtractor
                .ExtractAsync(
                    prepared.Source,
                    format,
                    cancellationToken)
                .ConfigureAwait(false);

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

        if (preflight.Classification !=
            DocumentPreflightClassification.HealthyBornDigital)
        {
            throw new NotSupportedException(
                $"Phase 21A native-only processing requires '{DocumentPreflightClassification.HealthyBornDigital}' preflight classification; " +
                $"observed '{preflight.Classification}'. Hybrid/raster routing is introduced in later Phase 21 increments.");
        }

        var assembledPages =
            new List<Core.Hybrid.HybridDocumentPage>(
                extraction.Pages.Count);

        foreach (var page in
                 extraction.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (page.Blocks.Count ==
                0)
            {
                throw new InvalidDataException(
                    $"Healthy born-digital page {page.PhysicalPageNumber} contains native words but no native text blocks.");
            }

            var elements =
                page.Blocks
                    .Select(
                        block =>
                            HybridDocumentElementFactory
                                .FromNative(
                                    page.PhysicalPageNumber,
                                    block))
                    .ToArray();

            assembledPages.Add(
                HybridDocumentAssembler
                    .AssemblePage(
                        page,
                        elements));
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

        var provenanceContext =
            new DocumentProcessingProvenanceContext(
                new DocumentSourceIdentity(
                    format,
                    prepared.Sha256,
                    prepared.ByteLength,
                    extraction.Pages.Count,
                    source.FileName,
                    source.DeclaredMediaType),
                _engineVersion,
                _nativeExtractionIdentity);

        return DocumentIngestionResultBuilder
            .Build(
                segmentation,
                provenanceContext);
    }

    private static void ValidateExtraction(
        DocumentFormatId detectedFormat,
        DocumentExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        if (extraction.Format !=
            detectedFormat)
        {
            throw new InvalidDataException(
                $"Native extraction format '{extraction.Format}' does not match detected format '{detectedFormat}'.");
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

    /// <summary>
    /// Makes the input repeatably readable while computing the custody root.
    ///
    /// Seekable caller-owned streams are hashed from position zero and have
    /// their original position restored when processing completes.
    ///
    /// Non-seekable streams are copied once to an internal delete-on-close
    /// temporary file so type detection and extraction can safely reread the
    /// exact bytes without placing a potentially large document in memory.
    ///
    /// Temporary paths are strictly internal and never enter result/provenance
    /// contracts.
    /// </summary>
    private sealed class PreparedDocumentSource
        : IAsyncDisposable
    {
        private const int BufferSize =
            81920;

        private readonly Stream? _ownedStream;
        private readonly Stream? _borrowedStream;
        private readonly long? _borrowedOriginalPosition;

        private PreparedDocumentSource(
            DocumentSource source,
            string sha256,
            long byteLength,
            Stream? ownedStream,
            Stream? borrowedStream,
            long? borrowedOriginalPosition)
        {
            Source =
                source;

            Sha256 =
                sha256;

            ByteLength =
                byteLength;

            _ownedStream =
                ownedStream;

            _borrowedStream =
                borrowedStream;

            _borrowedOriginalPosition =
                borrowedOriginalPosition;
        }

        public DocumentSource Source { get; }

        public string Sha256 { get; }

        public long ByteLength { get; }

        public static async ValueTask<PreparedDocumentSource> CreateAsync(
            DocumentSource source,
            CancellationToken cancellationToken)
        {
            if (source.Content.CanSeek)
            {
                var originalPosition =
                    source.Content.Position;

                try
                {
                    source.Content.Position =
                        0;

                    var identity =
                        await ReadAndHashAsync(
                            source.Content,
                            destination:
                                null,
                            cancellationToken)
                            .ConfigureAwait(false);

                    EnsureNonEmpty(
                        identity.ByteLength);

                    source.Content.Position =
                        0;

                    return new PreparedDocumentSource(
                        source,
                        identity.Sha256,
                        identity.ByteLength,
                        ownedStream:
                            null,
                        borrowedStream:
                            source.Content,
                        borrowedOriginalPosition:
                            originalPosition);
                }
                catch
                {
                    try
                    {
                        source.Content.Position =
                            originalPosition;
                    }
                    catch
                    {
                        // Preserve the original processing exception.
                    }

                    throw;
                }
            }

            var temporaryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"document-processing-{Path.GetRandomFileName()}");

            var temporaryStream =
                new FileStream(
                    temporaryPath,
                    new FileStreamOptions
                    {
                        Mode =
                            FileMode.CreateNew,
                        Access =
                            FileAccess.ReadWrite,
                        Share =
                            FileShare.None,
                        BufferSize =
                            BufferSize,
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan |
                            FileOptions.DeleteOnClose
                    });

            try
            {
                var identity =
                    await ReadAndHashAsync(
                        source.Content,
                        temporaryStream,
                        cancellationToken)
                        .ConfigureAwait(false);

                EnsureNonEmpty(
                    identity.ByteLength);

                await temporaryStream
                    .FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                temporaryStream.Position =
                    0;

                var bufferedSource =
                    new DocumentSource(
                        temporaryStream,
                        source.FileName,
                        source.DeclaredMediaType);

                return new PreparedDocumentSource(
                    bufferedSource,
                    identity.Sha256,
                    identity.ByteLength,
                    ownedStream:
                        temporaryStream,
                    borrowedStream:
                        null,
                    borrowedOriginalPosition:
                        null);
            }
            catch
            {
                await temporaryStream
                    .DisposeAsync()
                    .ConfigureAwait(false);

                throw;
            }
        }

        public void ResetForRead()
        {
            if (!Source.Content.CanSeek)
            {
                throw new InvalidOperationException(
                    "Prepared document source must be seekable.");
            }

            Source.Content.Position =
                0;
        }

        public async ValueTask DisposeAsync()
        {
            if (_ownedStream is not null)
            {
                await _ownedStream
                    .DisposeAsync()
                    .ConfigureAwait(false);

                return;
            }

            if (_borrowedStream is not null &&
                _borrowedOriginalPosition.HasValue &&
                _borrowedStream.CanSeek)
            {
                _borrowedStream.Position =
                    _borrowedOriginalPosition.Value;
            }
        }

        private static async ValueTask<SourceByteIdentity> ReadAndHashAsync(
            Stream source,
            Stream? destination,
            CancellationToken cancellationToken)
        {
            using var hash =
                IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

            var buffer =
                ArrayPool<byte>.Shared.Rent(
                    BufferSize);

            long byteLength =
                0;

            try
            {
                while (true)
                {
                    var read =
                        await source
                            .ReadAsync(
                                buffer.AsMemory(
                                    0,
                                    buffer.Length),
                                cancellationToken)
                            .ConfigureAwait(false);

                    if (read ==
                        0)
                    {
                        break;
                    }

                    hash.AppendData(
                        buffer,
                        0,
                        read);

                    if (destination is not null)
                    {
                        await destination
                            .WriteAsync(
                                buffer.AsMemory(
                                    0,
                                    read),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    byteLength =
                        checked(
                            byteLength +
                            read);
                }

                var sha256 =
                    Convert.ToHexString(
                            hash.GetHashAndReset())
                        .ToLowerInvariant();

                return new SourceByteIdentity(
                    sha256,
                    byteLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(
                    buffer);
            }
        }

        private static void EnsureNonEmpty(
            long byteLength)
        {
            if (byteLength <=
                0)
            {
                throw new InvalidDataException(
                    "Document source is empty.");
            }
        }

        private readonly record struct SourceByteIdentity(
            string Sha256,
            long ByteLength);
    }
}
