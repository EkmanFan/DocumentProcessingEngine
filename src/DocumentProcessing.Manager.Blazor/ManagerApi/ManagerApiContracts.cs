using System.Text.Json.Serialization;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

[JsonConverter(
    typeof(JsonStringEnumConverter<ManagerHostState>))]
internal enum ManagerHostState
{
    Stopped,
    Running,
    Paused
}

[JsonConverter(
    typeof(JsonStringEnumConverter<ManagerQueueItemStatus>))]
internal enum ManagerQueueItemStatus
{
    Pending,
    Active,
    Succeeded,
    Failed
}

[JsonConverter(
    typeof(JsonStringEnumConverter<ManagerQueueItemDispatchState>))]
internal enum ManagerQueueItemDispatchState
{
    Shelved,
    Ready
}

internal enum ManagerControlAction
{
    Start,
    Pause,
    Resume,
    Stop
}

internal sealed record ManagerStateContract(
    ManagerHostState State,
    long Version);

internal sealed record ManagerSettingsContract(
    string DefaultSubmissionBehavior,
    string? VisualDestinationRoot,
    long Version,
    int CompletedRetentionDays);

internal sealed record ManagerSettingsUpdateRequest(
    long ExpectedVersion,
    string DefaultSubmissionBehavior,
    string? VisualDestinationRoot,
    int CompletedRetentionDays);

internal sealed record ManagerQueueContract(
    long Version,
    IReadOnlyList<ManagerQueueItemContract> Items);

internal sealed record ManagerArchiveContract(
    long TotalCount,
    int Offset,
    int Limit,
    IReadOnlyList<ManagerQueueItemContract> Items);

internal sealed record ManagerQueueItemContract(
    Guid UnitId,
    Guid SubmissionId,
    string OriginalFileName,
    ManagerScopeContract Scope,
    int AttemptNumber,
    ManagerQueueItemStatus Status,
    ManagerQueueItemDispatchState DispatchState,
    long? QueuePosition,
    string? ResultReference,
    string? LastFailureCode,
    string? LastFailureMessage,
    DateTimeOffset UpdatedAtUtc);

internal sealed record ManagerScopeContract(
    string Kind,
    int? StartPhysicalPageNumber,
    int? EndPhysicalPageNumber,
    string? Title);

internal sealed record ManagerDocumentSubmissionRequest(
    Guid SubmissionId,
    Stream Content,
    long ContentLength,
    string OriginalFileName,
    string MediaType,
    string? SourceOrigin,
    Components.Workshop.ManagerDocumentSubmissionBehavior SubmissionBehavior);

internal sealed record ManagerQueueReorderRequest(
    long ExpectedVersion,
    IReadOnlyList<Guid> OrderedPendingUnitIds);

internal sealed record ManagerQueueReleaseRequest(
    long ExpectedVersion);

internal sealed record ManagerDocumentSubmissionResult(
    Guid SubmissionId,
    string SourceSha256,
    long SourceByteLength,
    string OriginalFileName,
    IReadOnlyList<Guid> ProcessingUnitIds,
    bool Created);

internal sealed record ManagerApiErrorContract(
    string? Code,
    string? Message,
    string? Title,
    string? Detail);
