using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Settings;

/// <summary>
/// Requests an optimistic update of durable Manager settings.
/// </summary>
public sealed record UpdateManagerSettingsCommand
{
    #region Properties

    /// <summary>Gets the expected settings version.</summary>
    public long ExpectedVersion { get; }

    /// <summary>Gets the default submission dispatch state.</summary>
    public ProcessingUnitDispatchState DefaultSubmissionDispatchState { get; }

    /// <summary>Gets the optional completed-visual destination root.</summary>
    public string? VisualDestinationRoot { get; }

    /// <summary>Gets how long terminal items remain in Processed.</summary>
    public int CompletedRetentionDays { get; }

    #endregion

    #region ctor

    /// <summary>Creates one versioned Manager-settings update.</summary>
    public UpdateManagerSettingsCommand(
        long expectedVersion,
        ProcessingUnitDispatchState defaultSubmissionDispatchState,
        string? visualDestinationRoot,
        int completedRetentionDays = ManagerSettingsSnapshot.DefaultCompletedRetentionDays)
    {
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion));
        }

        if (!Enum.IsDefined(
                defaultSubmissionDispatchState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultSubmissionDispatchState));
        }

        if (completedRetentionDays is <
                ManagerSettingsSnapshot.MinimumCompletedRetentionDays or >
                ManagerSettingsSnapshot.MaximumCompletedRetentionDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedRetentionDays),
                completedRetentionDays,
                $"Completed retention must be between {ManagerSettingsSnapshot.MinimumCompletedRetentionDays} and {ManagerSettingsSnapshot.MaximumCompletedRetentionDays} days.");
        }

        ExpectedVersion =
            expectedVersion;

        DefaultSubmissionDispatchState =
            defaultSubmissionDispatchState;

        VisualDestinationRoot =
            string.IsNullOrWhiteSpace(
                visualDestinationRoot)
                ? null
                : visualDestinationRoot.Trim();

        CompletedRetentionDays =
            completedRetentionDays;
    }

    #endregion
}
