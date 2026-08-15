using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Default deterministic V1 mapping from native-text assessment to one of the
/// execution routes already defined by the Phase 21.0 contract.
/// </summary>
public sealed class DefaultPageProcessingPolicy
    : IPageProcessingPolicy
{
    public PageProcessingPlan Decide(
        PageProcessingAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(
            assessment);

        var route =
            assessment.NativeTextStatus switch
            {
                NativeTextStatus.Healthy =>
                    PageProcessingRoute.NativeOnly,

                NativeTextStatus.Missing =>
                    PageProcessingRoute.LayoutWithTargetedOcrRecovery,

                NativeTextStatus.Suspicious =>
                    PageProcessingRoute.LayoutWithTargetedOcrReconciliation,

                NativeTextStatus.Unverified =>
                    PageProcessingRoute.LayoutWithTargetedOcrReconciliation,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(assessment),
                        assessment.NativeTextStatus,
                        "Unsupported native text status.")
            };

        return new PageProcessingPlan(
            route);
    }
}
