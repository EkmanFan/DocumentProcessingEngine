namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Execution action for one actual source visual occurrence.
/// </summary>
public sealed record VisualElementExecutionPlan
{
    public VisualElementExecutionPlan(
        int sourceVisualIndex,
        VisualExecutionAction action)
    {
        if (sourceVisualIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        if (!Enum.IsDefined(
                action))
        {
            throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "Visual execution action must be a defined value.");
        }

        SourceVisualIndex =
            sourceVisualIndex;

        Action =
            action;
    }

    public int SourceVisualIndex { get; }

    public VisualExecutionAction Action { get; }
}
