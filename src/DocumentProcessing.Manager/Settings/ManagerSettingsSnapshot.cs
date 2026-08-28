using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Settings;

/// <summary>
/// Durable versioned settings that affect Manager intake and result storage.
/// </summary>
public sealed record ManagerSettingsSnapshot
{
    #region Variables and Constants

    /// <summary>Gets the default recent-completion retention period.</summary>
    public const int DefaultCompletedRetentionDays =
        30;

    /// <summary>Gets the minimum supported retention period.</summary>
    public const int MinimumCompletedRetentionDays =
        1;

    /// <summary>Gets the maximum supported retention period.</summary>
    public const int MaximumCompletedRetentionDays =
        3650;

    #endregion

    #region Properties

    /// <summary>Gets how newly submitted documents enter the queue.</summary>
    public ProcessingUnitDispatchState DefaultSubmissionDispatchState { get; }

    /// <summary>Gets the optional completed-visual destination root.</summary>
    public string? VisualDestinationRoot { get; }

    /// <summary>Gets how long terminal items remain in Processed.</summary>
    public int CompletedRetentionDays { get; }

    /// <summary>Gets the optimistic-concurrency version.</summary>
    public long Version { get; }

    #endregion

    #region ctor

    /// <summary>Creates one durable Manager-settings snapshot.</summary>
    public ManagerSettingsSnapshot(
        ProcessingUnitDispatchState defaultSubmissionDispatchState,
        string? visualDestinationRoot,
        long version,
        int completedRetentionDays = DefaultCompletedRetentionDays)
    {
        if (!Enum.IsDefined(
                defaultSubmissionDispatchState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultSubmissionDispatchState));
        }

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version));
        }

        if (completedRetentionDays is <
                MinimumCompletedRetentionDays or >
                MaximumCompletedRetentionDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedRetentionDays),
                completedRetentionDays,
                $"Completed retention must be between {MinimumCompletedRetentionDays} and {MaximumCompletedRetentionDays} days.");
        }

        DefaultSubmissionDispatchState =
            defaultSubmissionDispatchState;

        VisualDestinationRoot =
            string.IsNullOrWhiteSpace(
                visualDestinationRoot)
                ? null
                : visualDestinationRoot.Trim();

        CompletedRetentionDays =
            completedRetentionDays;

        Version =
            version;
    }

    #endregion
}
