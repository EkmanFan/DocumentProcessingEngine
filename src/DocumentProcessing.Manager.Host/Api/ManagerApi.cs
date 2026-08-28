using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.History;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;
using DocumentProcessing.Manager.Settings;
using DocumentProcessing.Manager.Submissions;
using DocumentProcessing.Manager.Host.Security;
using Microsoft.Net.Http.Headers;
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
        ResultClaimTokenHeader =
            "X-Result-Claim-Token";

    #endregion

    #region Methods Mapping

    public static void Map(
        WebApplication application,
        string apiKey,
        string consumerApiKey,
        TimeSpan consumerClaimDuration,
        long maximumSourceBytes)
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
                snapshot));
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
        ProcessingQueueSnapshot snapshot) =>
        new(
            snapshot.Version,
            snapshot.Items
                .Select(
                    ToResponse)
                .ToArray());

    private static ProcessingArchiveResponse ToResponse(
        ProcessingArchivePage page) =>
        new(
            page.TotalCount,
            page.Offset,
            page.Limit,
            page.Items
                .Select(
                    ToResponse)
                .ToArray());

    private static ProcessingQueueItemResponse ToResponse(
        ProcessingQueueItemSnapshot item) =>
        new(
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
            item.UpdatedAtUtc);

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
                        null),
            ProcessingUnitScope.PageRange range =>
                new ProcessingScopeResponse(
                    "pageRange",
                    range.StartPhysicalPageNumber,
                    range.EndPhysicalPageNumber,
                    range.Title),
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
            delivery.ClaimExpiresAtUtc);

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
        DateTimeOffset UpdatedAtUtc);

    internal sealed record ProcessingScopeResponse(
        string Kind,
        int? StartPhysicalPageNumber,
        int? EndPhysicalPageNumber,
        string? Title);

    internal sealed record QueueVersionConflictResponse(
        string Code,
        string Message,
        long ExpectedVersion,
        long ActualVersion);

    internal sealed record ResultAcknowledgementRequest(
        Guid ClaimToken);

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
        DateTimeOffset ClaimExpiresAtUtc);

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
