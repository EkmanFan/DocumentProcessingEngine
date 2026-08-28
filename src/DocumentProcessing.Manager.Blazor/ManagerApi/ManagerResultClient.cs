using System.Net;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal sealed class ManagerResultClient(
    HttpClient httpClient)
    : IManagerResultClient
{
    #region Variables and Constants

    private const string
        DefaultMediaType =
            "application/octet-stream";

    private readonly HttpClient
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

    #endregion

    #region Methods

    public async ValueTask<ManagerResultContent?> OpenResultAsync(
        string resultReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                resultReference))
        {
            throw new ArgumentException(
                "Manager result reference cannot be empty.",
                nameof(resultReference));
        }

        var encodedReference =
            Uri.EscapeDataString(
                resultReference.Trim());

        var response =
            await _httpClient
                .GetAsync(
                    $"api/manager/results/{encodedReference}",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            response.Dispose();

            return null;
        }

        try
        {
            EnsureSuccess(
                response);

            var content =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            return new ManagerResultContent(
                response,
                content,
                response.Content.Headers.ContentType?.MediaType ??
                DefaultMediaType,
                response.Content.Headers.ContentLength);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static void EnsureSuccess(
        HttpResponseMessage response)
    {
        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "The Manager rejected the configured UI credentials.");
        }

        response.EnsureSuccessStatusCode();
    }

    #endregion
}
