using System.Buffers;
using System.Security.Cryptography;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;

namespace DocumentProcessing.Manager.Persistence.Files;

/// <summary>
/// Content-addressed filesystem adapter preserving exact immutable source bytes.
/// </summary>
public sealed class FileSystemSourceArtifactCustodyStore
    : ISourceArtifactWriter,
      ISourceArtifactReader
{
    #region Variables and Constants

    private const int
        BufferSize =
            128 *
            1024;

    private readonly FileSystemSourceArtifactCustodyOptions
        _options;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the content-addressed filesystem custody adapter.
    /// </summary>
    public FileSystemSourceArtifactCustodyStore(
        FileSystemSourceArtifactCustodyOptions options)
    {
        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));
    }

    #endregion

    #region Methods Write

    /// <inheritdoc />
    public async ValueTask<SourceArtifact> StoreAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "Source-artifact content must be readable.",
                nameof(content));
        }

        var stagingDirectory =
            Path.Combine(
                _options.RootDirectory,
                ".staging");

        Directory.CreateDirectory(
            stagingDirectory);

        var temporaryPath =
            Path.Combine(
                stagingDirectory,
                $"{Guid.NewGuid():N}.tmp");

        try
        {
            SourceArtifact artifact;

            await using (var temporary =
                         new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             BufferSize,
                             FileOptions.Asynchronous |
                             FileOptions.SequentialScan |
                             FileOptions.WriteThrough))
            {
                artifact =
                    await CopyAndHashAsync(
                            content,
                            temporary,
                            cancellationToken)
                        .ConfigureAwait(false);

                await temporary
                    .FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                temporary.Flush(
                    flushToDisk:
                        true);
            }

            var artifactPath =
                GetArtifactPath(
                    artifact.Digest);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    artifactPath) ??
                throw new InvalidOperationException(
                    "Content-addressed artifact directory could not be resolved."));

            if (File.Exists(
                    artifactPath))
            {
                await EnsureExistingArtifactAsync(
                        artifactPath,
                        artifact,
                        cancellationToken)
                    .ConfigureAwait(false);

                MakeReadOnly(
                    artifactPath);

                return artifact;
            }

            try
            {
                File.Move(
                    temporaryPath,
                    artifactPath);

                MakeReadOnly(
                    artifactPath);
            }
            catch (IOException)
                when (File.Exists(
                    artifactPath))
            {
                await EnsureExistingArtifactAsync(
                        artifactPath,
                        artifact,
                        cancellationToken)
                    .ConfigureAwait(false);

                MakeReadOnly(
                    artifactPath);
            }

            return artifact;
        }
        finally
        {
            TryDeleteStagingFile(
                temporaryPath);
        }
    }

    private async ValueTask<SourceArtifact> CopyAndHashAsync(
        Stream source,
        Stream destination,
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
                                BufferSize),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    break;
                }

                byteLength =
                    checked(
                        byteLength +
                        read);

                if (byteLength >
                    _options.MaximumArtifactBytes)
                {
                    throw new InvalidDataException(
                        $"Source artifact exceeds the configured {_options.MaximumArtifactBytes}-byte custody limit.");
                }

                hash.AppendData(
                    buffer,
                    0,
                    read);

                await destination
                    .WriteAsync(
                        buffer.AsMemory(
                            0,
                            read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                buffer,
                clearArray:
                    true);
        }

        if (byteLength ==
            0)
        {
            throw new InvalidDataException(
                "Source artifact cannot be empty.");
        }

        return new SourceArtifact(
            new Sha256Digest(
                Convert.ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant()),
            byteLength);
    }

    #endregion

    #region Methods Read

    /// <inheritdoc />
    public async ValueTask<bool> VerifyAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            artifact);

        var artifactPath =
            GetArtifactPath(
                artifact.Digest);

        if (!File.Exists(
                artifactPath))
        {
            return false;
        }

        await using var stream =
            OpenArtifactFile(
                artifactPath);

        return await VerifyStreamAsync(
                stream,
                artifact,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Stream> OpenReadAsync(
        SourceArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            artifact);

        var artifactPath =
            GetArtifactPath(
                artifact.Digest);

        if (!File.Exists(
                artifactPath))
        {
            throw new SourceArtifactIntegrityException(
                artifact.Digest,
                $"Retained source artifact '{artifact.Digest}' is missing.");
        }

        var stream =
            OpenArtifactFile(
                artifactPath);

        try
        {
            if (!await VerifyStreamAsync(
                    stream,
                    artifact,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                throw new SourceArtifactIntegrityException(
                    artifact.Digest,
                    $"Retained source artifact '{artifact.Digest}' failed integrity verification.");
            }

            stream.Position =
                0;

            return stream;
        }
        catch
        {
            await stream.DisposeAsync()
                .ConfigureAwait(false);

            throw;
        }
    }

    private static async ValueTask<bool> VerifyStreamAsync(
        Stream stream,
        SourceArtifact artifact,
        CancellationToken cancellationToken)
    {
        if (stream.Length !=
            artifact.ByteLength)
        {
            return false;
        }

        stream.Position =
            0;

        var digest =
            await SHA256.HashDataAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);

        return string.Equals(
            Convert.ToHexString(
                    digest)
                .ToLowerInvariant(),
            artifact.Digest.Value,
            StringComparison.Ordinal);
    }

    private async ValueTask EnsureExistingArtifactAsync(
        string artifactPath,
        SourceArtifact artifact,
        CancellationToken cancellationToken)
    {
        await using var existing =
            OpenArtifactFile(
                artifactPath);

        if (!await VerifyStreamAsync(
                existing,
                artifact,
                cancellationToken)
            .ConfigureAwait(false))
        {
            throw new SourceArtifactIntegrityException(
                artifact.Digest,
                $"Existing content-addressed artifact '{artifact.Digest}' does not match its expected bytes.");
        }
    }

    #endregion

    #region Methods Paths

    private string GetArtifactPath(
        Sha256Digest digest) =>
        Path.Combine(
            _options.RootDirectory,
            "sha256",
            digest.Value[..2],
            digest.Value[2..4],
            digest.Value);

    private static FileStream OpenArtifactFile(
        string artifactPath) =>
        new(
            artifactPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

    private static void MakeReadOnly(
        string artifactPath)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(
                artifactPath,
                File.GetAttributes(
                    artifactPath) |
                FileAttributes.ReadOnly);

            return;
        }

        File.SetUnixFileMode(
            artifactPath,
            UnixFileMode.UserRead);
    }

    private static void TryDeleteStagingFile(
        string temporaryPath)
    {
        try
        {
            File.Delete(
                temporaryPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup must not hide the primary custody failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup must not hide the primary custody failure.
        }
    }

    #endregion
}
