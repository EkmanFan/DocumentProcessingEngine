namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Describes monotonic pipeline completion observed during one processing request.
/// </summary>
public sealed record DocumentProcessingProgress
{
    #region Properties

    /// <summary>Gets the current processing stage.</summary>
    public DocumentProcessingProgressStage Stage { get; }

    /// <summary>Gets completed pipeline work as an integer from zero through one hundred.</summary>
    public int CompletionPercentage { get; }

    /// <summary>Gets the completed source-unit count when the stage exposes one.</summary>
    public int? CompletedUnitCount { get; }

    /// <summary>Gets the total source-unit count when the stage exposes one.</summary>
    public int? TotalUnitCount { get; }

    #endregion

    #region ctor

    /// <summary>Creates one processing-progress observation.</summary>
    public DocumentProcessingProgress(
        DocumentProcessingProgressStage stage,
        int completionPercentage,
        int? completedUnitCount = null,
        int? totalUnitCount = null)
    {
        if (!Enum.IsDefined(
                stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "Unknown document-processing progress stage.");
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
    }

    #endregion
}
