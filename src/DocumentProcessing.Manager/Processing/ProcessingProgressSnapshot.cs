namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Immutable observation of completed work for one active processing unit.
/// </summary>
public sealed record ProcessingProgressSnapshot
{
    #region Properties

    /// <summary>Gets the current execution stage.</summary>
    public ProcessingProgressStage Stage { get; }

    /// <summary>Gets completed pipeline work from zero through one hundred.</summary>
    public int CompletionPercentage { get; }

    /// <summary>Gets the completed page or content-unit count when available.</summary>
    public int? CompletedUnitCount { get; }

    /// <summary>Gets the total page or content-unit count when available.</summary>
    public int? TotalUnitCount { get; }

    /// <summary>Gets when this observation was recorded.</summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    #endregion

    #region ctor

    /// <summary>Creates one processing-progress snapshot.</summary>
    public ProcessingProgressSnapshot(
        ProcessingProgressStage stage,
        int completionPercentage,
        int? completedUnitCount,
        int? totalUnitCount,
        DateTimeOffset updatedAtUtc)
    {
        if (!Enum.IsDefined(
                stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "Unknown Manager processing-progress stage.");
        }

        if (completionPercentage is <
                0 or >
                100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completionPercentage),
                completionPercentage,
                "Completion percentage must be between zero and one hundred.");
        }

        if (completedUnitCount.HasValue !=
            totalUnitCount.HasValue)
        {
            throw new ArgumentException(
                "Completed and total unit counts must either both be supplied or both be omitted.",
                nameof(totalUnitCount));
        }

        if (completedUnitCount is <
                0 ||
            totalUnitCount is <=
                0 ||
            completedUnitCount >
                totalUnitCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedUnitCount),
                completedUnitCount,
                "Completed units must be between zero and the positive total unit count.");
        }

        Stage =
            stage;

        CompletionPercentage =
            completionPercentage;

        CompletedUnitCount =
            completedUnitCount;

        TotalUnitCount =
            totalUnitCount;

        UpdatedAtUtc =
            updatedAtUtc.ToUniversalTime();
    }

    #endregion
}
