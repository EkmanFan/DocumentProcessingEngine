using System.IO.Compression;
using System.Text;
using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Epub.Recognition;

internal sealed class EpubFormatRecognizer
{
    private const string EpubMediaType =
        "application/epub+zip";

    public bool IsRecognized(
        DocumentSource source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (!source.Content.CanSeek)
        {
            throw new InvalidOperationException(
                "EPUB recognition requires a prepared seekable source.");
        }

        var originalPosition =
            source.Content.Position;

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

            var mimetype =
                archive.Entries
                    .FirstOrDefault(
                        entry =>
                            string.Equals(
                                entry.FullName,
                                "mimetype",
                                StringComparison.Ordinal));

            if (mimetype is null ||
                mimetype.Length >
                    64)
            {
                return false;
            }

            using var stream =
                mimetype.Open();

            using var reader =
                new StreamReader(
                    stream,
                    Encoding.ASCII,
                    detectEncodingFromByteOrderMarks:
                        false,
                    leaveOpen:
                        false);

            return string.Equals(
                reader.ReadToEnd(),
                EpubMediaType,
                StringComparison.Ordinal);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            try
            {
                source.Content.Position =
                    originalPosition;
            }
            catch (Exception exception)
                when (exception is IOException or
                      ObjectDisposedException or
                      NotSupportedException)
            {
            }
        }
    }
}
