using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;
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

    #endregion

    #region Methods Mapping

    public static void Map(
        WebApplication application,
        string apiKey,
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
                DocumentProcessingManagerRuntime runtime,
                CancellationToken cancellationToken) =>
                SubmitAsync(
                    submissionId,
                    request,
                    submitter,
                    runtime,
                    maximumSourceBytes,
                    cancellationToken));

        group.MapGet(
            "/queue",
            GetQueueAsync);

        group.MapPut(
            "/queue/order",
            ReorderQueueAsync);

        group.MapPost(
            "/queue/{unitId:guid}/release",
            ReleaseQueueUnitAsync);

        group.MapGet(
            "/results/{resultReference}",
            GetResultAsync);

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

        if (!TryReadInitialDispatchState(
                request,
                out var initialDispatchState))
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
        IProcessingQueueReader queueReader,
        CancellationToken cancellationToken)
    {
        var snapshot =
            await queueReader
                .GetSnapshotAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return HttpResults.Ok(
            ToResponse(
                snapshot));
    }

    private static async Task<IResult> ReorderQueueAsync(
        QueueReorderRequest request,
        IProcessingQueueStore queueStore,
        IProcessingQueueReader queueReader,
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
                await queueReader
                    .GetSnapshotAsync(
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
        IProcessingQueueReader queueReader,
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
                await queueReader
                    .GetSnapshotAsync(
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

    private static ProcessingQueueResponse ToResponse(
        ProcessingQueueSnapshot snapshot) =>
        new(
            snapshot.Version,
            snapshot.Items
                .Select(
                    item =>
                        new ProcessingQueueItemResponse(
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
                            item.UpdatedAtUtc))
                .ToArray());

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

    private static bool TryReadInitialDispatchState(
        HttpRequest request,
        out ProcessingUnitDispatchState dispatchState)
    {
        var value =
            request.Query[
                    "dispatch"]
                .ToString();

        if (string.IsNullOrWhiteSpace(
                value) ||
            string.Equals(
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

    internal sealed record ApiConflictResponse(
        string Code,
        string Message);

    internal sealed record HealthResponse(
        string Status);

    #endregion
}
