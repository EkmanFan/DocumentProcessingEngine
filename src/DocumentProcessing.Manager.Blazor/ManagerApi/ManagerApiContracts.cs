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

internal sealed record ManagerQueueContract(
    long Version,
    IReadOnlyList<ManagerQueueItemContract> Items);

internal sealed record ManagerQueueItemContract(
    Guid UnitId,
    Guid SubmissionId,
    string OriginalFileName,
    ManagerScopeContract Scope,
    int AttemptNumber,
    ManagerQueueItemStatus Status,
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
