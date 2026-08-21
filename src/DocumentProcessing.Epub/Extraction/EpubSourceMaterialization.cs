namespace DocumentProcessing.Epub.Extraction;

internal sealed class EpubSourceMaterialization
    : IAsyncDisposable
{
    #region Properties

    public string Path { get; }

    #endregion

    #region ctor

    private EpubSourceMaterialization(
        string path)
    {
        Path =
            path;
    }

    #endregion

    #region Methods Creation

    public static async ValueTask<EpubSourceMaterialization> CreateAsync(
        Stream source,
        long maximumSourceBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (!source.CanSeek)
        {
            throw new InvalidOperationException(
                "EPUB materialization requires a prepared seekable source.");
        }

        var path =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"document-processing-{System.IO.Path.GetRandomFileName()}.epub");

        try
        {
            await using var destination =
                new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode =
                            FileMode.CreateNew,
                        Access =
                            FileAccess.Write,
                        Share =
                            FileShare.Read,
                        BufferSize =
                            81920,
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                    });

            source.Position =
                0;

            var buffer =
                new byte[81920];

            long copied =
                0;

            while (true)
            {
                var read =
                    await source
                        .ReadAsync(
                            buffer,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    break;
                }

                copied =
                    checked(
                        copied +
                        read);

                if (copied >
                    maximumSourceBytes)
                {
                    throw new InvalidDataException(
                        "EPUB source exceeds the configured V1 size boundary.");
                }

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

            return new EpubSourceMaterialization(
                path);
        }
        catch
        {
            TryDelete(
                path);

            throw;
        }
        finally
        {
            try
            {
                source.Position =
                    0;
            }
            catch (Exception exception)
                when (exception is IOException or
                      ObjectDisposedException or
                      NotSupportedException)
            {
            }
        }
    }

    #endregion

    #region Methods Lifecycle

    public ValueTask DisposeAsync()
    {
        TryDelete(
            Path);

        return ValueTask.CompletedTask;
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            File.Delete(
                path);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
        }
    }

    #endregion
}
