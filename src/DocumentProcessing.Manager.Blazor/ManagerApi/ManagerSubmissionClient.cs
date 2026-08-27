using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal sealed class ManagerSubmissionClient(
    HttpClient httpClient)
    : IManagerSubmissionClient
{
    #region Variables and Constants

    private readonly HttpClient
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

    #endregion

    #region Methods

    public async ValueTask<ManagerDocumentSubmissionResult> SubmitDocumentAsync(
        ManagerDocumentSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateSubmissionRequest(
            request);

        using var content =
            new StreamContent(
                request.Content);

        content.Headers.ContentLength =
            request.ContentLength;

        content.Headers.ContentType =
            new MediaTypeHeaderValue(
                request.MediaType);

        content.Headers.ContentDisposition =
            new ContentDispositionHeaderValue(
                "attachment")
            {
                FileNameStar =
                    request.OriginalFileName
            };

        using var message =
            new HttpRequestMessage(
                HttpMethod.Put,
                $"api/manager/submissions/{request.SubmissionId:D}")
            {
                Content =
                    content
            };

        message.Headers.ExpectContinue =
            true;

        if (request.SourceOrigin is not null)
        {
            message.Headers.Add(
                "X-Source-Origin",
                request.SourceOrigin);
        }

        AddLegacyFileNameHeader(
            message,
            request.OriginalFileName);

        using var response =
            await _httpClient
                .SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        await EnsureSuccessAsync(
                response,
                cancellationToken)
            .ConfigureAwait(false);

        return await response.Content
                   .ReadFromJsonAsync<ManagerDocumentSubmissionResult>(
                       cancellationToken)
                   .ConfigureAwait(false) ??
               throw new InvalidDataException(
                   "The Manager returned an empty submission response.");
    }

    #endregion

    #region Methods Validation

    private static void ValidateSubmissionRequest(
        ManagerDocumentSubmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.SubmissionId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Submission identifier cannot be empty.",
                nameof(request));
        }

        if (!request.Content.CanRead)
        {
            throw new ArgumentException(
                "Submission content must be readable.",
                nameof(request));
        }

        if (request.ContentLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.ContentLength,
                "Submission content length must be positive.");
        }

        if (string.IsNullOrWhiteSpace(
                request.OriginalFileName))
        {
            throw new ArgumentException(
                "Submission filename cannot be empty.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(
                request.MediaType))
        {
            throw new ArgumentException(
                "Submission media type cannot be empty.",
                nameof(request));
        }
    }

    private static void AddLegacyFileNameHeader(
        HttpRequestMessage message,
        string fileName)
    {
        if (fileName.All(
                character =>
                    character <=
                    127 &&
                    !char.IsControl(
                        character)))
        {
            message.Headers.Add(
                "X-Document-File-Name",
                fileName);
        }
    }

    private static async ValueTask EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ManagerApiErrorContract? error =
            null;

        try
        {
            error =
                await response.Content
                    .ReadFromJsonAsync<ManagerApiErrorContract>(
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        var message =
            FirstNonEmpty(
                error?.Message,
                error?.Detail,
                error?.Title,
                response.ReasonPhrase) ??
            $"The Manager rejected the document with HTTP status {(int)response.StatusCode}.";

        throw new ManagerSubmissionRejectedException(
            response.StatusCode,
            error?.Code,
            message);
    }

    private static string? FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            value =>
                !string.IsNullOrWhiteSpace(
                    value));

    #endregion
}
