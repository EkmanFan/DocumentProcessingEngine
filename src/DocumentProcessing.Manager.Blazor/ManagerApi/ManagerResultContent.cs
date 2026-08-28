namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal sealed class ManagerResultContent
    : IAsyncDisposable
{
    #region Variables and Constants

    private readonly HttpResponseMessage
        _response;

    #endregion

    #region Properties

    public Stream Content { get; }

    public string MediaType { get; }

    public long? ContentLength { get; }

    #endregion

    #region ctor

    public ManagerResultContent(
        HttpResponseMessage response,
        Stream content,
        string mediaType,
        long? contentLength)
    {
        _response =
            response ??
            throw new ArgumentNullException(
                nameof(response));

        Content =
            content ??
            throw new ArgumentNullException(
                nameof(content));

        if (string.IsNullOrWhiteSpace(
                mediaType))
        {
            throw new ArgumentException(
                "Manager result media type cannot be empty.",
                nameof(mediaType));
        }

        if (contentLength <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                "Manager result content length cannot be negative.");
        }

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        ContentLength =
            contentLength;
    }

    #endregion

    #region Methods

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Content
                .DisposeAsync();
        }
        finally
        {
            _response.Dispose();
        }
    }

    #endregion
}
