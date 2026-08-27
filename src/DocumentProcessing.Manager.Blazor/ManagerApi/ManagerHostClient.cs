using System.Net;
using System.Net.Http.Json;
using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal sealed class ManagerHostClient(
    HttpClient httpClient)
    : IManagerHostClient
{
    #region Variables and Constants

    private readonly HttpClient
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

    #endregion

    #region Methods Query

    public async ValueTask<ManagerWorkshopSnapshot> GetWorkshopAsync(
        CancellationToken cancellationToken = default)
    {
        var state =
            await GetRequiredAsync<ManagerStateContract>(
                    "api/manager/state",
                    cancellationToken)
                .ConfigureAwait(false);

        var queue =
            await GetRequiredAsync<ManagerQueueContract>(
                    "api/manager/queue",
                    cancellationToken)
                .ConfigureAwait(false);

        return ManagerWorkshopSnapshot.Create(
            state,
            queue);
    }

    private async ValueTask<T> GetRequiredAsync<T>(
        string relativeUri,
        CancellationToken cancellationToken)
    {
        using var response =
            await _httpClient
                .GetAsync(
                    relativeUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);

        return await response.Content
                   .ReadFromJsonAsync<T>(
                       cancellationToken)
                   .ConfigureAwait(false) ??
               throw new InvalidDataException(
                   "The Manager returned an empty response.");
    }

    #endregion

    #region Methods Control

    public async ValueTask ExecuteControlAsync(
        ManagerControlAction action,
        CancellationToken cancellationToken = default)
    {
        var command =
            action switch
            {
                ManagerControlAction.Start =>
                    "start",
                ManagerControlAction.Pause =>
                    "pause",
                ManagerControlAction.Resume =>
                    "resume",
                ManagerControlAction.Stop =>
                    "stop",
                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(action),
                        action,
                        "Unknown Manager control action.")
            };

        using var response =
            await _httpClient
                .PostAsync(
                    $"api/manager/control/{command}",
                    content:
                        null,
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);
    }

    #endregion

    #region Methods Validation

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
