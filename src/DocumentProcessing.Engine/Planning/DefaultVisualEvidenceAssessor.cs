

using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Maps deterministic visual observations to neutral
/// <see cref="VisualEvidenceKind"/> values.
///
/// The rule order and thresholds are frozen regression policy that
/// subsequently passed the recorded blind-holdout validation. This class produces
/// evidence only. It does not select <see cref="VisualDisposition"/> or
/// <see cref="PageProcessingRoute"/>.
/// </summary>
public sealed class DefaultVisualEvidenceAssessor
{
    #region Variables and Constants

    private const double SmallForegroundMaximumRatio =
        0.005;

    private const double SmallHeadingMaximumTouchedWordRatio =
        0.01;

    private const double SmallHeadingMaximumEffectiveAreaRatio =
        0.02;

    private const double TinyMaximumTouchedWordRatio =
        0.02;

    private const int TinyMaximumSignificantComponentCount =
        2;

    private const double LargeIndependentMinimumForegroundRatio =
        0.05;

    #endregion

    #region ctor

    #endregion

    #region Methods

    public VisualElementEvidence Assess(
        VisualEvidenceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        return new VisualElementEvidence(
            observation.SourceVisualIndex,
            Classify(
                observation));
    }

    private static VisualEvidenceKind Classify(
        VisualEvidenceObservation observation)
    {
        if (observation.ForegroundState ==
            VisualForegroundState.BlankCanvas)
        {
            return VisualEvidenceKind.BlankCanvas;
        }

        if (observation.ForegroundState !=
                VisualForegroundState.Measured ||
            observation.ForegroundPixelRatio is null)
        {
            return VisualEvidenceKind.Unknown;
        }

        var foregroundRatio =
            observation.ForegroundPixelRatio.Value;

        // Caption evidence has priority over container evidence. This protects
        // labelled figures such as the p79 regression control.
        if (observation.CaptionAssociation ==
            CaptionAssociationEvidenceKind.StrongAssociation)
        {
            return VisualEvidenceKind.CaptionedMeaningfulVisual;
        }

        if (observation.HeadingAssociation ==
                HeadingAssociationEvidenceKind.StrongAdjacentVisual &&
            foregroundRatio <=
                SmallForegroundMaximumRatio &&
            observation.NativeWordsTouchedRatio <=
                SmallHeadingMaximumTouchedWordRatio &&
            (
                observation.EffectiveVisualAreaRatio is null ||
                observation.EffectiveVisualAreaRatio.Value <=
                    SmallHeadingMaximumEffectiveAreaRatio
            ))
        {
            return VisualEvidenceKind.SmallHeadingAssociatedVisual;
        }

        if (foregroundRatio <=
                SmallForegroundMaximumRatio &&
            (
                observation.SignificantComponentCount is null ||
                observation.SignificantComponentCount.Value <=
                    TinyMaximumSignificantComponentCount
            ) &&
            observation.NativeWordsTouchedRatio <=
                TinyMaximumTouchedWordRatio)
        {
            return VisualEvidenceKind.TinyOrNoise;
        }

        if (observation.TextContainment ==
            NativeTextContainmentEvidenceKind.HeadingDominatedContainedText)
        {
            return VisualEvidenceKind.HeadingBackplateOrPresentation;
        }

        if (observation.TextContainment ==
            NativeTextContainmentEvidenceKind.TextRichContainer)
        {
            return VisualEvidenceKind.NativeTextContainerOrFrame;
        }

        if (foregroundRatio >=
                LargeIndependentMinimumForegroundRatio &&
            observation.PixelInteraction ==
                VisualPixelInteractionKind.NoForegroundWordIntersection &&
            observation.HeadingAssociation !=
                HeadingAssociationEvidenceKind.StrongAdjacentVisual)
        {
            return VisualEvidenceKind.LargeIndependentVisual;
        }

        return VisualEvidenceKind.Unknown;
    }

    #endregion
}
