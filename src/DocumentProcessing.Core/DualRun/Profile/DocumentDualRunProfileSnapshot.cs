namespace DocumentProcessing.Core.DualRun;

/// <summary>
/// Immutable Dual Run configuration captured once for a document.
///
/// Configuration values are snapshotted before document processing. The stable
/// source SHA-256 may become available later; Resolve then performs the already
/// frozen deterministic document selection without rereading configuration.
/// </summary>
public sealed record DocumentDualRunProfileSnapshot
{
    #region Properties

    public DocumentDualRunProfile Profile { get; }

    public int SampledBasisPoints { get; }

    #endregion

    #region ctor

    public DocumentDualRunProfileSnapshot(
        DocumentDualRunProfile profile,
        int sampledBasisPoints = 0)
    {
        if (!Enum.IsDefined(
                typeof(DocumentDualRunProfile),
                profile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile));
        }

        if (sampledBasisPoints is < 0 or >
            DocumentDualRunProfileSelector.SamplingResolution)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampledBasisPoints));
        }

        Profile =
            profile;

        SampledBasisPoints =
            sampledBasisPoints;
    }

    #endregion

    #region Methods Selection

    public DocumentDualRunSelection Resolve(
        string? sourceDocumentSha256 = null) =>
        DocumentDualRunProfileSelector
            .Select(
                Profile,
                sourceDocumentSha256,
                SampledBasisPoints);

    #endregion
}
