using System.IO.Compression;
using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Epub.Extraction;

/// <summary>
/// Copies one selected packaged EPUB image without transcoding it.
/// </summary>
internal sealed class EpubStructuredNativeVisualMaterializer(
    long maximumVisualResourceBytes)
    : IStructuredNativeVisualMaterializer
{
    public const string ProfileId =
        "epub-package-image-raw-v1";

    public bool CanMaterialize(
        DocumentFormatId format) =>
        format ==
        DocumentFormatId.Epub;

    public async ValueTask<StructuredNativeVisualMaterialization>
        MaterializeAsync(
            DocumentSource source,
            DocumentFormatId format,
            StructuredNativeVisual visual,
            Stream destination,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            visual);

        ArgumentNullException.ThrowIfNull(
            destination);

        if (!CanMaterialize(
                format))
        {
            throw new NotSupportedException(
                $"Format '{format}' is not supported by the EPUB visual materializer.");
        }

        if (!source.Content.CanSeek)
        {
            throw new InvalidOperationException(
                "EPUB visual materialization requires an Engine-prepared seekable source.");
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Visual destination stream must be writable.",
                nameof(destination));
        }

        if (ReferenceEquals(
                source.Content,
                destination))
        {
            throw new ArgumentException(
                "Document source and visual destination streams must be different.",
                nameof(destination));
        }

        if (destination.CanSeek &&
            (destination.Position !=
                 0 ||
             destination.Length !=
                 0))
        {
            throw new ArgumentException(
                "Seekable visual destinations must be empty and positioned at zero.",
                nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var originalPosition =
            source.Content.Position;

        Exception? processingFailure =
            null;

        try
        {
            source.Content.Position =
                0;

            using var archive =
                new ZipArchive(
                    source.Content,
                    ZipArchiveMode.Read,
                    leaveOpen:
                        true);

            var resourcePath =
                EpubArchivePath.NormalizeEntryPath(
                    visual.SourceResourceId);

            var entry =
                archive.GetEntry(
                    resourcePath) ??
                throw new InvalidDataException(
                    "Selected EPUB visual resource is missing from the archive.");

            if (entry.Length <=
                    0 ||
                entry.Length >
                    maximumVisualResourceBytes)
            {
                throw new InvalidDataException(
                    "Selected EPUB visual exceeds the configured resource boundary.");
            }

            using var hash =
                IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

            await using var input =
                entry.Open();

            var buffer =
                new byte[81920];

            long total =
                0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read =
                    await input.ReadAsync(
                            buffer,
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
                    maximumVisualResourceBytes)
                {
                    throw new InvalidDataException(
                        "Selected EPUB visual exceeds the configured resource boundary.");
                }

                hash.AppendData(
                    buffer,
                    0,
                    read);

                await destination.WriteAsync(
                        buffer.AsMemory(
                            0,
                            read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await destination.FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new StructuredNativeVisualMaterialization(
                ProfileId,
                visual.MediaType,
                total,
                Convert.ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant());
        }
        catch (Exception exception)
        {
            processingFailure =
                exception;

            if (destination.CanSeek)
            {
                try
                {
                    destination.SetLength(
                        0);

                    destination.Position =
                        0;
                }
                catch
                {
                    // Preserve the materialization failure.
                }
            }

            throw;
        }
        finally
        {
            try
            {
                source.Content.Position =
                    originalPosition;
            }
            catch when (processingFailure is not null)
            {
                // Preserve the materialization failure.
            }
        }
    }
}
