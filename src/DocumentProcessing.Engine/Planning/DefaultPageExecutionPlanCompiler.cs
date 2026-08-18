using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Pure deterministic compiler from policy requirements to the engine's current
/// independent text/visual execution mechanisms.
///
/// This component performs no I/O and is not wired into
/// <see cref="DocumentPageProcessingPlanner"/> in Phase 21E.1H.3B.
/// </summary>
public sealed class DefaultPageExecutionPlanCompiler
{
    #region Variables and Constants

    #endregion

    #region ctor

    #endregion

    #region Methods

    public PageExecutionPlan Compile(
        PageProcessingRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(
            requirements);

        var textMode =
            CompileTextMode(
                requirements.TextRequirement);

        var visualElements =
            requirements.VisualElements
                .Select(
                    CompileVisualAction)
                .ToArray();

        return new PageExecutionPlan(
            requirements.PhysicalPageNumber,
            textMode,
            visualElements);
    }

    private static TextExecutionMode CompileTextMode(
        TextProcessingRequirement requirement) =>
        requirement switch
        {
            TextProcessingRequirement.UseNativeText =>
                TextExecutionMode.NativeText,

            TextProcessingRequirement.RecoverMissingNativeText =>
                TextExecutionMode.TargetedOcrRecovery,

            TextProcessingRequirement.VerifyNativeText =>
                TextExecutionMode.TargetedOcrVerification,

            TextProcessingRequirement.ReconcileCorruptedNativeText =>
                TextExecutionMode.TargetedOcrReconciliation,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(requirement),
                    requirement,
                    "Unsupported text processing requirement.")
        };

    private static VisualElementExecutionPlan CompileVisualAction(
        VisualElementDisposition visual)
    {
        ArgumentNullException.ThrowIfNull(
            visual);

        var action =
            visual.Disposition switch
            {
                VisualDisposition.PresentationOnly =>
                    VisualExecutionAction.NoAdditionalSemanticProcessing,

                VisualDisposition.PreserveMeaningfulVisual =>
                    VisualExecutionAction.PreserveMeaningfulVisual,

                VisualDisposition.RequiresVisualAnalysis =>
                    VisualExecutionAction.AnalyzeVisual,

                VisualDisposition.NoVisual =>
                    throw new InvalidOperationException(
                        "NoVisual cannot describe an existing visual element."),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(visual),
                        visual.Disposition,
                        "Unsupported visual disposition.")
            };

        return new VisualElementExecutionPlan(
            visual.SourceVisualIndex,
            action);
    }

    #endregion
}
