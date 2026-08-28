using System.Security.Cryptography;
using System.Text.Json;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Persistence.Files;

/// <summary>
/// Filesystem adapter that publishes one custody-verified visual directory per
/// completed processing unit.
/// </summary>
public sealed class FileSystemProcessingVisualAssetStore
    : IProcessingVisualAssetStore
{
    #region Variables and Constants

    /// <summary>Gets the default maximum size of one visual.</summary>
    public const long DefaultMaximumVisualBytes =
        64L *
        1024 *
        1024;

    /// <summary>Gets the default maximum total size of one visual set.</summary>
    public const long DefaultMaximumVisualSetBytes =
        2L *
        1024 *
        1024 *
        1024;

    private readonly long
        _maximumVisualBytes;

    private readonly long
        _maximumVisualSetBytes;

    #endregion

    #region ctor

    /// <summary>Creates the completed-visual filesystem adapter.</summary>
    public FileSystemProcessingVisualAssetStore(
        long maximumVisualBytes = DefaultMaximumVisualBytes,
        long maximumVisualSetBytes = DefaultMaximumVisualSetBytes)
    {
        if (maximumVisualBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumVisualBytes));
        }

        if (maximumVisualSetBytes < maximumVisualBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumVisualSetBytes),
                "Visual-set limit cannot be smaller than the per-visual limit.");
        }

        _maximumVisualBytes =
            maximumVisualBytes;

        _maximumVisualSetBytes =
            maximumVisualSetBytes;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask ValidateRootAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        var root =
            NormalizeExistingRoot(
                rootDirectory);

        cancellationToken.ThrowIfCancellationRequested();

        var probePath =
            Path.Combine(
                root,
                $".dpengine-write-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            await using var probe =
                new FileStream(
                    probePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize:
                        1,
                    FileOptions.Asynchronous |
                    FileOptions.WriteThrough);

            await probe
                .WriteAsync(
                    new byte[] { 1 },
                    cancellationToken)
                .ConfigureAwait(false);

            await probe
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            probe.Flush(
                flushToDisk:
                    true);
        }
        finally
        {
            TryDeleteFile(
                probePath);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IProcessingVisualAssetWriteSession> BeginWriteAsync(
        string rootDirectory,
        ProcessingUnitId unitId,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        if (unitId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(unitId));
        }

        if (string.IsNullOrWhiteSpace(
                originalFileName))
        {
            throw new ArgumentException(
                "Original filename cannot be empty.",
                nameof(originalFileName));
        }

        await ValidateRootAsync(
                rootDirectory,
                cancellationToken)
            .ConfigureAwait(false);

        var root =
            NormalizeExistingRoot(
                rootDirectory);

        return new WriteSession(
            root,
            unitId,
            originalFileName,
            _maximumVisualBytes,
            _maximumVisualSetBytes);
    }

    private static string NormalizeExistingRoot(
        string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                rootDirectory) ||
            !Path.IsPathFullyQualified(
                rootDirectory))
        {
            throw new ArgumentException(
                "Visual destination must be an absolute directory path.",
                nameof(rootDirectory));
        }

        var root =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    rootDirectory.Trim()));

        if (string.Equals(
                root,
                Path.GetPathRoot(
                    root),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Visual destination cannot be a filesystem root.",
                nameof(rootDirectory));
        }

        if (!Directory.Exists(
                root))
        {
            throw new DirectoryNotFoundException(
                $"Visual destination does not exist: {root}");
        }

        return root;
    }

    private static void TryDeleteFile(
        string path)
    {
        try
        {
            if (File.Exists(
                    path))
            {
                File.SetAttributes(
                    path,
                    FileAttributes.Normal);

                File.Delete(
                    path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    #endregion

    #region Nested Types

    private sealed class WriteSession
        : IProcessingVisualAssetWriteSession
    {
        #region Variables and Constants

        private const string
            ManifestFileName =
                "visual-assets.manifest.json";

        private static readonly JsonSerializerOptions
            ManifestJsonOptions =
                new()
                {
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase,
                    WriteIndented =
                        true
                };

        private readonly object
            _sync =
                new();

        private readonly string
            _stagingDirectory;

        private readonly string
            _completedDirectory;

        private readonly long
            _maximumVisualBytes;

        private readonly long
            _maximumVisualSetBytes;

        private readonly List<StagedWrite>
            _writes =
                [];

        private bool
            _writesClosed;

        private bool
            _completed;

        private bool
            _disposed;

        #endregion

        #region ctor

        public WriteSession(
            string rootDirectory,
            ProcessingUnitId unitId,
            string originalFileName,
            long maximumVisualBytes,
            long maximumVisualSetBytes)
        {
            var stagingRoot =
                Path.Combine(
                    rootDirectory,
                    ".dpengine-staging");

            _stagingDirectory =
                Path.Combine(
                    stagingRoot,
                    $"{unitId.Value:N}-{Guid.NewGuid():N}");

            _completedDirectory =
                Path.Combine(
                    rootDirectory,
                    CreateCompletedDirectoryName(
                        originalFileName,
                        unitId));

            _maximumVisualBytes =
                maximumVisualBytes;

            _maximumVisualSetBytes =
                maximumVisualSetBytes;
        }

        #endregion

        #region Methods

        public ValueTask<Stream> OpenWriteAsync(
            string mediaType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(
                    mediaType) ||
                !mediaType.Trim()
                    .StartsWith(
                        "image/",
                        StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Staged visual media type must be an image media type.",
                    nameof(mediaType));
            }

            lock (_sync)
            {
                ThrowIfUnavailable();

                Directory.CreateDirectory(
                    _stagingDirectory);

                var path =
                    Path.Combine(
                        _stagingDirectory,
                        $"write-{_writes.Count + 1:D4}-{Guid.NewGuid():N}.tmp");

                var stream =
                    new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize:
                            128 * 1024,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan |
                        FileOptions.WriteThrough);

                _writes.Add(
                    new StagedWrite(
                        path,
                        mediaType.Trim()
                            .ToLowerInvariant(),
                        stream));

                return ValueTask.FromResult<Stream>(
                    stream);
            }
        }

        public async ValueTask<string> CompleteAsync(
            IReadOnlyList<ProcessingVisualAssetDescriptor> assets,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                assets);

            lock (_sync)
            {
                ThrowIfUnavailable();

                _writesClosed =
                    true;
            }

            await CloseWritesAsync()
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (assets.Any(
                    asset =>
                        asset is null) ||
                assets.Select(
                        asset =>
                            asset.AssetId)
                    .Distinct(
                        StringComparer.Ordinal)
                    .Count() !=
                assets.Count)
            {
                throw new InvalidDataException(
                    "Completed visual assets must be non-null with unique identifiers.");
            }

            if (_writes.Count !=
                assets.Count)
            {
                throw new InvalidDataException(
                    $"DPEngine declared {assets.Count} visual assets after writing {_writes.Count} destinations.");
            }

            Directory.CreateDirectory(
                _stagingDirectory);

            var staged =
                new List<StagedEvidence>(
                    _writes.Count);

            long totalBytes =
                0;

            foreach (var write in _writes)
            {
                var evidence =
                    await ReadEvidenceAsync(
                            write,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (evidence.ByteLength >
                    _maximumVisualBytes)
                {
                    throw new InvalidDataException(
                        $"Visual asset exceeds the configured {_maximumVisualBytes}-byte limit.");
                }

                totalBytes =
                    checked(
                        totalBytes +
                        evidence.ByteLength);

                if (totalBytes >
                    _maximumVisualSetBytes)
                {
                    throw new InvalidDataException(
                        $"Visual asset set exceeds the configured {_maximumVisualSetBytes}-byte limit.");
                }

                staged.Add(
                    evidence);
            }

            var manifestAssets =
                MatchAndNameAssets(
                    assets,
                    staged);

            await WriteManifestAsync(
                    manifestAssets,
                    cancellationToken)
                .ConfigureAwait(false);

            MakeStagedFilesReadOnly();

            if (Directory.Exists(
                    _completedDirectory))
            {
                await ValidateCompletedDirectoryAsync(
                        manifestAssets,
                        cancellationToken)
                    .ConfigureAwait(false);

                _completed =
                    true;

                TryDeleteDirectory(
                    _stagingDirectory);

                return _completedDirectory;
            }

            try
            {
                Directory.Move(
                    _stagingDirectory,
                    _completedDirectory);
            }
            catch (IOException)
                when (Directory.Exists(
                    _completedDirectory))
            {
                await ValidateCompletedDirectoryAsync(
                        manifestAssets,
                        cancellationToken)
                    .ConfigureAwait(false);

                TryDeleteDirectory(
                    _stagingDirectory);
            }

            _completed =
                true;

            return _completedDirectory;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            await CloseWritesAsync()
                .ConfigureAwait(false);

            if (!_completed)
            {
                TryDeleteDirectory(
                    _stagingDirectory);
            }
        }

        private async ValueTask CloseWritesAsync()
        {
            foreach (var write in _writes)
            {
                await write.Stream
                    .DisposeAsync()
                    .ConfigureAwait(false);
            }
        }

        private async ValueTask<StagedEvidence> ReadEvidenceAsync(
            StagedWrite write,
            CancellationToken cancellationToken)
        {
            await using var content =
                new FileStream(
                    write.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize:
                        128 * 1024,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

            var digest =
                Convert.ToHexString(
                        await SHA256
                            .HashDataAsync(
                                content,
                                cancellationToken)
                            .ConfigureAwait(false))
                    .ToLowerInvariant();

            return new StagedEvidence(
                write.Path,
                write.MediaType,
                content.Length,
                digest);
        }

        private IReadOnlyList<ManifestAsset> MatchAndNameAssets(
            IReadOnlyList<ProcessingVisualAssetDescriptor> assets,
            IReadOnlyList<StagedEvidence> staged)
        {
            var unmatched =
                staged.ToList();

            var manifestAssets =
                new List<ManifestAsset>(
                    assets.Count);

            for (var index = 0;
                 index < assets.Count;
                 index++)
            {
                var asset =
                    assets[index];

                var matchIndex =
                    unmatched.FindIndex(
                        candidate =>
                            candidate.ByteLength ==
                            asset.ByteLength &&
                            string.Equals(
                                candidate.Digest,
                                asset.Digest.Value,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                candidate.MediaType,
                                asset.MediaType,
                                StringComparison.Ordinal));

                if (matchIndex < 0)
                {
                    throw new InvalidDataException(
                        $"Staged bytes do not match visual asset '{asset.AssetId}'.");
                }

                var match =
                    unmatched[matchIndex];

                unmatched.RemoveAt(
                    matchIndex);

                var fileName =
                    $"{index + 1:D4}-{SanitizeName(asset.AssetId)}{GetExtension(asset.MediaType)}";

                File.Move(
                    match.Path,
                    Path.Combine(
                        _stagingDirectory,
                        fileName));

                manifestAssets.Add(
                    new ManifestAsset(
                        asset.AssetId,
                        fileName,
                        asset.MediaType,
                        asset.ByteLength,
                        asset.Digest.Value));
            }

            return manifestAssets;
        }

        private async ValueTask WriteManifestAsync(
            IReadOnlyList<ManifestAsset> assets,
            CancellationToken cancellationToken)
        {
            var path =
                Path.Combine(
                    _stagingDirectory,
                    ManifestFileName);

            await using var stream =
                new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize:
                        16 * 1024,
                    FileOptions.Asynchronous |
                    FileOptions.WriteThrough);

            await JsonSerializer
                .SerializeAsync(
                    stream,
                    new VisualAssetManifest(
                        "manager-visual-assets-v1",
                        assets),
                    ManifestJsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            await stream
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            stream.Flush(
                flushToDisk:
                    true);
        }

        private async ValueTask ValidateCompletedDirectoryAsync(
            IReadOnlyList<ManifestAsset> assets,
            CancellationToken cancellationToken)
        {
            var expectedFiles =
                assets.Select(
                        asset =>
                            asset.FileName)
                    .Append(
                        ManifestFileName)
                    .ToHashSet(
                        StringComparer.Ordinal);

            var actualFiles =
                Directory.EnumerateFiles(
                        _completedDirectory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Select(
                        Path.GetFileName)
                    .ToHashSet(
                        StringComparer.Ordinal);

            if (!actualFiles.SetEquals(
                    expectedFiles))
            {
                throw new InvalidDataException(
                    $"Completed visual directory conflicts with processing unit output: {_completedDirectory}");
            }

            var expectedManifest =
                await File.ReadAllBytesAsync(
                        Path.Combine(
                            _stagingDirectory,
                            ManifestFileName),
                        cancellationToken)
                    .ConfigureAwait(false);

            var completedManifest =
                await File.ReadAllBytesAsync(
                        Path.Combine(
                            _completedDirectory,
                            ManifestFileName),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!expectedManifest.AsSpan()
                    .SequenceEqual(
                        completedManifest))
            {
                throw new InvalidDataException(
                    $"Completed visual manifest conflicts with processing unit output: {_completedDirectory}");
            }

            foreach (var asset in assets)
            {
                var path =
                    Path.Combine(
                        _completedDirectory,
                        asset.FileName);

                await using var content =
                    new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize:
                            128 * 1024,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

                var digest =
                    Convert.ToHexString(
                            await SHA256
                                .HashDataAsync(
                                    content,
                                    cancellationToken)
                                .ConfigureAwait(false))
                        .ToLowerInvariant();

                if (content.Length !=
                        asset.ByteLength ||
                    !string.Equals(
                        digest,
                        asset.Sha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Completed visual '{asset.FileName}' failed custody verification.");
                }
            }
        }

        private void MakeStagedFilesReadOnly()
        {
            foreach (var path in Directory.EnumerateFiles(
                         _stagingDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                File.SetAttributes(
                    path,
                    File.GetAttributes(
                        path) |
                    FileAttributes.ReadOnly);
            }
        }

        private void ThrowIfUnavailable()
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            if (_writesClosed)
            {
                throw new InvalidOperationException(
                    "Visual write session no longer accepts destinations.");
            }
        }

        private static string CreateCompletedDirectoryName(
            string originalFileName,
            ProcessingUnitId unitId)
        {
            var leafName =
                Path.GetFileName(
                    originalFileName.Trim()
                        .Replace(
                            '\\',
                            '/'));

            var title =
                Path.GetFileNameWithoutExtension(
                    leafName);

            return $"{SanitizeName(title)}--{unitId.Value:N}";
        }

        private static string SanitizeName(
            string value)
        {
            var invalid =
                Path.GetInvalidFileNameChars()
                    .ToHashSet();

            var normalized =
                new string(
                    value.Trim()
                        .Select(
                            character =>
                                invalid.Contains(
                                    character) ||
                                char.IsControl(
                                    character) ||
                                character is ':' or '/' or '\\'
                                    ? '-'
                                    : character)
                        .ToArray())
                    .Trim(
                        ' ',
                        '.',
                        '-');

            if (string.IsNullOrWhiteSpace(
                    normalized))
            {
                normalized =
                    "document";
            }

            return normalized.Length <= 80
                ? normalized
                : normalized[..80]
                    .TrimEnd(
                        ' ',
                        '.',
                        '-');
        }

        private static string GetExtension(
            string mediaType) =>
            mediaType switch
            {
                "image/png" =>
                    ".png",
                "image/jpeg" =>
                    ".jpg",
                "image/gif" =>
                    ".gif",
                "image/webp" =>
                    ".webp",
                "image/svg+xml" =>
                    ".svg",
                "image/bmp" =>
                    ".bmp",
                "image/tiff" =>
                    ".tiff",
                _ =>
                    ".image"
            };

        private static void TryDeleteDirectory(
            string path)
        {
            try
            {
                if (!Directory.Exists(
                        path))
                {
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(
                             path,
                             "*",
                             SearchOption.AllDirectories))
                {
                    File.SetAttributes(
                        file,
                        FileAttributes.Normal);
                }

                Directory.Delete(
                    path,
                    recursive:
                        true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        #endregion

        #region Nested Records

        private sealed record StagedWrite(
            string Path,
            string MediaType,
            FileStream Stream);

        private sealed record StagedEvidence(
            string Path,
            string MediaType,
            long ByteLength,
            string Digest);

        private sealed record VisualAssetManifest(
            string SchemaVersion,
            IReadOnlyList<ManifestAsset> Assets);

        private sealed record ManifestAsset(
            string AssetId,
            string FileName,
            string MediaType,
            long ByteLength,
            string Sha256);

        #endregion
    }

    #endregion
}
