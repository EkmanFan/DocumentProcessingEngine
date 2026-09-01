using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentProcessing.Manager.Blazor.Components.Workshop;
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

    public async ValueTask<ManagerWorkshopSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var contract =
            await GetRequiredAsync<ManagerSettingsContract>(
                    "api/manager/settings",
                    cancellationToken)
                .ConfigureAwait(false);

        return ManagerWorkshopSettings.Create(
            contract);
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

    #region Methods Settings

    public async ValueTask<ManagerWorkshopSettings> UpdateSettingsAsync(
        long expectedVersion,
        ManagerDocumentSubmissionBehavior submissionBehavior,
        string? visualDestinationRoot,
        int completedRetentionDays,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion));
        }

        if (!Enum.IsDefined(
                submissionBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(submissionBehavior));
        }

        if (completedRetentionDays is <
                ManagerWorkshopSettings.MinimumCompletedRetentionDays or >
                ManagerWorkshopSettings.MaximumCompletedRetentionDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedRetentionDays));
        }

        using var response =
            await _httpClient
                .PutAsJsonAsync(
                    "api/manager/settings",
                    new ManagerSettingsUpdateRequest(
                        expectedVersion,
                        submissionBehavior ==
                            ManagerDocumentSubmissionBehavior.Shelve
                            ? "shelve"
                            : "run",
                        string.IsNullOrWhiteSpace(
                            visualDestinationRoot)
                            ? null
                            : visualDestinationRoot.Trim(),
                        completedRetentionDays),
                    cancellationToken)
                .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowSettingsRejectedAsync(
                    response,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var contract =
            await response.Content
                .ReadFromJsonAsync<ManagerSettingsContract>(
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidDataException(
                "The Manager returned empty settings.");

        return ManagerWorkshopSettings.Create(
            contract);
    }

    private static async ValueTask ThrowSettingsRejectedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
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

        throw new ManagerSettingsRejectedException(
            response.StatusCode,
            error?.Code,
            error?.Message ??
            error?.Detail ??
            error?.Title ??
            response.ReasonPhrase ??
            "The Manager rejected the settings update.");
    }

    #endregion

    #region Methods Archive

    public async ValueTask<ManagerArchivePage> SearchArchiveAsync(
        ManagerArchiveQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            query);

        var parameters =
            new List<string>
            {
                $"sort={query.Sort.ToApiValue()}",
                $"offset={query.Offset}",
                $"limit={query.Limit}"
            };

        if (query.TitleContains is not null)
        {
            parameters.Add(
                $"title={Uri.EscapeDataString(query.TitleContains)}");
        }

        if (query.CompletedFromUtc.HasValue)
        {
            parameters.Add(
                $"fromUtc={Uri.EscapeDataString(query.CompletedFromUtc.Value.ToString("O"))}");
        }

        if (query.CompletedBeforeUtc.HasValue)
        {
            parameters.Add(
                $"beforeUtc={Uri.EscapeDataString(query.CompletedBeforeUtc.Value.ToString("O"))}");
        }

        var contract =
            await GetRequiredAsync<ManagerArchiveContract>(
                    $"api/manager/archive?{string.Join('&', parameters)}",
                    cancellationToken)
                .ConfigureAwait(false);

        return ManagerArchivePage.Create(
            contract);
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

    #region Methods Queue

    public async ValueTask ReorderQueueAsync(
        long expectedVersion,
        IReadOnlyList<Guid> orderedPendingUnitIds,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                expectedVersion,
                "Queue version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(
            orderedPendingUnitIds);

        if (orderedPendingUnitIds.Any(
                unitId =>
                    unitId ==
                    Guid.Empty) ||
            orderedPendingUnitIds.Distinct().Count() !=
            orderedPendingUnitIds.Count)
        {
            throw new ArgumentException(
                "Queue order must contain distinct non-empty processing-unit identifiers.",
                nameof(orderedPendingUnitIds));
        }

        using var response =
            await _httpClient
                .PutAsJsonAsync(
                    "api/manager/queue/order",
                    new ManagerQueueReorderRequest(
                        expectedVersion,
                        orderedPendingUnitIds),
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);
    }

    public async ValueTask ReleaseProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (unitId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(unitId));
        }

        if (expectedVersion <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                expectedVersion,
                "Queue version cannot be negative.");
        }

        using var response =
            await _httpClient
                .PostAsJsonAsync(
                    $"api/manager/queue/{unitId:D}/release",
                    new ManagerQueueReleaseRequest(
                        expectedVersion),
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);
    }

    public async ValueTask RetryFailedProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (unitId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(unitId));
        }

        if (expectedVersion <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                expectedVersion,
                "Queue version cannot be negative.");
        }

        using var response =
            await _httpClient
                .PostAsJsonAsync(
                    $"api/manager/queue/{unitId:D}/retry",
                    new ManagerQueueRetryRequest(
                        expectedVersion),
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);
    }

    public ValueTask RemovePendingProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default) =>
        PostVersionedUnitCommandAsync(
            unitId,
            expectedVersion,
            $"api/manager/queue/{unitId:D}/remove",
            cancellationToken);

    public async ValueTask ClearPendingQueueAsync(
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(expectedVersion);

        using var response = await _httpClient.PostAsJsonAsync(
            "api/manager/queue/clear",
            new ManagerQueueVersionRequest(expectedVersion),
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public ValueTask HideTerminalProcessingUnitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default) =>
        PostVersionedUnitCommandAsync(
            unitId,
            expectedVersion,
            $"api/manager/history/{unitId:D}/hide",
            cancellationToken);

    private async ValueTask PostVersionedUnitCommandAsync(
        Guid unitId,
        long expectedVersion,
        string requestUri,
        CancellationToken cancellationToken)
    {
        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("Processing-unit identifier cannot be empty.", nameof(unitId));
        }

        ValidateExpectedVersion(expectedVersion);

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri,
            new ManagerQueueVersionRequest(expectedVersion),
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    private static void ValidateExpectedVersion(long expectedVersion)
    {
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                expectedVersion,
                "Queue version cannot be negative.");
        }
    }

    public ValueTask<ManagerSplitPreviewContract> GetSplitPreviewAsync(
        Guid unitId,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<ManagerSplitPreviewContract>(
            $"api/manager/queue/{unitId:D}/split-preview",
            cancellationToken);

    public async ValueTask PrepareProcessingUnitSplitAsync(
        Guid unitId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.PostAsJsonAsync(
                $"api/manager/queue/{unitId:D}/prepare-split",
                new ManagerQueueReleaseRequest(expectedVersion),
                cancellationToken).ConfigureAwait(false);

        EnsureSuccess(response);
    }

    public async ValueTask<byte[]> GetSplitPreviewPageAsync(
        Guid unitId,
        int physicalPageNumber,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.GetAsync(
                $"api/manager/queue/{unitId:D}/split-preview/pages/{physicalPageNumber}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

        EnsureSuccess(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ManagerSplitPendingUnitResult> SplitPendingUnitAsync(
        Guid unitId,
        long expectedVersion,
        IReadOnlyList<ManagerPageRangeRequest> ranges,
        bool releaseAfterSplit,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.PostAsJsonAsync(
                $"api/manager/queue/{unitId:D}/split",
                new ManagerSplitPendingUnitRequest(expectedVersion, ranges, releaseAfterSplit),
                cancellationToken).ConfigureAwait(false);

        EnsureSuccess(response);

        return await response.Content.ReadFromJsonAsync<ManagerSplitPendingUnitResult>(cancellationToken)
                   .ConfigureAwait(false) ??
               throw new InvalidDataException("The Manager returned an empty split result.");
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
