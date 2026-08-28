using DocumentProcessing.Manager.Blazor.Components.Workshop;
using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.Manager.Blazor.Workshop;

internal sealed record ManagerWorkshopSettings(
    ManagerDocumentSubmissionBehavior DefaultSubmissionBehavior,
    string? VisualDestinationRoot,
    long Version,
    int CompletedRetentionDays)
{
    #region Variables and Constants

    public const int DefaultCompletedRetentionDays =
        30;

    public const int MinimumCompletedRetentionDays =
        1;

    public const int MaximumCompletedRetentionDays =
        3650;

    #endregion

    #region Methods Factory

    public static ManagerWorkshopSettings Create(
        ManagerSettingsContract contract)
    {
        ArgumentNullException.ThrowIfNull(
            contract);

        if (contract.Version < 0)
        {
            throw new InvalidDataException(
                "The Manager returned an invalid settings version.");
        }

        if (contract.CompletedRetentionDays is <
                MinimumCompletedRetentionDays or >
                MaximumCompletedRetentionDays)
        {
            throw new InvalidDataException(
                "The Manager returned an invalid completed-item retention period.");
        }

        var behavior =
            contract.DefaultSubmissionBehavior.ToLowerInvariant() switch
            {
                "shelve" =>
                    ManagerDocumentSubmissionBehavior.Shelve,
                "run" =>
                    ManagerDocumentSubmissionBehavior.Run,
                _ =>
                    throw new InvalidDataException(
                        "The Manager returned an unknown submission behavior.")
            };

        return new ManagerWorkshopSettings(
            behavior,
            string.IsNullOrWhiteSpace(
                contract.VisualDestinationRoot)
                ? null
                : contract.VisualDestinationRoot.Trim(),
            contract.Version,
            contract.CompletedRetentionDays);
    }

    #endregion
}
