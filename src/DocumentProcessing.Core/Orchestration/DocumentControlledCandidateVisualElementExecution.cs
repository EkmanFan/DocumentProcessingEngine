using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Non-authoritative execution evidence for one actual source visual occurrence.
/// </summary>
public sealed record DocumentControlledCandidateVisualElementExecution
{
    public DocumentControlledCandidateVisualElementExecution(
        int sourceVisualIndex,
        VisualExecutionAction action,
        SourceVisualAssetMaterialization? materialization = null)
    {
        if (sourceVisualIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex));
        }

        if (!Enum.IsDefined(
                action))
        {
            throw new ArgumentOutOfRangeException(
                nameof(action));
        }

        switch (action)
        {
            case VisualExecutionAction.NoAdditionalSemanticProcessing:
            case VisualExecutionAction.AnalyzeVisual:
                if (materialization is not null)
                {
                    throw new ArgumentException(
                        $"Visual action '{action}' cannot carry source-asset " +
                        "materialization evidence.",
                        nameof(materialization));
                }

                break;

            case VisualExecutionAction.PreserveMeaningfulVisual:
                if (materialization is null)
                {
                    throw new ArgumentNullException(
                        nameof(materialization),
                        "PreserveMeaningfulVisual requires source-asset materialization evidence.");
                }

                if (materialization.SourceVisualIndex !=
                    sourceVisualIndex)
                {
                    throw new ArgumentException(
                        "Materialization source visual index does not match the execution element.",
                        nameof(materialization));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(action));
        }

        SourceVisualIndex =
            sourceVisualIndex;

        Action =
            action;

        Materialization =
            materialization;
    }

    public int SourceVisualIndex { get; }

    public VisualExecutionAction Action { get; }

    public SourceVisualAssetMaterialization? Materialization { get; }
}
