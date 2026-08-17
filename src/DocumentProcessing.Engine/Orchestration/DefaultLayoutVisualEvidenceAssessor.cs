using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Classifies layout-detected Figure regions using layout evidence only.
///
/// A Figure label alone is never treated as meaningful-visual evidence.
/// A Figure receives <see cref="VisualEvidenceKind.CaptionedMeaningfulVisual"/>
/// only when exactly one caption satisfies the existing strong spatial
/// Figure/Caption relation. Ambiguous or unsupported Figure evidence fails
/// closed to <see cref="VisualEvidenceKind.Unknown"/>.
/// </summary>
public sealed class DefaultLayoutVisualEvidenceAssessor
{
    // Reuses the already validated real-corpus Figure/Caption spatial oracle.
    private const double MinimumHorizontalOverlap =
        0.40;

    private const double MaximumVerticalGap =
        0.08;

    public IReadOnlyList<LayoutVisualEvidence> Assess(
        LayoutAnalysisResult layout)
    {
        ArgumentNullException.ThrowIfNull(
            layout);

        return layout.Observations
            .Where(
                observation =>
                    observation.Kind ==
                    LayoutObservationKind.Figure)
            .Select(
                figure =>
                    new LayoutVisualEvidence(
                        figure,
                        Classify(
                            figure,
                            layout.Observations)))
            .ToArray();
    }

    private static VisualEvidenceKind Classify(
        LayoutObservation figure,
        IReadOnlyList<LayoutObservation> observations)
    {
        var matchingCaptionCount =
            observations.Count(
                candidate =>
                    candidate.Kind ==
                        LayoutObservationKind.Caption &&
                    IsStrongCaptionAssociation(
                        figure,
                        candidate));

        return matchingCaptionCount ==
                1
            ? VisualEvidenceKind.CaptionedMeaningfulVisual
            : VisualEvidenceKind.Unknown;
    }

    private static bool IsStrongCaptionAssociation(
        LayoutObservation figure,
        LayoutObservation caption)
    {
        var figureOrder =
            figure.ReadingOrder ??
            figure.ObservationSequence;

        var captionOrder =
            caption.ReadingOrder ??
            caption.ObservationSequence;

        if (figureOrder >=
            captionOrder)
        {
            return false;
        }

        var horizontalOverlap =
            OverlapRatio(
                figure.Bounds.Left,
                figure.Bounds.Right,
                caption.Bounds.Left,
                caption.Bounds.Right);

        var verticalGap =
            AxisGap(
                figure.Bounds.Top,
                figure.Bounds.Bottom,
                caption.Bounds.Top,
                caption.Bounds.Bottom);

        return horizontalOverlap >=
                   MinimumHorizontalOverlap &&
               verticalGap <=
                   MaximumVerticalGap;
    }

    private static double OverlapRatio(
        double a0,
        double a1,
        double b0,
        double b1)
    {
        var overlap =
            Math.Max(
                0,
                Math.Min(
                    a1,
                    b1) -
                Math.Max(
                    a0,
                    b0));

        var denominator =
            Math.Min(
                a1 -
                a0,
                b1 -
                b0);

        return denominator <=
                0
            ? 0
            : overlap /
              denominator;
    }

    private static double AxisGap(
        double a0,
        double a1,
        double b0,
        double b1)
    {
        if (a1 <
            b0)
        {
            return b0 -
                   a1;
        }

        if (b1 <
            a0)
        {
            return a0 -
                   b1;
        }

        return 0;
    }
}
