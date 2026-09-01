using System.Buffers;
using System.Security.Cryptography;
using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Persistence.Files;

internal sealed class FileSystemContentAddressedStore
{
    #region Variables and Constants

    private const int
        BufferSize =
            128 *
            1024;

    private readonly string
        _rootDirectory;

    private readonly long
        _maximumArtifactBytes;

    #endregion

    #region ctor

    public FileSystemContentAddressedStore(
        string rootDirectory,
        long maximumArtifactBytes)
    {
        _rootDirectory =
            rootDirectory;

        _maximumArtifactBytes =
            maximumArtifactBytes;
    }

    #endregion

    #region Methods Write

    public async ValueTask<ContentAddressedFile> StoreAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "Content-addressed artifact input must be readable.",
                nameof(content));
        }

        var stagingDirectory =
            Path.Combine(
                _rootDirectory,
                ".staging");

        Directory.CreateDirectory(
            stagingDirectory);

        var temporaryPath =
            Path.Combine(
                stagingDirectory,
                $"{Guid.NewGuid():N}.tmp");

        try
        {
            ContentAddressedFile artifact;

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

    private async ValueTask<ContentAddressedFile> CopyAndHashAsync(
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
                    _maximumArtifactBytes)
                {
                    throw new InvalidDataException(
                        $"Artifact exceeds the configured {_maximumArtifactBytes}-byte storage limit.");
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
                "Content-addressed artifact cannot be empty.");
        }

        return new ContentAddressedFile(
            new Sha256Digest(
                Convert.ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant()),
            byteLength);
    }

    #endregion

    #region Methods Read

    public async ValueTask<bool> VerifyAsync(
        ContentAddressedFile artifact,
        CancellationToken cancellationToken)
    {
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

    public async ValueTask<Stream> OpenReadAsync(
        ContentAddressedFile artifact,
        CancellationToken cancellationToken)
    {
        var artifactPath =
            GetArtifactPath(
                artifact.Digest);

        if (!File.Exists(
                artifactPath))
        {
            throw new ContentAddressedFileIntegrityException(
                artifact.Digest,
                $"Content-addressed artifact '{artifact.Digest}' is missing.");
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
                throw new ContentAddressedFileIntegrityException(
                    artifact.Digest,
                    $"Content-addressed artifact '{artifact.Digest}' failed integrity verification.");
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
        ContentAddressedFile artifact,
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
        ContentAddressedFile artifact,
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
            throw new ContentAddressedFileIntegrityException(
                artifact.Digest,
                $"Existing content-addressed artifact '{artifact.Digest}' does not match its expected bytes.");
        }
    }

    #endregion

    #region Methods Delete

    public ValueTask DeleteAsync(
        Sha256Digest digest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var artifactPath = GetArtifactPath(digest);
        if (!File.Exists(artifactPath))
        {
            return ValueTask.CompletedTask;
        }

        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(artifactPath, FileAttributes.Normal);
        }

        File.Delete(artifactPath);
        TryDeleteEmptyDirectory(Path.GetDirectoryName(artifactPath));
        return ValueTask.CompletedTask;
    }

    private static void TryDeleteEmptyDirectory(string? directory)
    {
        if (directory is null || !Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return;
        }

        try
        {
            Directory.Delete(directory);
        }
        catch (IOException)
        {
            // Empty prefix cleanup is best effort; the artifact itself is gone.
        }
        catch (UnauthorizedAccessException)
        {
            // Empty prefix cleanup is best effort; the artifact itself is gone.
        }
    }

    #endregion

    #region Methods Paths

    private string GetArtifactPath(
        Sha256Digest digest) =>
        Path.Combine(
            _rootDirectory,
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
            // Best-effort cleanup must not hide the primary storage failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup must not hide the primary storage failure.
        }
    }

    #endregion
}
