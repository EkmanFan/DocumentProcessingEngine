using System.Buffers;
using System.Security.Cryptography;
using DocumentProcessing.Core.DualRun.Transport;

namespace DocumentProcessing.Engine.DualRun.Isolation;

/// <summary>
/// Materializes an independent immutable source.bin for one isolated Dual Run
/// job.
///
/// Construction performs no file-system I/O. The spool root is created only
/// when CreateAsync is actually invoked, preserving the Disabled profile's
/// no-spool-I/O requirement at composition time.
/// </summary>
public sealed class DocumentDualRunSourceSnapshotFactory
{
    #region Variables and Constants

    private const int BufferSize =
        81920;

    private const string PartialSourceFileName =
        "source.bin.partial";

    private const int JobDirectoryCreationAttempts =
        8;

    private static readonly UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute;

    private static readonly UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite;

    private readonly string _spoolRootPath;

    #endregion

    #region Properties

    public string SpoolRootPath =>
        _spoolRootPath;

    #endregion

    #region ctor

    public DocumentDualRunSourceSnapshotFactory(
        string spoolRootPath)
    {
        if (string.IsNullOrWhiteSpace(
                spoolRootPath))
        {
            throw new ArgumentException(
                "Dual Run spool root cannot be empty.",
                nameof(spoolRootPath));
        }

        if (!Path.IsPathFullyQualified(
                spoolRootPath))
        {
            throw new ArgumentException(
                "Dual Run spool root must be fully qualified.",
                nameof(spoolRootPath));
        }

        _spoolRootPath =
            Path.GetFullPath(
                spoolRootPath);
    }

    #endregion

    #region Methods Creation

    public async ValueTask<DocumentDualRunSourceSnapshot> CreateAsync(
        Guid jobId,
        Stream source,
        string expectedSourceDocumentSha256,
        long expectedSourceByteLength,
        CancellationToken cancellationToken = default)
    {
        if (jobId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Dual Run job ID cannot be empty.",
                nameof(jobId));
        }

        ArgumentNullException.ThrowIfNull(
            source);

        if (!source.CanRead)
        {
            throw new ArgumentException(
                "Dual Run source stream must be readable.",
                nameof(source));
        }

        var expectedSha256 =
            NormalizeSha256(
                expectedSourceDocumentSha256,
                nameof(expectedSourceDocumentSha256));

        if (expectedSourceByteLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedSourceByteLength));
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var originalPosition =
            source.CanSeek
                ? source.Position
                : (long?)null;

        string? jobDirectoryPath =
            null;

        var completed =
            false;

        try
        {
            if (source.CanSeek)
            {
                source.Position =
                    0;
            }

            EnsureSpoolRoot();

            jobDirectoryPath =
                CreatePrivateJobDirectory(
                    jobId);

            var partialSourcePath =
                Path.Combine(
                    jobDirectoryPath,
                    PartialSourceFileName);

            var sourceSnapshotPath =
                Path.Combine(
                    jobDirectoryPath,
                    DocumentDualRunTransportSchema
                        .SourceSnapshotFileName);

            var observedIdentity =
                await CopyAndHashAsync(
                        source,
                        partialSourcePath,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (observedIdentity.ByteLength !=
                expectedSourceByteLength)
            {
                throw new InvalidDataException(
                    $"Dual Run source snapshot byte length " +
                    $"{observedIdentity.ByteLength} does not match expected " +
                    $"{expectedSourceByteLength}.");
            }

            if (!string.Equals(
                    observedIdentity.Sha256,
                    expectedSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Dual Run source snapshot SHA-256 " +
                    $"'{observedIdentity.Sha256}' does not match expected " +
                    $"'{expectedSha256}'.");
            }

            File.Move(
                partialSourcePath,
                sourceSnapshotPath);

            if (originalPosition.HasValue)
            {
                source.Position =
                    originalPosition.Value;
            }

            var snapshot =
                new DocumentDualRunSourceSnapshot(
                    jobId,
                    jobDirectoryPath,
                    sourceSnapshotPath,
                    observedIdentity.Sha256,
                    observedIdentity.ByteLength);

            completed =
                true;

            return snapshot;
        }
        catch
        {
            if (originalPosition.HasValue)
            {
                try
                {
                    source.Position =
                        originalPosition.Value;
                }
                catch
                {
                    // Preserve the original snapshot-creation failure.
                }
            }

            throw;
        }
        finally
        {
            if (!completed &&
                jobDirectoryPath is not null)
            {
                DeleteDirectoryBestEffort(
                    jobDirectoryPath);
            }
        }
    }

    #endregion

    #region Methods Spool Paths

    private void EnsureSpoolRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(
                _spoolRootPath);

            return;
        }

        Directory.CreateDirectory(
            _spoolRootPath,
            PrivateDirectoryMode);
    }

    private string CreatePrivateJobDirectory(
        Guid jobId)
    {
        for (var attempt = 0;
             attempt <
             JobDirectoryCreationAttempts;
             attempt++)
        {
            var candidate =
                Path.Combine(
                    _spoolRootPath,
                    $"job-{jobId:N}-{Guid.NewGuid():N}");

            if (Directory.Exists(
                    candidate) ||
                File.Exists(
                    candidate))
            {
                continue;
            }

            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(
                    candidate);
            }
            else
            {
                Directory.CreateDirectory(
                    candidate,
                    PrivateDirectoryMode);
            }

            return candidate;
        }

        throw new IOException(
            $"Unable to allocate a private Dual Run job directory after " +
            $"{JobDirectoryCreationAttempts} attempts.");
    }

    #endregion

    #region Methods Stream and Hash

    private static async ValueTask<SourceByteIdentity> CopyAndHashAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var options =
            new FileStreamOptions
            {
                Mode =
                    FileMode.CreateNew,
                Access =
                    FileAccess.Write,
                Share =
                    FileShare.None,
                BufferSize =
                    BufferSize,
                Options =
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan
            };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode =
                PrivateFileMode;
        }

        await using var destination =
            new FileStream(
                destinationPath,
                options);

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
                cancellationToken
                    .ThrowIfCancellationRequested();

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

                await destination
                    .WriteAsync(
                        buffer.AsMemory(
                            0,
                            read),
                        cancellationToken)
                    .ConfigureAwait(false);

                byteLength =
                    checked(
                        byteLength +
                        read);
            }

            await destination
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new SourceByteIdentity(
                Convert
                    .ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant(),
                byteLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                buffer);
        }
    }

    private static string NormalizeSha256(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Dual Run expected source SHA-256 cannot be empty.",
                parameterName);
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Dual Run expected source SHA-256 must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }

    #endregion

    #region Methods Cleanup

    private static void DeleteDirectoryBestEffort(
        string path)
    {
        try
        {
            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    recursive:
                        true);
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    #endregion

    #region Internal Types

    private readonly record struct SourceByteIdentity(
        string Sha256,
        long ByteLength);

    #endregion
}
