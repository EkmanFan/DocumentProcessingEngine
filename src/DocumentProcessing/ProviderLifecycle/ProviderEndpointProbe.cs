namespace DocumentProcessing.ProviderLifecycle;

internal interface IProviderEndpointProbe
    : IDisposable
{
    ValueTask<bool> IsReadyAsync(
        Uri processingEndpoint,
        CancellationToken cancellationToken);
}

internal sealed class ProviderEndpointProbe
    : IProviderEndpointProbe
{
    #region Variables and Constants

    private static readonly TimeSpan
        ProbeTimeout =
            TimeSpan.FromSeconds(
                5);

    private readonly HttpClient
        _httpClient =
            new()
            {
                Timeout =
                    Timeout.InfiniteTimeSpan
            };

    #endregion

    #region Methods

    public async ValueTask<bool> IsReadyAsync(
        Uri processingEndpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            processingEndpoint);

        var readinessEndpoint =
            new Uri(
                processingEndpoint.GetLeftPart(
                    UriPartial.Authority) +
                "/openapi.json",
                UriKind.Absolute);

        using var timeout =
            new CancellationTokenSource(
                ProbeTimeout);

        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

        try
        {
            using var response =
                await _httpClient
                    .GetAsync(
                        readinessEndpoint,
                        HttpCompletionOption.ResponseHeadersRead,
                        linked.Token)
                    .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #endregion
}
