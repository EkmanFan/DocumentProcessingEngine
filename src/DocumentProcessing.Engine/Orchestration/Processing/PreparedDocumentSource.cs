using System.Buffers;
using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Preflight;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Results;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Engine.DualRun.InProcess;

namespace DocumentProcessing.Engine.Orchestration;

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
internal sealed class PreparedDocumentSource
    : IAsyncDisposable
{
    #region Variables and Constants

    private const int BufferSize =
        81920;

    private readonly Stream? _ownedStream;
    private readonly Stream? _borrowedStream;
    private readonly long? _borrowedOriginalPosition;

    #endregion

    #region ctor

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

    #endregion

    #region Properties

    public DocumentSource Source { get; }

    public string Sha256 { get; }

    public long ByteLength { get; }

    #endregion

    #region Methods Creation and Lifecycle

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

    #endregion

    #region Methods Stream and Hash

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

    #endregion

    #region Internal Types

    private readonly record struct SourceByteIdentity(
        string Sha256,
        long ByteLength);

    #endregion
}
