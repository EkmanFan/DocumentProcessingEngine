namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Stable identity of one configured processing component.
///
/// ProfileId is the versioned configuration identity used for custody and
/// reproducibility. It may encode implementation/model/configuration details
/// without leaking a backend-specific schema into Core.
/// </summary>
public sealed record ProcessingComponentIdentity
{
    public ProcessingComponentIdentity(
        string backendId,
        string profileId)
    {
        if (string.IsNullOrWhiteSpace(
                backendId))
        {
            throw new ArgumentException(
                "Processing backend ID cannot be empty.",
                nameof(backendId));
        }

        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "Processing profile ID cannot be empty.",
                nameof(profileId));
        }

        BackendId =
            backendId.Trim();

        ProfileId =
            profileId.Trim();
    }

    public string BackendId { get; }

    public string ProfileId { get; }
}
