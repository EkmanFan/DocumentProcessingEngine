using System.Text.Json;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Ephemeral disk-backed custody for the compact evidence produced by the
/// authoritative full-page layout phase.
///
/// The spool deliberately persists only neutral raster metadata and layout
/// observations. Full-page raster bytes are never retained here.
/// </summary>
internal sealed class AuthoritativeLayoutSpool : IAsyncDisposable
{
    #region Variables and Constants

    private const int CurrentSchemaVersion = 1;
    private const string DirectoryPrefix = "document-processing-authoritative-layout-";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _directoryPath;
    private bool _disposed;

    #endregion

    #region ctor

    private AuthoritativeLayoutSpool(
        string directoryPath)
    {
        _directoryPath = directoryPath;
    }

    #endregion

    #region Methods Creation and Custody

    public static AuthoritativeLayoutSpool Create(
        string? temporaryRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(temporaryRoot)
            ? Path.GetTempPath()
            : Path.GetFullPath(temporaryRoot);

        Directory.CreateDirectory(root);

        var directoryPath =
            Path.Combine(
                root,
                $"{DirectoryPrefix}{Guid.NewGuid():N}");

        Directory.CreateDirectory(directoryPath);

        return new AuthoritativeLayoutSpool(
            directoryPath);
    }

    public async ValueTask WriteAsync(
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(pageRaster);
        ArgumentNullException.ThrowIfNull(layout);
        cancellationToken.ThrowIfCancellationRequested();

        ValidatePair(
            pageRaster,
            layout);

        var path =
            PagePath(
                pageRaster.PhysicalPageNumber);

        await using var stream =
            new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options:
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

        await JsonSerializer
            .SerializeAsync(
                stream,
                ToDto(
                    pageRaster,
                    layout),
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<AuthoritativePreparedLayoutPage> ReadAsync(
        int physicalPageNumber,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var path =
            PagePath(
                physicalPageNumber);

        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                $"No authoritative layout spool entry exists for physical page {physicalPageNumber}.");
        }

        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options:
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

        var dto =
            await JsonSerializer
                .DeserializeAsync<SpoolPageDto>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidDataException(
                $"Authoritative layout spool entry for physical page {physicalPageNumber} is empty.");

        return FromDto(
            physicalPageNumber,
            dto);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(
                _directoryPath,
                recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    #endregion

    #region Methods Serialization

    private static SpoolPageDto ToDto(
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout) =>
        new(
            CurrentSchemaVersion,
            pageRaster.PhysicalPageNumber,
            pageRaster.SourcePagePixelWidth,
            pageRaster.SourcePagePixelHeight,
            pageRaster.OutputPixelWidth,
            pageRaster.OutputPixelHeight,
            pageRaster.MediaType,
            pageRaster.ProfileId,
            pageRaster.ContentLength,
            pageRaster.ContentSha256,
            layout.BackendId,
            layout.Observations
                .Select(
                    observation =>
                        new SpoolObservationDto(
                            observation.PhysicalPageNumber,
                            observation.ObservationSequence,
                            observation.ReadingOrder,
                            (int)observation.Kind,
                            observation.Bounds.Left,
                            observation.Bounds.Top,
                            observation.Bounds.Right,
                            observation.Bounds.Bottom,
                            observation.RawLabel))
                .ToArray());

    private static AuthoritativePreparedLayoutPage FromDto(
        int expectedPhysicalPageNumber,
        SpoolPageDto dto)
    {
        if (dto.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported authoritative layout spool schema version {dto.SchemaVersion}.");
        }

        if (dto.PhysicalPageNumber != expectedPhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Authoritative layout spool page identity does not match the requested physical page.");
        }

        var observations =
            dto.Observations
                .Select(
                    observation =>
                    {
                        if (!Enum.IsDefined(
                                typeof(LayoutObservationKind),
                                observation.Kind))
                        {
                            throw new InvalidDataException(
                                $"Authoritative layout spool contains unknown layout kind {observation.Kind}.");
                        }

                        return new LayoutObservation(
                            observation.PhysicalPageNumber,
                            observation.ObservationSequence,
                            observation.ReadingOrder,
                            (LayoutObservationKind)observation.Kind,
                            new NormalizedRectangle(
                                observation.Left,
                                observation.Top,
                                observation.Right,
                                observation.Bottom),
                            observation.RawLabel);
                    })
                .ToArray();

        var pageRaster =
            new RasterRenderResult(
                dto.PhysicalPageNumber,
                dto.SourcePagePixelWidth,
                dto.SourcePagePixelHeight,
                crop: null,
                dto.OutputPixelWidth,
                dto.OutputPixelHeight,
                dto.MediaType,
                dto.ProfileId,
                dto.ContentLength,
                dto.ContentSha256);

        var layout =
            new LayoutAnalysisResult(
                dto.LayoutBackendId,
                dto.PhysicalPageNumber,
                observations);

        ValidatePair(
            pageRaster,
            layout);

        return new AuthoritativePreparedLayoutPage(
            pageRaster,
            layout);
    }

    #endregion

    #region Methods Validation

    private static void ValidatePair(
        RasterRenderResult pageRaster,
        LayoutAnalysisResult layout)
    {
        if (!pageRaster.IsFullPage)
        {
            throw new InvalidDataException(
                "Authoritative layout spool accepts only full-page raster metadata.");
        }

        if (pageRaster.PhysicalPageNumber != layout.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Authoritative layout spool raster and layout evidence belong to different physical pages.");
        }
    }

    private string PagePath(
        int physicalPageNumber) =>
        Path.Combine(
            _directoryPath,
            $"page-{physicalPageNumber:D6}.json");

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    #endregion

    private sealed record SpoolPageDto(
        int SchemaVersion,
        int PhysicalPageNumber,
        int SourcePagePixelWidth,
        int SourcePagePixelHeight,
        int OutputPixelWidth,
        int OutputPixelHeight,
        string MediaType,
        string ProfileId,
        long ContentLength,
        string ContentSha256,
        string LayoutBackendId,
        IReadOnlyList<SpoolObservationDto> Observations);

    private sealed record SpoolObservationDto(
        int PhysicalPageNumber,
        int ObservationSequence,
        int? ReadingOrder,
        int Kind,
        double Left,
        double Top,
        double Right,
        double Bottom,
        string? RawLabel);
}

internal sealed record AuthoritativePreparedLayoutPage(
    RasterRenderResult PageRaster,
    LayoutAnalysisResult Layout);
