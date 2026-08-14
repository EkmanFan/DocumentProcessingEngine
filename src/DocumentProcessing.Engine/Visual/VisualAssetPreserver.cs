using System.Security.Cryptography;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Raster;

namespace DocumentProcessing.Engine.Visual;

/// <summary>
/// Copies one already-materialized visual crop into a caller-owned destination
/// while computing integrity/provenance evidence.
///
/// The engine deliberately does not choose a filesystem, database, object
/// store, or other persistence backend. The destination stream is the storage
/// boundary.
/// </summary>
public sealed class VisualAssetPreserver
{
    public const long DefaultMaxInputBytes =
        64L * 1024L * 1024L;

    private readonly long _maxInputBytes;

    public VisualAssetPreserver(
        long maxInputBytes = DefaultMaxInputBytes)
    {
        if (maxInputBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInputBytes));
        }

        _maxInputBytes = maxInputBytes;
    }

    public async ValueTask<PreservedVisualEvidence> PreserveAsync(
        Stream visualContent,
        Stream destination,
        string sourceDocumentSha256,
        string profileId,
        string mediaType,
        LayoutObservation sourceLayoutObservation,
        PixelRectangle crop,
        int pagePixelWidth,
        int pagePixelHeight,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visualContent);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sourceLayoutObservation);

        if (ReferenceEquals(
                visualContent,
                destination))
        {
            throw new ArgumentException(
                "Visual source and destination streams must be different.",
                nameof(destination));
        }

        if (!visualContent.CanRead)
        {
            throw new ArgumentException(
                "Visual content stream must be readable.",
                nameof(visualContent));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Visual destination stream must be writable.",
                nameof(destination));
        }

        if (pagePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePixelWidth));
        }

        if (pagePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePixelHeight));
        }

        if (LayoutTreatmentPolicy.Decide(sourceLayoutObservation.Kind) !=
            LayoutTreatment.PreserveVisualWithoutOcr)
        {
            throw new InvalidOperationException(
                $"Layout region {sourceLayoutObservation.ObservationSequence} " +
                $"of kind {sourceLayoutObservation.Kind} is not authorized " +
                "for visual preservation by deterministic layout treatment " +
                "policy.");
        }

        var expectedCrop =
            RasterCropGeometry.FromNormalized(
                sourceLayoutObservation.Bounds,
                pagePixelWidth,
                pagePixelHeight);

        if (crop != expectedCrop)
        {
            throw new ArgumentException(
                "Visual crop does not match the deterministic crop derived " +
                "from the source layout observation.",
                nameof(crop));
        }

        if (destination.CanSeek &&
            (destination.Position != 0 ||
             destination.Length != 0))
        {
            throw new ArgumentException(
                "Seekable visual destinations must be empty and positioned " +
                "at zero so a preserved asset cannot silently overwrite or " +
                "append to unrelated content.",
                nameof(destination));
        }

        long? originalSourcePosition =
            null;

        if (visualContent.CanSeek)
        {
            originalSourcePosition =
                visualContent.Position;

            var remaining =
                visualContent.Length -
                visualContent.Position;

            if (remaining <= 0)
            {
                throw new InvalidDataException(
                    "Visual content stream has no remaining bytes.");
            }

            if (remaining > _maxInputBytes)
            {
                throw new InvalidDataException(
                    $"Visual content exceeds the {_maxInputBytes}-byte limit.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        var buffer =
            new byte[81920];

        long total =
            0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read =
                    await visualContent
                        .ReadAsync(
                            buffer.AsMemory(),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                total += read;

                if (total > _maxInputBytes)
                {
                    throw new InvalidDataException(
                        $"Visual content exceeds the " +
                        $"{_maxInputBytes}-byte limit.");
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

            if (total == 0)
            {
                throw new InvalidDataException(
                    "Visual content stream is empty.");
            }

            await destination
                .FlushAsync(cancellationToken)
                .ConfigureAwait(false);

            var contentSha256 =
                Convert.ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant();

            return new PreservedVisualEvidence(
                sourceDocumentSha256,
                profileId,
                mediaType,
                sourceLayoutObservation,
                pagePixelWidth,
                pagePixelHeight,
                crop,
                total,
                contentSha256);
        }
        catch
        {
            if (destination.CanSeek)
            {
                destination.SetLength(0);
                destination.Position = 0;
            }

            throw;
        }
        finally
        {
            if (originalSourcePosition.HasValue)
            {
                visualContent.Position =
                    originalSourcePosition.Value;
            }
        }
    }
}
