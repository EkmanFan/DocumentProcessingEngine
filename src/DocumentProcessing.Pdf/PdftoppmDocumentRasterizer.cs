using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Linux/Poppler PDF raster execution adapter.
///
/// One opened session materializes the source PDF exactly once into a private
/// temporary directory, then reuses that immutable copy for full-page and
/// direct-region rendering. This avoids copying a large PDF once per OCR/layout
/// region.
///
/// Poppler process execution is an implementation detail. Callers interact only
/// with the engine-owned raster contracts and caller-owned destination streams.
/// </summary>
public sealed class PdftoppmDocumentRasterizer
    : IDocumentRasterizer
{
    public const string BackendId =
        "pdftoppm";

    public const int DefaultDpi =
        300;

    public const long DefaultMaxSourceBytes =
        4L * 1024L * 1024L * 1024L;

    public const long DefaultMaxOutputBytes =
        64L * 1024L * 1024L;

    public static readonly TimeSpan DefaultRenderTimeout =
        TimeSpan.FromSeconds(
            90);

    private const int BufferSize =
        81920;

    private readonly string _executablePath;
    private readonly int _dpi;
    private readonly long _maxSourceBytes;
    private readonly long _maxOutputBytes;
    private readonly TimeSpan _renderTimeout;

    public PdftoppmDocumentRasterizer(
        string executablePath = "pdftoppm",
        int dpi = DefaultDpi,
        long maxSourceBytes = DefaultMaxSourceBytes,
        long maxOutputBytes = DefaultMaxOutputBytes,
        TimeSpan? renderTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(
                executablePath))
        {
            throw new ArgumentException(
                "pdftoppm executable path cannot be empty.",
                nameof(executablePath));
        }

        if (dpi <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi));
        }

        if (maxSourceBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSourceBytes));
        }

        if (maxOutputBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputBytes));
        }

        _renderTimeout =
            renderTimeout ??
            DefaultRenderTimeout;

        if (_renderTimeout <=
                TimeSpan.Zero ||
            _renderTimeout ==
                Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderTimeout),
                _renderTimeout,
                "Render timeout must be finite and greater than zero.");
        }

        _executablePath =
            executablePath.Trim();

        _dpi =
            dpi;

        _maxSourceBytes =
            maxSourceBytes;

        _maxOutputBytes =
            maxOutputBytes;
    }

    public bool CanRasterize(
        DocumentFormatId format) =>
        format ==
        DocumentFormatId.Pdf;

    public async ValueTask<IDocumentRasterizationSession> OpenAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (!CanRasterize(
                format))
        {
            throw new NotSupportedException(
                $"pdftoppm rasterization does not support format '{format}'.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                $"document-processing-raster-{Path.GetRandomFileName()}");

        Directory.CreateDirectory(
            temporaryDirectory);

        var sourcePath =
            Path.Combine(
                temporaryDirectory,
                "source.pdf");

        try
        {
            await MaterializeSourceAsync(
                    source.Content,
                    sourcePath,
                    cancellationToken)
                .ConfigureAwait(false);

            return new Session(
                temporaryDirectory,
                sourcePath,
                _executablePath,
                _dpi,
                _maxOutputBytes,
                _renderTimeout);
        }
        catch
        {
            TryDeleteDirectory(
                temporaryDirectory);

            throw;
        }
    }

    private async ValueTask MaterializeSourceAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        long? originalPosition =
            null;

        if (source.CanSeek)
        {
            originalPosition =
                source.Position;

            source.Position =
                0;
        }

        try
        {
            await using var destination =
                new FileStream(
                    destinationPath,
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
                    });

            var buffer =
                ArrayPool<byte>.Shared.Rent(
                    BufferSize);

            long total =
                0;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                    total =
                        checked(
                            total +
                            read);

                    if (total >
                        _maxSourceBytes)
                    {
                        throw new InvalidDataException(
                            $"Document source exceeds the {_maxSourceBytes}-byte rasterization limit.");
                    }

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
                    buffer);
            }

            if (total ==
                0)
            {
                throw new InvalidDataException(
                    "Document source is empty.");
            }

            await destination
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                source.Position =
                    originalPosition.Value;
            }
        }
    }

    private static void TryDeleteDirectory(
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
        catch
        {
            // Cleanup must never hide the original processing exception.
        }
    }

    private sealed class Session
        : IDocumentRasterizationSession
    {
        private const int MaxDiagnosticCharacters =
            64 * 1024;

        private readonly string _temporaryDirectory;
        private readonly string _sourcePath;
        private readonly string _executablePath;
        private readonly long _maxOutputBytes;
        private readonly TimeSpan _renderTimeout;
        private bool _disposed;

        public Session(
            string temporaryDirectory,
            string sourcePath,
            string executablePath,
            int dpi,
            long maxOutputBytes,
            TimeSpan renderTimeout)
        {
            _temporaryDirectory =
                temporaryDirectory;

            _sourcePath =
                sourcePath;

            _executablePath =
                executablePath;

            Dpi =
                dpi;

            _maxOutputBytes =
                maxOutputBytes;

            _renderTimeout =
                renderTimeout;

            ProfileId =
                $"pdftoppm-{dpi.ToString(CultureInfo.InvariantCulture)}dpi-rgb-png-direct-crop-v1";
        }

        public string BackendId =>
            PdftoppmDocumentRasterizer.BackendId;

        public string ProfileId { get; }

        public int Dpi { get; }

        public ValueTask<RasterRenderResult> RenderPageAsync(
            int physicalPageNumber,
            Stream destination,
            CancellationToken cancellationToken = default) =>
            RenderAsync(
                physicalPageNumber,
                sourcePagePixelWidth:
                    null,
                sourcePagePixelHeight:
                    null,
                crop:
                    null,
                destination,
                cancellationToken);

        public ValueTask<RasterRenderResult> RenderRegionAsync(
            int physicalPageNumber,
            int sourcePagePixelWidth,
            int sourcePagePixelHeight,
            PixelRectangle crop,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if (sourcePagePixelWidth <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourcePagePixelWidth));
            }

            if (sourcePagePixelHeight <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourcePagePixelHeight));
            }

            if (crop.Right >
                    sourcePagePixelWidth ||
                crop.Bottom >
                    sourcePagePixelHeight)
            {
                throw new ArgumentException(
                    "Raster crop must remain inside the source page raster.",
                    nameof(crop));
            }

            return RenderAsync(
                physicalPageNumber,
                sourcePagePixelWidth,
                sourcePagePixelHeight,
                crop,
                destination,
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed =
                true;

            TryDeleteDirectory(
                _temporaryDirectory);

            return ValueTask.CompletedTask;
        }

        private async ValueTask<RasterRenderResult> RenderAsync(
            int physicalPageNumber,
            int? sourcePagePixelWidth,
            int? sourcePagePixelHeight,
            PixelRectangle? crop,
            Stream destination,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(
                destination);

            if (physicalPageNumber <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalPageNumber));
            }

            if (!destination.CanWrite)
            {
                throw new ArgumentException(
                    "Raster destination stream must be writable.",
                    nameof(destination));
            }

            if (destination.CanSeek &&
                (destination.Position !=
                     0 ||
                 destination.Length !=
                     0))
            {
                throw new ArgumentException(
                    "Seekable raster destinations must be empty and positioned at zero.",
                    nameof(destination));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var outputPrefix =
                Path.Combine(
                    _temporaryDirectory,
                    $"render-{physicalPageNumber:D6}-{Guid.NewGuid():N}");

            var outputPath =
                outputPrefix +
                ".png";

            try
            {
                await RunPdftoppmAsync(
                        physicalPageNumber,
                        crop,
                        outputPrefix,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!File.Exists(
                        outputPath))
                {
                    throw new InvalidDataException(
                        $"pdftoppm produced no PNG for physical page {physicalPageNumber}.");
                }

                var fileInfo =
                    new FileInfo(
                        outputPath);

                if (fileInfo.Length <=
                    0)
                {
                    throw new InvalidDataException(
                        $"pdftoppm produced an empty PNG for physical page {physicalPageNumber}.");
                }

                if (fileInfo.Length >
                    _maxOutputBytes)
                {
                    throw new InvalidDataException(
                        $"Raster output exceeds the {_maxOutputBytes}-byte limit.");
                }

                var dimensions =
                    await ReadPngDimensionsAsync(
                            outputPath,
                            cancellationToken)
                        .ConfigureAwait(false);

                int resolvedSourceWidth;
                int resolvedSourceHeight;

                if (crop is null)
                {
                    resolvedSourceWidth =
                        dimensions.Width;

                    resolvedSourceHeight =
                        dimensions.Height;
                }
                else
                {
                    resolvedSourceWidth =
                        sourcePagePixelWidth!.Value;

                    resolvedSourceHeight =
                        sourcePagePixelHeight!.Value;

                    if (dimensions.Width !=
                            crop.Value.Width ||
                        dimensions.Height !=
                            crop.Value.Height)
                    {
                        throw new InvalidDataException(
                            $"pdftoppm crop dimensions {dimensions.Width}x{dimensions.Height} " +
                            $"do not match requested crop {crop.Value.Width}x{crop.Value.Height}.");
                    }
                }

                var copied =
                    await CopyAndHashAsync(
                            outputPath,
                            destination,
                            cancellationToken)
                        .ConfigureAwait(false);

                return new RasterRenderResult(
                    physicalPageNumber,
                    resolvedSourceWidth,
                    resolvedSourceHeight,
                    crop,
                    dimensions.Width,
                    dimensions.Height,
                    "image/png",
                    ProfileId,
                    copied.ContentLength,
                    copied.Sha256);
            }
            catch
            {
                if (destination.CanSeek)
                {
                    destination.SetLength(
                        0);

                    destination.Position =
                        0;
                }

                throw;
            }
            finally
            {
                try
                {
                    File.Delete(
                        outputPath);
                }
                catch
                {
                    // Session disposal is the final cleanup boundary.
                }
            }
        }

        private async Task RunPdftoppmAsync(
            int physicalPageNumber,
            PixelRectangle? crop,
            string outputPrefix,
            CancellationToken cancellationToken)
        {
            var startInfo =
                new ProcessStartInfo
                {
                    FileName =
                        _executablePath,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true,

                    UseShellExecute =
                        false,

                    CreateNoWindow =
                        true
                };

            foreach (var argument in
                     BuildArguments(
                         physicalPageNumber,
                         crop,
                         outputPrefix))
            {
                startInfo.ArgumentList.Add(
                    argument);
            }

            using var process =
                new Process
                {
                    StartInfo =
                        startInfo
                };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Could not start pdftoppm.");
                }
            }
            catch (Exception exception)
                when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Could not start pdftoppm executable '{_executablePath}'.",
                    exception);
            }

            var stdoutTask =
                ReadBoundedAsync(
                    process.StandardOutput,
                    MaxDiagnosticCharacters);

            var stderrTask =
                ReadBoundedAsync(
                    process.StandardError,
                    MaxDiagnosticCharacters);

            using var timeoutSource =
                new CancellationTokenSource(
                    _renderTimeout);

            using var linkedSource =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);

            try
            {
                await process
                    .WaitForExitAsync(
                        linkedSource.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                TryKill(
                    process);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                if (timeoutSource.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"pdftoppm rendering exceeded {_renderTimeout}.",
                        exception);
                }

                throw;
            }

            var stdout =
                await stdoutTask
                    .ConfigureAwait(false);

            var stderr =
                await stderrTask
                    .ConfigureAwait(false);

            if (process.ExitCode !=
                0)
            {
                throw new InvalidDataException(
                    $"pdftoppm failed for physical page {physicalPageNumber} " +
                    $"with exit code {process.ExitCode}: {stderr}{stdout}");
            }
        }

        private IEnumerable<string> BuildArguments(
            int physicalPageNumber,
            PixelRectangle? crop,
            string outputPrefix)
        {
            yield return "-f";
            yield return physicalPageNumber.ToString(
                CultureInfo.InvariantCulture);

            yield return "-l";
            yield return physicalPageNumber.ToString(
                CultureInfo.InvariantCulture);

            yield return "-singlefile";

            yield return "-r";
            yield return Dpi.ToString(
                CultureInfo.InvariantCulture);

            if (crop is { } region)
            {
                yield return "-x";
                yield return region.Left.ToString(
                    CultureInfo.InvariantCulture);

                yield return "-y";
                yield return region.Top.ToString(
                    CultureInfo.InvariantCulture);

                yield return "-W";
                yield return region.Width.ToString(
                    CultureInfo.InvariantCulture);

                yield return "-H";
                yield return region.Height.ToString(
                    CultureInfo.InvariantCulture);
            }

            yield return "-png";
            yield return _sourcePath;
            yield return outputPrefix;
        }

        private static async Task<(int Width, int Height)> ReadPngDimensionsAsync(
            string path,
            CancellationToken cancellationToken)
        {
            await using var stream =
                new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode =
                            FileMode.Open,
                        Access =
                            FileAccess.Read,
                        Share =
                            FileShare.Read,
                        BufferSize =
                            4096,
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                    });

            var header =
                new byte[24];

            var offset =
                0;

            while (offset <
                   header.Length)
            {
                var read =
                    await stream
                        .ReadAsync(
                            header.AsMemory(
                                offset,
                                header.Length -
                                offset),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    throw new InvalidDataException(
                        "PNG header is incomplete.");
                }

                offset +=
                    read;
            }

            ReadOnlySpan<byte> signature =
            [
                137,
                80,
                78,
                71,
                13,
                10,
                26,
                10
            ];

            if (!header
                    .AsSpan(
                        0,
                        8)
                    .SequenceEqual(
                        signature))
            {
                throw new InvalidDataException(
                    "pdftoppm output is not a PNG file.");
            }

            var width =
                ReadBigEndianInt32(
                    header.AsSpan(
                        16,
                        4));

            var height =
                ReadBigEndianInt32(
                    header.AsSpan(
                        20,
                        4));

            if (width <=
                    0 ||
                height <=
                    0)
            {
                throw new InvalidDataException(
                    "PNG dimensions must be greater than zero.");
            }

            return (
                width,
                height);
        }

        private static int ReadBigEndianInt32(
            ReadOnlySpan<byte> bytes) =>
            (bytes[0] << 24) |
            (bytes[1] << 16) |
            (bytes[2] << 8) |
            bytes[3];

        private async Task<(long ContentLength, string Sha256)> CopyAndHashAsync(
            string sourcePath,
            Stream destination,
            CancellationToken cancellationToken)
        {
            await using var source =
                new FileStream(
                    sourcePath,
                    new FileStreamOptions
                    {
                        Mode =
                            FileMode.Open,
                        Access =
                            FileAccess.Read,
                        Share =
                            FileShare.Read,
                        BufferSize =
                            BufferSize,
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                    });

            using var hash =
                IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

            var buffer =
                ArrayPool<byte>.Shared.Rent(
                    BufferSize);

            long total =
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

                    total =
                        checked(
                            total +
                            read);

                    if (total >
                        _maxOutputBytes)
                    {
                        throw new InvalidDataException(
                            $"Raster output exceeds the {_maxOutputBytes}-byte limit.");
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

                await destination
                    .FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                var sha256 =
                    Convert.ToHexString(
                            hash.GetHashAndReset())
                        .ToLowerInvariant();

                return (
                    total,
                    sha256);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(
                    buffer);
            }
        }

        private static async Task<string> ReadBoundedAsync(
            TextReader reader,
            int maxCharacters)
        {
            var buffer =
                new char[4096];

            var builder =
                new StringBuilder();

            while (true)
            {
                var read =
                    await reader
                        .ReadAsync(
                            buffer.AsMemory(
                                0,
                                buffer.Length),
                            CancellationToken.None)
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    break;
                }

                var remaining =
                    maxCharacters -
                    builder.Length;

                if (remaining <=
                    0)
                {
                    continue;
                }

                builder.Append(
                    buffer,
                    0,
                    Math.Min(
                        read,
                        remaining));
            }

            return builder.ToString();
        }

        private static void TryKill(
            Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(
                        entireProcessTree:
                            true);
                }
            }
            catch
            {
                // Preserve cancellation/timeout as the primary failure.
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);
        }
    }
}
