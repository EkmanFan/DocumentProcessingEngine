using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Default deterministic two-axis policy.
///
/// Text authority remains authoritative for Missing and Corrupted text.
/// NeedsVerification may be resolved to native text only when at least one
/// visual occurrence was classified and every visual has a non-ambiguous
/// disposition. Unknown visual evidence therefore fails closed.
///
/// This policy selects requirements only. It does not select or execute a
/// <see cref="PageProcessingRoute"/>.
/// </summary>
public sealed class DefaultPageProcessingRequirementsPolicy
    : IPageProcessingRequirementsPolicy
{
    #region Variables and Constants

    #endregion

    #region ctor

    #endregion

    #region Methods

    public PageProcessingRequirements Decide(
        PageProcessingEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        var visualElements =
            evidence.VisualElements
                .Select(
                    DecideVisualDisposition)
                .ToArray();

        var textRequirement =
            DecideTextRequirement(
                evidence.TextAuthority,
                visualElements);

        return new PageProcessingRequirements(
            evidence.PhysicalPageNumber,
            textRequirement,
            visualElements);
    }

    private static VisualElementDisposition DecideVisualDisposition(
        VisualElementEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        var disposition =
            VisualEvidenceDispositionPolicy.Decide(
                evidence.Kind);

        return new VisualElementDisposition(
            evidence.SourceVisualIndex,
            disposition);
    }

    private static TextProcessingRequirement DecideTextRequirement(
        TextAuthority textAuthority,
        IReadOnlyList<VisualElementDisposition> visualElements) =>
        textAuthority switch
        {
            TextAuthority.Missing =>
                TextProcessingRequirement.RecoverMissingNativeText,

            TextAuthority.Corrupted =>
                TextProcessingRequirement.ReconcileCorruptedNativeText,

            TextAuthority.Trusted =>
                TextProcessingRequirement.UseNativeText,

            TextAuthority.NeedsVerification =>
                CanResolveNativeTextVerification(
                    visualElements)
                    ? TextProcessingRequirement.UseNativeText
                    : TextProcessingRequirement.VerifyNativeText,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(textAuthority),
                    textAuthority,
                    "Unsupported text authority.")
        };

    private static bool CanResolveNativeTextVerification(
        IReadOnlyList<VisualElementDisposition> visualElements)
    {
        if (visualElements.Count ==
            0)
        {
            return false;
        }

        return visualElements.All(
            visual =>
                visual.Disposition !=
                VisualDisposition.RequiresVisualAnalysis);
    }

    #endregion
}
