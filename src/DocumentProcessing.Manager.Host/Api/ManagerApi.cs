using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.History;
using DocumentProcessing.Manager.Partitioning;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;
using DocumentProcessing.Manager.Settings;
using DocumentProcessing.Manager.Submissions;
using DocumentProcessing.Manager.Host.Hosting;
using DocumentProcessing.Manager.Host.Security;
using Microsoft.Net.Http.Headers;
using System.Text.Json;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace DocumentProcessing.Manager.Host.Api;

internal static class ManagerApi
{
    #region Variables and Constants

    private const string
        DocumentFileNameHeader =
            "X-Document-File-Name";

    private const string
        SourceOriginHeader =
            "X-Source-Origin";

    private const string
        ConsumerIdHeader =
            "X-Consumer-Id";

    private const string
        ConsumerApiKeyHeader =
            "X-Manager-Consumer-Key";

    private const string
        DeliveryReplayApiKeyHeader =
            "X-Manager-Delivery-Replay-Key";

    private const string
        ResultClaimTokenHeader =
            "X-Result-Claim-Token";

    #endregion

    #region Methods Mapping

    public static void Map(
        WebApplication application,
        string apiKey,
        string consumerApiKey,
        string? deliveryReplayApiKey,
        TimeSpan consumerClaimDuration,
        long maximumSourceBytes,
        bool allowPermanentDeletion)
    {
        ArgumentNullException.ThrowIfNull(
            application);

        var group =
            application
                .MapGroup(
                    "/api/manager")
                .AddEndpointFilter(
                    new ManagerApiKeyEndpointFilter(
                        apiKey));

        group.MapGet(
            "/state",
            GetStateAsync);

        group.MapGet(
            "/settings",
            GetSettingsAsync);

        group.MapPut(
            "/settings",
            UpdateSettingsAsync);

        group.MapPost(
            "/control/start",
            static (
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                ExecuteControlAsync(
                    runtime,
                    new StartManagerCommand(),
                    cancellationToken));

        group.MapPost(
            "/control/pause",
            static (
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                ExecuteControlAsync(
                    runtime,
                    new PauseManagerCommand(),
                    cancellationToken));

        group.MapPost(
            "/control/resume",
            static (
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                ExecuteControlAsync(
                    runtime,
                    new ResumeManagerCommand(),
                    cancellationToken));

        group.MapPost(
            "/control/stop",
            static (
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                ExecuteControlAsync(
                    runtime,
                    new StopManagerCommand(),
                    cancellationToken));

        group.MapPut(
            "/submissions/{submissionId:guid}",
            (
                Guid submissionId,
                HttpRequest request,
                SubmitDocumentService submitter,
                IManagerSettingsStore settingsStore,
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                SubmitAsync(
                    submissionId,
                    request,
                    submitter,
                    settingsStore,
                    runtime,
                    maximumSourceBytes,
                    cancellationToken));

        group.MapGet(
            "/queue",
            GetQueueAsync);

        group.MapGet(
            "/archive",
            SearchArchiveAsync);

        group.MapPut(
            "/queue/order",
            ReorderQueueAsync);

        group.MapPost(
            "/queue/{unitId:guid}/release",
            ReleaseQueueUnitAsync);

        group.MapPost(
            "/queue/{unitId:guid}/retry",
            RetryFailedQueueUnitAsync);

        group.MapPost(
            "/queue/{unitId:guid}/remove",
            RemovePendingQueueUnitAsync);

        group.MapPost(
            "/queue/clear",
            ClearPendingQueueAsync);

        group.MapPost(
            "/history/{unitId:guid}/hide",
            HideTerminalQueueUnitAsync);

        group.MapPost(
            "/history/{unitId:guid}/purge",
            (
                Guid unitId,
                QueueVersionRequest request,
                PurgeTerminalProcessingUnitService purgeService,
                IProcessingHistoryReader historyReader,
                IManagerSettingsStore settingsStore,
                TimeProvider timeProvider,
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                PurgeTerminalQueueUnitAsync(
                    unitId,
                    request,
                    allowPermanentDeletion,
                    purgeService,
                    historyReader,
                    settingsStore,
                    timeProvider,
                    runtime,
                    cancellationToken));

        group.MapPost(
            "/submissions/{submissionId:guid}/replay-delivery",
            ReplaySubmissionDeliveryAsync);

        group.MapPost(
            "/queue/{unitId:guid}/prepare-split",
            PrepareQueueUnitSplitAsync);

        group.MapGet(
            "/queue/{unitId:guid}/split-preview",
            GetSplitPreviewAsync);

        group.MapGet(
            "/queue/{unitId:guid}/split-preview/pages/{physicalPageNumber:int}",
            GetSplitPreviewPageAsync);

        group.MapPost(
            "/queue/{unitId:guid}/split",
            SplitPendingUnitAsync);

        group.MapGet(
            "/results/{resultReference}",
            GetResultAsync);

        var consumerGroup =
            application
                .MapGroup(
                    "/api/manager-consumers")
                .AddEndpointFilter(
                    new ManagerApiKeyEndpointFilter(
                        consumerApiKey,
                        ConsumerApiKeyHeader));

        consumerGroup.MapPost(
            "/results/claims",
            (
                HttpRequest request,
                IResultPublicationStore publicationStore,
                TimeProvider timeProvider,
                CancellationToken cancellationToken) =>
                ClaimNextResultAsync(
                    request,
                    publicationStore,
                    timeProvider,
                    consumerClaimDuration,
                    cancellationToken));

        consumerGroup.MapPost(
            "/results/{resultReference}/ack",
            AcknowledgeResultAsync);

        consumerGroup.MapGet(
            "/results/{resultReference}/content",
            GetConsumerResultAsync);

        consumerGroup.MapGet(
            "/results/{resultReference}/visuals",
            GetResultVisualsAsync);

        consumerGroup.MapGet(
            "/results/{resultReference}/visuals/{**assetId}",
            GetResultVisualAsync);

        if (deliveryReplayApiKey is not null)
        {
            application
                .MapGroup("/api/manager-delivery-administration")
                .AddEndpointFilter(
                    new ManagerApiKeyEndpointFilter(
                        deliveryReplayApiKey,
                        DeliveryReplayApiKeyHeader))
                .MapPost(
                    "/submissions/{submissionId:guid}/replay",
                    ReplaySubmissionDeliveryAsync);
        }

        application.MapGet(
            "/health/live",
            static () =>
                HttpResults.Ok(
                    new HealthResponse(
                        "live")));

        application.MapGet(
            "/health/ready",
            GetReadyAsync);
    }

    #endregion

    #region Methods Settings

    private static async Task<IResult> GetSettingsAsync(
        IManagerSettingsStore settingsStore,
        CancellationToken cancellationToken)
    {
        var settings =
            await settingsStore
                .GetAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return HttpResults.Ok(
            ToResponse(
                settings));
    }

    private static async Task<IResult> UpdateSettingsAsync(
        ManagerSettingsUpdateRequest request,
        IManagerSettingsStore settingsStore,
        IProcessingVisualAssetStore visualAssetStore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (!TryMapSubmissionBehavior(
                request.DefaultSubmissionBehavior,
                out var dispatchState))
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.settings_dispatch_invalid",
                    "Default submission behavior must be either 'shelve' or 'run'."));
        }

        if (request.CompletedRetentionDays is <
                ManagerSettingsSnapshot.MinimumCompletedRetentionDays or >
                ManagerSettingsSnapshot.MaximumCompletedRetentionDays)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.completed_retention_invalid",
                    $"Completed retention must be between {ManagerSettingsSnapshot.MinimumCompletedRetentionDays} and {ManagerSettingsSnapshot.MaximumCompletedRetentionDays} days."));
        }

        string? visualDestinationRoot =
            null;

        try
        {
            if (!string.IsNullOrWhiteSpace(
                    request.VisualDestinationRoot))
            {
                if (!Path.IsPathFullyQualified(
                        request.VisualDestinationRoot.Trim()))
                {
                    return HttpResults.BadRequest(
                        new ApiConflictResponse(
                            "manager.visual_destination_invalid",
                            "Visual destination must be an absolute directory path."));
                }

                visualDestinationRoot =
                    Path.TrimEndingDirectorySeparator(
                        Path.GetFullPath(
                            request.VisualDestinationRoot.Trim()));

                await visualAssetStore
                    .ValidateRootAsync(
                        visualDestinationRoot,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var updated =
                await settingsStore
                    .TryUpdateAsync(
                        new UpdateManagerSettingsCommand(
                            request.ExpectedVersion,
                            dispatchState,
                            visualDestinationRoot,
                            request.CompletedRetentionDays),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (updated is not null)
            {
                return HttpResults.Ok(
                    ToResponse(
                        updated));
            }

            var current =
                await settingsStore
                    .GetAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            return HttpResults.Conflict(
                new ManagerSettingsVersionConflictResponse(
                    "manager.settings_version_conflict",
                    "Manager settings changed before this update was applied.",
                    request.ExpectedVersion,
                    current.Version));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.visual_destination_invalid",
                    exception.Message));
        }
        catch (DirectoryNotFoundException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.visual_destination_missing",
                    exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.visual_destination_not_writable",
                    exception.Message));
        }
        catch (IOException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.visual_destination_not_writable",
                    exception.Message));
        }
    }

    #endregion

    #region Methods Control

    private static async Task<IResult> GetStateAsync(
        IManagerStateStore stateStore,
        CancellationToken cancellationToken)
    {
        var snapshot =
            await stateStore
                .GetAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return HttpResults.Ok(
            ToResponse(
                snapshot));
    }

    private static async Task<IResult> ExecuteControlAsync(
        DocumentProcessingManagerRuntime runtime,
        ManagerControlCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await runtime
                    .ExecuteAsync(
                        command,
                        cancellationToken)
                    .ConfigureAwait(false);

            return HttpResults.Ok(
                new ManagerControlResponse(
                    result.Transition.PreviousState,
                    result.Transition.CurrentState,
                    result.Transition.Changed,
                    result.Snapshot.Version));
        }
        catch (InvalidManagerStateTransitionException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse(
                    "manager.invalid_state_transition",
                    exception.Message));
        }
    }

    #endregion

    #region Methods Submission

    private static async Task<IResult> SubmitAsync(
        Guid submissionId,
        HttpRequest request,
        SubmitDocumentService submitter,
        IManagerSettingsStore settingsStore,
        DocumentProcessingManagerRuntime runtime,
        long maximumSourceBytes,
        CancellationToken cancellationToken)
    {
        if (submissionId ==
            Guid.Empty)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.submission_id_invalid",
                    "Submission identifier cannot be empty."));
        }

        var fileName =
            ReadDocumentFileName(
                request);

        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.file_name_required",
                    $"A filename is required in Content-Disposition or header '{DocumentFileNameHeader}'."));
        }

        if (request.ContentLength is <=
            0)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.source_empty",
                    "A submitted document must contain source bytes."));
        }

        if (request.ContentLength >
            maximumSourceBytes)
        {
            return HttpResults.Json(
                new ApiConflictResponse(
                    "manager.source_too_large",
                    $"The source exceeds the configured custody limit of {maximumSourceBytes} bytes."),
                statusCode:
                    StatusCodes.Status413PayloadTooLarge);
        }

        var dispatchValue =
            request.Query[
                    "dispatch"]
                .ToString();

        ProcessingUnitDispatchState initialDispatchState;

        if (string.IsNullOrWhiteSpace(
                dispatchValue))
        {
            initialDispatchState =
                (await settingsStore
                    .GetAsync(
                        cancellationToken)
                    .ConfigureAwait(false))
                .DefaultSubmissionDispatchState;
        }
        else if (!TryMapSubmissionBehavior(
                     dispatchValue,
                     out initialDispatchState))
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.dispatch_invalid",
                    "Query parameter 'dispatch' must be either 'shelve' or 'run'."));
        }

        try
        {
            var registration =
                await submitter
                    .SubmitAsync(
                        new SubmitDocumentCommand(
                            new DocumentSubmissionId(
                                submissionId),
                            request.Body,
                            fileName,
                            request.ContentType,
                            ReadOptionalHeader(
                                request,
                                SourceOriginHeader),
                            initialDispatchState),
                        cancellationToken)
                    .ConfigureAwait(false);

            runtime.NotifyQueueChanged();

            var response =
                new DocumentSubmissionResponse(
                    registration.Submission.SubmissionId.Value,
                    registration.Submission.SourceArtifact.Digest.Value,
                    registration.Submission.SourceArtifact.ByteLength,
                    registration.Submission.OriginalFileName,
                    registration.ProcessingUnitIds
                        .Select(
                            unitId =>
                                unitId.Value)
                        .ToArray(),
                    registration.Created);

            return registration.Created
                ? HttpResults.Created(
                    $"/api/manager/submissions/{submissionId:D}",
                    response)
                : HttpResults.Ok(
                    response);
        }
        catch (DocumentSubmissionConflictException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse(
                    "manager.submission_conflict",
                    exception.Message));
        }
        catch (InvalidDataException)
        {
            return HttpResults.Problem(
                statusCode:
                    StatusCodes.Status413PayloadTooLarge,
                title:
                    "The source is empty or exceeds the configured custody limit.");
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.submission_invalid",
                    exception.Message));
        }
    }

    #endregion

    #region Methods Queue

    private static async Task<IResult> GetQueueAsync(
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        IProcessingProgressReader progressReader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var snapshot =
            await GetRecentSnapshotAsync(
                    historyReader,
                    settingsStore,
                    timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);

        return HttpResults.Ok(
            ToResponse(
                snapshot,
                progressReader));
    }

    private static async Task<IResult> SearchArchiveAsync(
        string? title,
        DateTimeOffset? fromUtc,
        DateTimeOffset? beforeUtc,
        string? sort,
        int? offset,
        int? limit,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryMapArchiveSort(
                sort,
                out var archiveSort))
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.archive_sort_invalid",
                    "Archive sort must be completedNewest, completedOldest, titleAscending or titleDescending."));
        }

        try
        {
            var settings =
                await settingsStore
                    .GetAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            var page =
                await historyReader
                    .SearchArchiveAsync(
                        new ProcessingArchiveQuery(
                            timeProvider.GetUtcNow().AddDays(
                                -settings.CompletedRetentionDays),
                            title,
                            fromUtc,
                            beforeUtc,
                            archiveSort,
                            offset ??
                                0,
                            limit ??
                                ProcessingArchiveQuery.DefaultLimit),
                        cancellationToken)
                    .ConfigureAwait(false);

            return HttpResults.Ok(
                ToResponse(
                    page));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.archive_query_invalid",
                    exception.Message));
        }
    }

    private static async Task<IResult> ReorderQueueAsync(
        QueueReorderRequest request,
        IProcessingQueueStore queueStore,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.OrderedPendingUnitIds is null)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.invalid_queue_order",
                    "Ordered pending unit identifiers are required."));
        }

        try
        {
            await queueStore
                .ReorderPendingAsync(
                    new ReorderProcessingQueueCommand(
                        request.OrderedPendingUnitIds.Select(
                            value =>
                                new ProcessingUnitId(
                                    value)),
                        request.ExpectedVersion),
                    cancellationToken)
                .ConfigureAwait(false);

            runtime.NotifyQueueChanged();

            var snapshot =
                await GetRecentSnapshotAsync(
                        historyReader,
                        settingsStore,
                        timeProvider,
                        cancellationToken)
                    .ConfigureAwait(false);

            return HttpResults.Ok(
                ToResponse(
                    snapshot));
        }
        catch (ProcessingQueueConcurrencyException exception)
        {
            return HttpResults.Conflict(
                new QueueVersionConflictResponse(
                    "manager.queue_version_conflict",
                    exception.Message,
                    exception.ExpectedVersion,
                    exception.ActualVersion));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.invalid_queue_order",
                    exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse(
                    "manager.queue_changed",
                    exception.Message));
        }
    }

    private static async Task<IResult> ReleaseQueueUnitAsync(
        Guid unitId,
        QueueReleaseRequest request,
        IProcessingQueueStore queueStore,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        try
        {
            await queueStore
                .ReleasePendingAsync(
                    new ReleaseProcessingUnitCommand(
                        new ProcessingUnitId(
                            unitId),
                        request.ExpectedVersion),
                    cancellationToken)
                .ConfigureAwait(false);

            runtime.NotifyQueueChanged();

            var snapshot =
                await GetRecentSnapshotAsync(
                        historyReader,
                        settingsStore,
                        timeProvider,
                        cancellationToken)
                    .ConfigureAwait(false);

            return HttpResults.Ok(
                ToResponse(
                    snapshot));
        }
        catch (ProcessingQueueConcurrencyException exception)
        {
            return HttpResults.Conflict(
                new QueueVersionConflictResponse(
                    "manager.queue_version_conflict",
                    exception.Message,
                    exception.ExpectedVersion,
                    exception.ActualVersion));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.invalid_processing_unit",
                    exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse(
                    "manager.processing_unit_not_shelved",
                    exception.Message));
        }
    }

    private static async Task<IResult> RetryFailedQueueUnitAsync(
        Guid unitId,
        QueueRetryRequest request,
        IProcessingQueueStore queueStore,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        try
        {
            await queueStore
                .RetryFailedAsync(
                    new RetryFailedProcessingUnitCommand(
                        new ProcessingUnitId(
                            unitId),
                        request.ExpectedVersion),
                    cancellationToken)
                .ConfigureAwait(false);

            runtime.NotifyQueueChanged();

            var snapshot =
                await GetRecentSnapshotAsync(
                        historyReader,
                        settingsStore,
                        timeProvider,
                        cancellationToken)
                    .ConfigureAwait(false);

            return HttpResults.Ok(
                ToResponse(
                    snapshot));
        }
        catch (ProcessingQueueConcurrencyException exception)
        {
            return HttpResults.Conflict(
                new QueueVersionConflictResponse(
                    "manager.queue_version_conflict",
                    exception.Message,
                    exception.ExpectedVersion,
                    exception.ActualVersion));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.invalid_processing_unit",
                    exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse(
                    "manager.processing_unit_not_failed",
                    exception.Message));
        }
    }

    private static async Task<IResult> RemovePendingQueueUnitAsync(
        Guid unitId,
        QueueVersionRequest request,
        IProcessingQueueStore queueStore,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken) =>
        await ExecuteAdministrativeQueueMutationAsync(
            async () => await queueStore.RemovePendingAsync(
                new RemovePendingProcessingUnitCommand(new ProcessingUnitId(unitId), request.ExpectedVersion),
                cancellationToken).ConfigureAwait(false),
            "manager.processing_unit_not_pending",
            historyReader,
            settingsStore,
            timeProvider,
            runtime,
            cancellationToken).ConfigureAwait(false);

    private static async Task<IResult> ClearPendingQueueAsync(
        QueueVersionRequest request,
        IProcessingQueueStore queueStore,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken) =>
        await ExecuteAdministrativeQueueMutationAsync(
            async () =>
            {
                await queueStore.ClearPendingAsync(
                    new ClearPendingProcessingQueueCommand(request.ExpectedVersion),
                    cancellationToken).ConfigureAwait(false);
            },
            "manager.queue_clear_rejected",
            historyReader,
            settingsStore,
            timeProvider,
            runtime,
            cancellationToken).ConfigureAwait(false);

    private static async Task<IResult> HideTerminalQueueUnitAsync(
        Guid unitId,
        QueueVersionRequest request,
        IProcessingQueueStore queueStore,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken) =>
        await ExecuteAdministrativeQueueMutationAsync(
            async () => await queueStore.HideTerminalAsync(
                new HideTerminalProcessingUnitCommand(new ProcessingUnitId(unitId), request.ExpectedVersion),
                cancellationToken).ConfigureAwait(false),
            "manager.processing_unit_not_terminal",
            historyReader,
            settingsStore,
            timeProvider,
            runtime,
            cancellationToken).ConfigureAwait(false);

    private static async Task<IResult> PurgeTerminalQueueUnitAsync(
        Guid unitId,
        QueueVersionRequest request,
        bool allowPermanentDeletion,
        PurgeTerminalProcessingUnitService purgeService,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        if (!allowPermanentDeletion)
        {
            return HttpResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Permanent deletion is disabled.",
                detail: "Enable ManagerHost:AllowPermanentDeletion only in an explicitly authorized environment.");
        }

        return await ExecuteAdministrativeQueueMutationAsync(
            async () => await purgeService.PurgeAsync(
                new PurgeTerminalProcessingUnitCommand(
                    new ProcessingUnitId(unitId),
                    request.ExpectedVersion),
                cancellationToken).ConfigureAwait(false),
            "manager.processing_unit_purge_rejected",
            historyReader,
            settingsStore,
            timeProvider,
            runtime,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> ReplaySubmissionDeliveryAsync(
        Guid submissionId,
        ReplaySubmissionDeliveryRequest request,
        IResultDeliveryAdministrationStore deliveryAdministrationStore,
        IResultAvailabilitySignal resultAvailabilitySignal,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (submissionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ConsumerId))
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.delivery_replay_invalid",
                    "Submission and consumer identifiers are required."));
        }

        var replay = await deliveryAdministrationStore.ReplaySubmissionAsync(
            request.ConsumerId,
            new DocumentSubmissionId(submissionId),
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);

        if (replay is null)
        {
            return HttpResults.NotFound(
                new ApiConflictResponse(
                    "manager.delivery_replay_not_found",
                    "The submission has no published results to replay."));
        }

        resultAvailabilitySignal.Notify();

        return HttpResults.Ok(
            new ReplaySubmissionDeliveryResponse(
                replay.ReplayId,
                replay.SubmissionId.Value,
                replay.ConsumerId,
                replay.ResultCount,
                replay.RequestedAtUtc));
    }

    private static async Task<IResult> ExecuteAdministrativeQueueMutationAsync(
        Func<Task> mutation,
        string invalidStateCode,
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            await mutation().ConfigureAwait(false);
            runtime.NotifyQueueChanged();

            var snapshot = await GetRecentSnapshotAsync(
                historyReader,
                settingsStore,
                timeProvider,
                cancellationToken).ConfigureAwait(false);
            return HttpResults.Ok(ToResponse(snapshot));
        }
        catch (ProcessingQueueConcurrencyException exception)
        {
            return HttpResults.Conflict(new QueueVersionConflictResponse(
                "manager.queue_version_conflict",
                exception.Message,
                exception.ExpectedVersion,
                exception.ActualVersion));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(new ApiConflictResponse(
                "manager.invalid_processing_unit",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(new ApiConflictResponse(invalidStateCode, exception.Message));
        }
    }

    private static async Task<IResult> GetSplitPreviewAsync(
        Guid unitId,
        IDocumentSplitPreviewProvider previewProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview =
                await previewProvider.InspectAsync(
                    new ProcessingUnitId(unitId),
                    cancellationToken).ConfigureAwait(false);

            return HttpResults.Ok(
                ToResponse(
                    preview));
        }
        catch (NotSupportedException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse("manager.split_not_supported", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse("manager.split_unit_unavailable", exception.Message));
        }
    }

    private static async Task<IResult> GetSplitPreviewPageAsync(
        Guid unitId,
        int physicalPageNumber,
        IDocumentSplitPreviewProvider previewProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes =
                await previewProvider.RenderPageAsync(
                    new ProcessingUnitId(unitId),
                    physicalPageNumber,
                    cancellationToken).ConfigureAwait(false);

            return HttpResults.File(bytes, "image/png");
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse("manager.split_page_invalid", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse("manager.split_unit_unavailable", exception.Message));
        }
    }

    private static async Task<IResult> SplitPendingUnitAsync(
        Guid unitId,
        SplitPendingUnitRequest request,
        SplitPendingProcessingUnitService splitService,
        IDocumentSplitPreviewProvider previewProvider,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (request.Ranges is null)
            {
                return HttpResults.BadRequest(
                    new ApiConflictResponse(
                        "manager.split_plan_invalid",
                        "A split plan is required."));
            }

            var ranges =
                request.Ranges
                    .Select(
                        ToProcessingScope)
                    .ToArray();

            var preview =
                await previewProvider.InspectAsync(
                        new ProcessingUnitId(
                            unitId),
                        cancellationToken)
                    .ConfigureAwait(false);

            ValidateApprovedSplitPlan(
                preview,
                ranges);

            var replacementIds =
                await splitService.SplitAsync(
                    new ProcessingUnitId(unitId),
                    request.ExpectedVersion,
                    ranges,
                    request.ReleaseAfterSplit
                        ? ProcessingUnitDispatchState.Ready
                        : null,
                    cancellationToken).ConfigureAwait(false);

            runtime.NotifyQueueChanged();

            return HttpResults.Ok(
                new SplitPendingUnitResponse(
                    replacementIds.Select(id => id.Value).ToArray()));
        }
        catch (ProcessingQueueConcurrencyException exception)
        {
            return HttpResults.Conflict(
                new QueueVersionConflictResponse(
                    "manager.queue_version_conflict",
                    exception.Message,
                    exception.ExpectedVersion,
                    exception.ActualVersion));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse("manager.split_plan_invalid", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse("manager.split_unit_unavailable", exception.Message));
        }
    }

    private static async Task<IResult> PrepareQueueUnitSplitAsync(
        Guid unitId,
        QueueReleaseRequest request,
        IProcessingQueueStore queueStore,
        DocumentProcessingManagerRuntime runtime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await queueStore.ShelvePendingAsync(
                    new ShelveProcessingUnitCommand(
                        new ProcessingUnitId(unitId),
                        request.ExpectedVersion),
                    cancellationToken)
                .ConfigureAwait(false);

            runtime.NotifyQueueChanged();
            return HttpResults.NoContent();
        }
        catch (ProcessingQueueConcurrencyException exception)
        {
            return HttpResults.Conflict(
                new QueueVersionConflictResponse(
                    "manager.queue_version_conflict",
                    exception.Message,
                    exception.ExpectedVersion,
                    exception.ActualVersion));
        }
        catch (InvalidOperationException exception)
        {
            return HttpResults.Conflict(
                new ApiConflictResponse(
                    "manager.split_unit_unavailable",
                    exception.Message));
        }
    }

    #endregion

    #region Methods Results

    private static async Task<IResult> ClaimNextResultAsync(
        HttpRequest request,
        IResultPublicationStore publicationStore,
        TimeProvider timeProvider,
        TimeSpan claimDuration,
        CancellationToken cancellationToken)
    {
        var consumerId =
            ReadOptionalHeader(
                request,
                ConsumerIdHeader);

        if (consumerId is null)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.consumer_id_required",
                    $"Header '{ConsumerIdHeader}' is required."));
        }

        try
        {
            var observedAtUtc =
                timeProvider.GetUtcNow();
            var delivery =
                await publicationStore
                    .ClaimNextAsync(
                        consumerId,
                        observedAtUtc,
                        observedAtUtc.Add(
                            claimDuration),
                        cancellationToken)
                    .ConfigureAwait(false);

            return delivery is null
                ? HttpResults.NoContent()
                : HttpResults.Ok(
                    ToResponse(
                        delivery));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.invalid_consumer",
                    exception.Message));
        }
    }

    private static async Task<IResult> AcknowledgeResultAsync(
        string resultReference,
        ResultAcknowledgementRequest acknowledgement,
        HttpRequest request,
        IResultPublicationStore publicationStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var consumerId =
            ReadOptionalHeader(
                request,
                ConsumerIdHeader);

        if (consumerId is null)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.consumer_id_required",
                    $"Header '{ConsumerIdHeader}' is required."));
        }

        try
        {
            var acknowledged =
                await publicationStore
                    .AcknowledgeAsync(
                        consumerId,
                        resultReference,
                        acknowledgement.ClaimToken,
                        timeProvider.GetUtcNow(),
                        cancellationToken)
                    .ConfigureAwait(false);

            return acknowledged
                ? HttpResults.NoContent()
                : HttpResults.Conflict(
                    new ApiConflictResponse(
                        "manager.result_claim_not_owned",
                        "The result claim is missing, expired or owned by another delivery attempt."));
        }
        catch (ArgumentException exception)
        {
            return HttpResults.BadRequest(
                new ApiConflictResponse(
                    "manager.invalid_result_acknowledgement",
                    exception.Message));
        }
    }

    private static async Task<IResult> GetResultAsync(
        string resultReference,
        IProcessingResultRegistryReader registry,
        IProcessingResultArtifactReader artifactReader,
        CancellationToken cancellationToken)
    {
        var result =
            await registry
                .GetByReferenceAsync(
                    resultReference,
                    cancellationToken)
                .ConfigureAwait(false);

        if (result is null)
        {
            return HttpResults.NotFound();
        }

        var stream =
            await artifactReader
                .OpenReadAsync(
                    result.Artifact,
                    cancellationToken)
                .ConfigureAwait(false);

        return HttpResults.Stream(
            stream,
            result.MediaType,
            fileDownloadName:
                null,
            enableRangeProcessing:
                false);
    }

    private static async Task<IResult> GetResultVisualsAsync(
        string resultReference,
        HttpRequest request,
        IProcessingResultRegistryReader registry,
        IResultPublicationStore publicationStore,
        IProcessingVisualAssetReader visualReader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await OwnsReadableClaimAsync(
                request,
                resultReference,
                publicationStore,
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return HttpResults.Unauthorized();
        }

        var result =
            await registry
                .GetByReferenceAsync(
                    resultReference,
                    cancellationToken)
                .ConfigureAwait(false);

        if (result is null ||
            result.PublicationDirectory is null)
        {
            return HttpResults.NotFound();
        }

        var assets =
            await visualReader
                .GetAssetsAsync(
                    result,
                    cancellationToken)
                .ConfigureAwait(false);

        return HttpResults.Ok(
            assets.Select(
                    asset =>
                        new PublishedVisualAssetResponse(
                            asset.AssetId,
                            asset.MediaType,
                            asset.ByteLength,
                            asset.Digest.Value))
                .ToArray());
    }

    private static async Task<IResult> GetResultVisualAsync(
        string resultReference,
        string assetId,
        HttpRequest request,
        IProcessingResultRegistryReader registry,
        IResultPublicationStore publicationStore,
        IProcessingVisualAssetReader visualReader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await OwnsReadableClaimAsync(
                request,
                resultReference,
                publicationStore,
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return HttpResults.Unauthorized();
        }

        var result =
            await registry
                .GetByReferenceAsync(
                    resultReference,
                    cancellationToken)
                .ConfigureAwait(false);

        if (result is null ||
            result.PublicationDirectory is null)
        {
            return HttpResults.NotFound();
        }

        var content =
            await visualReader
                .OpenReadAsync(
                    result,
                    assetId,
                    cancellationToken)
                .ConfigureAwait(false);

        return content is null
            ? HttpResults.NotFound()
            : HttpResults.Stream(
                content.Content,
                content.Asset.MediaType,
                fileDownloadName:
                    null,
                enableRangeProcessing:
                    false);
    }

    private static async Task<IResult> GetConsumerResultAsync(
        string resultReference,
        HttpRequest request,
        IProcessingResultRegistryReader registry,
        IProcessingResultArtifactReader artifactReader,
        IResultPublicationStore publicationStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!await OwnsReadableClaimAsync(
                request,
                resultReference,
                publicationStore,
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return HttpResults.Unauthorized();
        }

        return await GetResultAsync(
                resultReference,
                registry,
                artifactReader,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> OwnsReadableClaimAsync(
        HttpRequest request,
        string resultReference,
        IResultPublicationStore publicationStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var consumerId =
            ReadOptionalHeader(
                request,
                ConsumerIdHeader);
        var claimTokenValue =
            ReadOptionalHeader(
                request,
                ResultClaimTokenHeader);

        return consumerId is not null &&
            Guid.TryParse(
                claimTokenValue,
                out var claimToken) &&
            await publicationStore
                .OwnsClaimAsync(
                    consumerId,
                    resultReference,
                    claimToken,
                    timeProvider.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
    }

    #endregion

    #region Methods Health

    private static async Task<IResult> GetReadyAsync(
        IManagerStateStore stateStore,
        CancellationToken cancellationToken)
    {
        await stateStore
            .GetAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return HttpResults.Ok(
            new HealthResponse(
                "ready"));
    }

    #endregion

    #region Methods Mapping

    private static ManagerStateResponse ToResponse(
        ManagerStateSnapshot snapshot) =>
        new(
            snapshot.State,
            snapshot.Version);

    private static ManagerSettingsResponse ToResponse(
        ManagerSettingsSnapshot settings) =>
        new(
            settings.DefaultSubmissionDispatchState ==
                ProcessingUnitDispatchState.Shelved
                ? "shelve"
                : "run",
            settings.VisualDestinationRoot,
            settings.Version,
            settings.CompletedRetentionDays);

    private static ProcessingQueueResponse ToResponse(
        ProcessingQueueSnapshot snapshot,
        IProcessingProgressReader? progressReader = null) =>
        new(
            snapshot.Version,
            snapshot.Items
                .Select(
                    item =>
                        ToResponse(
                            item,
                            progressReader))
                .ToArray());

    private static ProcessingArchiveResponse ToResponse(
        ProcessingArchivePage page) =>
        new(
            page.TotalCount,
            page.Offset,
            page.Limit,
            page.Items
                .Select(
                    item =>
                        ToResponse(
                            item))
                .ToArray());

    private static ProcessingQueueItemResponse ToResponse(
        ProcessingQueueItemSnapshot item,
        IProcessingProgressReader? progressReader = null)
    {
        var progress =
            item.Status ==
                ProcessingUnitStatus.Active
                ? progressReader?.TryGet(
                    item.WorkItem.UnitId)
                : null;

        return new ProcessingQueueItemResponse(
            item.WorkItem.UnitId.Value,
            item.WorkItem.SubmissionId.Value,
            item.OriginalFileName,
            ToResponse(
                item.WorkItem.Scope),
            item.WorkItem.AttemptNumber,
            item.Status,
            item.DispatchState,
            item.QueuePosition,
            item.ResultReference,
            item.LastFailure?.Code,
            item.LastFailure?.Message,
            item.LastInterruptionReason,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            progress is null
                ? null
                : new ProcessingProgressResponse(
                    progress.Stage,
                    progress.CompletionPercentage,
                    progress.CompletedUnitCount,
                    progress.TotalUnitCount,
                    progress.UpdatedAtUtc));
    }

    private static async ValueTask<ProcessingQueueSnapshot> GetRecentSnapshotAsync(
        IProcessingHistoryReader historyReader,
        IManagerSettingsStore settingsStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var settings =
            await settingsStore
                .GetAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await historyReader
            .GetRecentSnapshotAsync(
                timeProvider.GetUtcNow().AddDays(
                    -settings.CompletedRetentionDays),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessingScopeResponse ToResponse(
        ProcessingUnitScope scope) =>
        scope switch
        {
            ProcessingUnitScope.WholeDocument =>
                new ProcessingScopeResponse(
                    "wholeDocument",
                    StartPhysicalPageNumber:
                        null,
                    EndPhysicalPageNumber:
                        null,
                    Title:
                        null,
                    StartContentUnitIndex:
                        null,
                    StartContentUnitId:
                        null,
                    EndContentUnitIndex:
                        null,
                    EndContentUnitId:
                        null),
            ProcessingUnitScope.PageRange range =>
                new ProcessingScopeResponse(
                    "pageRange",
                    range.StartPhysicalPageNumber,
                    range.EndPhysicalPageNumber,
                    range.Title,
                    null,
                    null,
                    null,
                    null),
            ProcessingUnitScope.ContentUnitRange range =>
                new ProcessingScopeResponse(
                    "contentUnitRange",
                    null,
                    null,
                    range.Title,
                    range.StartContentUnitIndex,
                    range.StartContentUnitId,
                    range.EndContentUnitIndex,
                    range.EndContentUnitId),
            _ =>
                throw new InvalidOperationException(
                    $"Unsupported processing scope '{scope.GetType().FullName}'.")
        };

    private static ResultAvailableResponse ToResponse(
        ResultAvailableDelivery delivery) =>
        new(
            delivery.ResultReference,
            delivery.SubmissionId.Value,
            delivery.ProcessingUnitId.Value,
            ToResponse(
                delivery.Scope),
            delivery.SchemaVersion,
            delivery.MediaType,
            delivery.ByteLength,
            delivery.Digest.Value,
            delivery.AvailableAtUtc,
            delivery.ClaimToken,
            delivery.ClaimExpiresAtUtc,
            new SubmissionManifestResponse(
                delivery.SubmissionManifest.SubmissionId.Value,
                delivery.SubmissionManifest.Revision,
                delivery.SubmissionManifest.SourceDigest.Value,
                delivery.SubmissionManifest.OriginalFileName,
                delivery.SubmissionManifest.FinalizedAtUtc,
                delivery.SubmissionManifest.ExpectedUnits
                    .Select(
                        unit =>
                            new ExpectedProcessingUnitResponse(
                                unit.ProcessingUnitId.Value,
                                unit.Ordinal,
                                ToResponse(unit.Scope)))
                    .ToArray()));

    private static string? ReadOptionalHeader(
        HttpRequest request,
        string headerName)
    {
        var value =
            request.Headers[
                    headerName]
                .ToString();

        return string.IsNullOrWhiteSpace(
            value)
            ? null
            : value;
    }

    private static bool TryMapSubmissionBehavior(
        string? value,
        out ProcessingUnitDispatchState dispatchState)
    {
        if (string.Equals(
                value,
                "shelve",
                StringComparison.OrdinalIgnoreCase))
        {
            dispatchState =
                ProcessingUnitDispatchState.Shelved;

            return true;
        }

        if (string.Equals(
                value,
                "run",
                StringComparison.OrdinalIgnoreCase))
        {
            dispatchState =
                ProcessingUnitDispatchState.Ready;

            return true;
        }

        dispatchState =
            default;

        return false;
    }

    private static bool TryMapArchiveSort(
        string? value,
        out ProcessingArchiveSort sort)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            string.Equals(
                value,
                "completedNewest",
                StringComparison.OrdinalIgnoreCase))
        {
            sort =
                ProcessingArchiveSort.CompletedNewest;

            return true;
        }

        var mapped =
            value.ToLowerInvariant() switch
            {
                "completedoldest" =>
                    ProcessingArchiveSort.CompletedOldest,
                "titleascending" =>
                    ProcessingArchiveSort.TitleAscending,
                "titledescending" =>
                    ProcessingArchiveSort.TitleDescending,
                _ =>
                    (ProcessingArchiveSort?)null
            };

        sort =
            mapped.GetValueOrDefault();

        return mapped.HasValue;
    }

    private static string ReadDocumentFileName(
        HttpRequest request)
    {
        var contentDisposition =
            request
                .GetTypedHeaders()
                .ContentDisposition;

        var encodedFileName =
            contentDisposition
                ?.FileNameStar
                .Value;

        if (!string.IsNullOrWhiteSpace(
                encodedFileName))
        {
            return encodedFileName;
        }

        var quotedFileName =
            contentDisposition
                ?.FileName;

        if (quotedFileName is not null)
        {
            var fileName =
                HeaderUtilities
                    .RemoveQuotes(
                        quotedFileName.Value)
                    .Value;

            if (!string.IsNullOrWhiteSpace(
                    fileName))
            {
                return fileName;
            }
        }

        return request.Headers[
                DocumentFileNameHeader]
            .ToString();
    }

    private static SplitPreviewResponse ToResponse(
        DocumentSplitPreviewManifest preview)
    {
        var (
            axisKind,
            physicalPageCount,
            contentUnits) =
            preview.Axis switch
            {
                DocumentPartitionAxis.PhysicalPages pages =>
                    (
                        "physicalPages",
                        (int?)pages.PhysicalPageCount,
                        Array.Empty<SplitContentUnitResponse>()
                    ),
                DocumentPartitionAxis.ContentUnits units =>
                    (
                        "contentUnits",
                        (int?)null,
                        units.ContentUnitIds
                            .Select(
                                (id, index) =>
                                    new SplitContentUnitResponse(
                                        index,
                                        id,
                                        preview.ContentUnitLabels
                                            .FirstOrDefault(
                                                label =>
                                                    label.ContentUnitIndex ==
                                                    index)
                                            ?.SuggestedTitle))
                            .ToArray()
                    ),
                _ =>
                    throw new InvalidOperationException(
                        $"Unsupported split-preview axis '{preview.Axis.GetType().FullName}'.")
            };

        var suggestedRanges =
            preview.SuggestedProposal?
                .Segments
                .Select(
                    segment =>
                        segment.Extent.Start switch
                        {
                            DocumentPartitionPosition.PhysicalPage start =>
                                new SplitSuggestedRangeResponse(
                                    "physicalPageRange",
                                    start.PhysicalPageNumber,
                                    ((DocumentPartitionPosition.PhysicalPage)
                                        segment.Extent.End)
                                    .PhysicalPageNumber,
                                    null,
                                    null,
                                    null,
                                    null,
                                    segment.SuggestedTitle),
                            DocumentPartitionPosition.ContentUnit start =>
                                new SplitSuggestedRangeResponse(
                                    "contentUnitRange",
                                    null,
                                    null,
                                    start.ContentUnitIndex,
                                    start.ContentUnitId,
                                    ((DocumentPartitionPosition.ContentUnit)
                                        segment.Extent.End)
                                    .ContentUnitIndex,
                                    ((DocumentPartitionPosition.ContentUnit)
                                        segment.Extent.End)
                                    .ContentUnitId,
                                    segment.SuggestedTitle),
                            _ =>
                                throw new InvalidOperationException(
                                    $"Unsupported split-preview extent '{segment.Extent.GetType().FullName}'.")
                        })
                .ToArray() ??
            [];

        return new SplitPreviewResponse(
            preview.UnitId.Value,
            preview.SubmissionId.Value,
            preview.OriginalFileName,
            axisKind,
            physicalPageCount,
            contentUnits,
            preview.SplitSuggested,
            suggestedRanges);
    }

    private static ProcessingUnitScope ToProcessingScope(
        SplitRangeRequest range)
    {
        ArgumentNullException.ThrowIfNull(
            range);

        return range.Kind switch
        {
            "physicalPageRange"
                when range.StartPhysicalPageNumber is not null &&
                     range.EndPhysicalPageNumber is not null =>
                new ProcessingUnitScope.PageRange(
                    range.StartPhysicalPageNumber.Value,
                    range.EndPhysicalPageNumber.Value,
                    range.Title),
            "contentUnitRange"
                when range.StartContentUnitIndex is not null &&
                     range.StartContentUnitId is not null &&
                     range.EndContentUnitIndex is not null &&
                     range.EndContentUnitId is not null =>
                new ProcessingUnitScope.ContentUnitRange(
                    range.StartContentUnitIndex.Value,
                    range.StartContentUnitId,
                    range.EndContentUnitIndex.Value,
                    range.EndContentUnitId,
                    range.Title),
            _ =>
                throw new ArgumentException(
                    $"Unsupported or incomplete split range kind '{range.Kind}'.",
                    nameof(range))
        };
    }

    private static void ValidateApprovedSplitPlan(
        DocumentSplitPreviewManifest preview,
        IReadOnlyList<ProcessingUnitScope> ranges)
    {
        if (ranges.Count <
            2)
        {
            throw new ArgumentException(
                "A split plan requires at least two ranges.",
                nameof(ranges));
        }

        switch (preview.Axis)
        {
            case DocumentPartitionAxis.PhysicalPages pages:
                ValidateApprovedPagePlan(
                    pages,
                    ranges);
                return;

            case DocumentPartitionAxis.ContentUnits units:
                ValidateApprovedContentUnitPlan(
                    units,
                    ranges);
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(preview),
                    preview.Axis,
                    "Unknown split-preview axis.");
        }
    }

    private static void ValidateApprovedPagePlan(
        DocumentPartitionAxis.PhysicalPages axis,
        IReadOnlyList<ProcessingUnitScope> ranges)
    {
        var pages =
            ranges
                .OfType<ProcessingUnitScope.PageRange>()
                .ToArray();

        if (pages.Length !=
                ranges.Count ||
            pages[0].StartPhysicalPageNumber !=
                1 ||
            pages[^1].EndPhysicalPageNumber !=
                axis.PhysicalPageCount ||
            pages
                .Skip(
                    1)
                .Zip(
                    pages,
                    (current, previous) =>
                        current.StartPhysicalPageNumber !=
                        previous.EndPhysicalPageNumber +
                        1)
                .Any(
                    invalid =>
                        invalid))
        {
            throw new ArgumentException(
                "The approved page ranges must cover the complete source without gaps or overlaps.",
                nameof(ranges));
        }
    }

    private static void ValidateApprovedContentUnitPlan(
        DocumentPartitionAxis.ContentUnits axis,
        IReadOnlyList<ProcessingUnitScope> ranges)
    {
        var units =
            ranges
                .OfType<ProcessingUnitScope.ContentUnitRange>()
                .ToArray();

        if (units.Length !=
                ranges.Count ||
            units[0].StartContentUnitIndex !=
                0 ||
            units[^1].EndContentUnitIndex !=
                axis.ContentUnitIds.Count -
                1 ||
            units
                .Skip(
                    1)
                .Zip(
                    units,
                    (current, previous) =>
                        current.StartContentUnitIndex !=
                        previous.EndContentUnitIndex +
                        1)
                .Any(
                    invalid =>
                        invalid) ||
            units.Any(
                range =>
                    range.EndContentUnitIndex >=
                        axis.ContentUnitIds.Count ||
                    !string.Equals(
                        range.StartContentUnitId,
                        axis.ContentUnitIds[range.StartContentUnitIndex],
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        range.EndContentUnitId,
                        axis.ContentUnitIds[range.EndContentUnitIndex],
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "The approved content-unit ranges must match and cover the complete source without gaps or overlaps.",
                nameof(ranges));
        }
    }

    #endregion

    #region Types

    internal sealed record ManagerStateResponse(
        ManagerOperatingState State,
        long Version);

    internal sealed record ManagerControlResponse(
        ManagerOperatingState PreviousState,
        ManagerOperatingState CurrentState,
        bool Changed,
        long Version);

    internal sealed record ManagerSettingsResponse(
        string DefaultSubmissionBehavior,
        string? VisualDestinationRoot,
        long Version,
        int CompletedRetentionDays);

    internal sealed record ManagerSettingsUpdateRequest(
        long ExpectedVersion,
        string DefaultSubmissionBehavior,
        string? VisualDestinationRoot,
        int CompletedRetentionDays);

    internal sealed record ManagerSettingsVersionConflictResponse(
        string Code,
        string Message,
        long ExpectedVersion,
        long ActualVersion);

    internal sealed record DocumentSubmissionResponse(
        Guid SubmissionId,
        string SourceSha256,
        long SourceByteLength,
        string OriginalFileName,
        IReadOnlyList<Guid> ProcessingUnitIds,
        bool Created);

    internal sealed record QueueReorderRequest(
        long ExpectedVersion,
        IReadOnlyList<Guid> OrderedPendingUnitIds);

    internal sealed record QueueReleaseRequest(
        long ExpectedVersion);

    internal sealed record QueueRetryRequest(
        long ExpectedVersion);

    internal sealed record QueueVersionRequest(
        long ExpectedVersion);

    internal sealed record SplitPreviewResponse(
        Guid UnitId,
        Guid SubmissionId,
        string OriginalFileName,
        string AxisKind,
        int? PhysicalPageCount,
        IReadOnlyList<SplitContentUnitResponse> ContentUnits,
        bool SplitSuggested,
        IReadOnlyList<SplitSuggestedRangeResponse> SuggestedRanges);

    internal sealed record SplitContentUnitResponse(
        int ContentUnitIndex,
        string ContentUnitId,
        string? SuggestedTitle);

    internal sealed record SplitSuggestedRangeResponse(
        string Kind,
        int? StartPhysicalPageNumber,
        int? EndPhysicalPageNumber,
        int? StartContentUnitIndex,
        string? StartContentUnitId,
        int? EndContentUnitIndex,
        string? EndContentUnitId,
        string? SuggestedTitle);

    internal sealed record SplitRangeRequest(
        string Kind,
        int? StartPhysicalPageNumber,
        int? EndPhysicalPageNumber,
        int? StartContentUnitIndex,
        string? StartContentUnitId,
        int? EndContentUnitIndex,
        string? EndContentUnitId,
        string Title);

    internal sealed record SplitPendingUnitRequest(
        long ExpectedVersion,
        IReadOnlyList<SplitRangeRequest> Ranges,
        bool ReleaseAfterSplit);

    internal sealed record SplitPendingUnitResponse(
        IReadOnlyList<Guid> ProcessingUnitIds);

    internal sealed record ProcessingQueueResponse(
        long Version,
        IReadOnlyList<ProcessingQueueItemResponse> Items);

    internal sealed record ProcessingArchiveResponse(
        long TotalCount,
        int Offset,
        int Limit,
        IReadOnlyList<ProcessingQueueItemResponse> Items);

    internal sealed record ProcessingQueueItemResponse(
        Guid UnitId,
        Guid SubmissionId,
        string OriginalFileName,
        ProcessingScopeResponse Scope,
        int AttemptNumber,
        ProcessingUnitStatus Status,
        ProcessingUnitDispatchState DispatchState,
        long? QueuePosition,
        string? ResultReference,
        string? LastFailureCode,
        string? LastFailureMessage,
        ProcessingInterruptionReason? LastInterruptionReason,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        ProcessingProgressResponse? Progress);

    internal sealed record ProcessingProgressResponse(
        ProcessingProgressStage Stage,
        int CompletionPercentage,
        int? CompletedUnitCount,
        int? TotalUnitCount,
        DateTimeOffset UpdatedAtUtc);

    internal sealed record ProcessingScopeResponse(
        string Kind,
        int? StartPhysicalPageNumber,
        int? EndPhysicalPageNumber,
        string? Title,
        int? StartContentUnitIndex,
        string? StartContentUnitId,
        int? EndContentUnitIndex,
        string? EndContentUnitId);

    internal sealed record QueueVersionConflictResponse(
        string Code,
        string Message,
        long ExpectedVersion,
        long ActualVersion);

    internal sealed record ResultAcknowledgementRequest(
        Guid ClaimToken);

    internal sealed record ReplaySubmissionDeliveryRequest(
        string ConsumerId);

    internal sealed record ReplaySubmissionDeliveryResponse(
        Guid ReplayId,
        Guid SubmissionId,
        string ConsumerId,
        int ResultCount,
        DateTimeOffset RequestedAtUtc);

    internal sealed record ResultAvailableResponse(
        string ResultReference,
        Guid SubmissionId,
        Guid ProcessingUnitId,
        ProcessingScopeResponse Scope,
        string SchemaVersion,
        string MediaType,
        long ByteLength,
        string Sha256,
        DateTimeOffset AvailableAtUtc,
        Guid ClaimToken,
        DateTimeOffset ClaimExpiresAtUtc,
        SubmissionManifestResponse SubmissionManifest);

    internal sealed record SubmissionManifestResponse(
        Guid SubmissionId,
        int Revision,
        string SourceSha256,
        string OriginalFileName,
        DateTimeOffset FinalizedAtUtc,
        IReadOnlyList<ExpectedProcessingUnitResponse> ExpectedUnits);

    internal sealed record ExpectedProcessingUnitResponse(
        Guid ProcessingUnitId,
        int Ordinal,
        ProcessingScopeResponse Scope);

    internal sealed record PublishedVisualAssetResponse(
        string AssetId,
        string MediaType,
        long ByteLength,
        string Sha256);

    internal sealed record ApiConflictResponse(
        string Code,
        string Message);

    internal sealed record HealthResponse(
        string Status);

    #endregion
}
