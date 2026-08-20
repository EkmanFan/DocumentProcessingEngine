namespace DocumentProcessing.Core.DualRun;

/// <summary>
/// Deterministic document-level resolution of a configured Dual Run profile.
/// </summary>
public sealed record DocumentDualRunSelection
{
    #region ctor

    public DocumentDualRunSelection(
        DocumentDualRunProfile profile,
        bool isSelected,
        DocumentDualRunExecutionMode? executionMode,
        int? samplingBucket)
    {
        if (!Enum.IsDefined(
                typeof(DocumentDualRunProfile),
                profile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile));
        }

        if (isSelected !=
            executionMode.HasValue)
        {
            throw new ArgumentException(
                "A selected Dual Run document must have exactly one execution mode.",
                nameof(executionMode));
        }

        if (profile ==
            DocumentDualRunProfile.Sampled)
        {
            if (!samplingBucket.HasValue ||
                samplingBucket.Value is < 0 or >=
                    DocumentDualRunProfileSelector.SamplingResolution)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samplingBucket));
            }

            if (isSelected &&
                executionMode !=
                DocumentDualRunExecutionMode.Full)
            {
                throw new ArgumentException(
                    "A selected Sampled document must resolve to Full execution.",
                    nameof(executionMode));
            }
        }
        else if (samplingBucket.HasValue)
        {
            throw new ArgumentException(
                "Only the Sampled profile can carry a sampling bucket.",
                nameof(samplingBucket));
        }

        switch (profile)
        {
            case DocumentDualRunProfile.Disabled
                when isSelected:
                throw new ArgumentException(
                    "Disabled Dual Run cannot select a document.",
                    nameof(isSelected));

            case DocumentDualRunProfile.PlanningOnly
                when executionMode !=
                    DocumentDualRunExecutionMode.PlanningOnly:
                throw new ArgumentException(
                    "PlanningOnly must resolve to PlanningOnly execution.",
                    nameof(executionMode));

            case DocumentDualRunProfile.Full
                when executionMode !=
                    DocumentDualRunExecutionMode.Full:
                throw new ArgumentException(
                    "Full must resolve to Full execution.",
                    nameof(executionMode));
        }

        Profile =
            profile;

        IsSelected =
            isSelected;

        ExecutionMode =
            executionMode;

        SamplingBucket =
            samplingBucket;
    }

    #endregion

    #region Properties

    public DocumentDualRunProfile Profile { get; }

    public bool IsSelected { get; }

    public DocumentDualRunExecutionMode? ExecutionMode { get; }

    /// <summary>
    /// Stable bucket in [0, 10000) for Sampled; null for all other profiles.
    /// </summary>
    public int? SamplingBucket { get; }

    #endregion
}
