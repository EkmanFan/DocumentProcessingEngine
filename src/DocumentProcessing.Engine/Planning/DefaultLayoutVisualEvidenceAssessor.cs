using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Classifies layout-detected Figure regions using layout evidence only.
///
/// A Figure label alone is never treated as meaningful-visual evidence.
/// A Figure receives <see cref="VisualEvidenceKind.CaptionedMeaningfulVisual"/>
/// when exactly one caption satisfies the existing strong spatial
/// Figure/Caption relation. A sufficiently large Figure may instead receive
/// <see cref="VisualEvidenceKind.LargeIndependentVisual"/> when it has no
/// caption evidence and remains spatially independent from semantic text-like
/// layout observations. Ambiguous or unsupported Figure evidence fails closed
/// to <see cref="VisualEvidenceKind.Unknown"/>.
/// </summary>
public sealed class DefaultLayoutVisualEvidenceAssessor
{
    #region Variables and Constants

    // Reuses the already validated real-corpus Figure/Caption spatial oracle.
    private const double MinimumHorizontalOverlap =
        0.40;

    private const double MaximumVerticalGap =
        0.08;

    // Unsupported figures remain fail-closed unless they occupy a substantial
    // fraction of the visible page and are spatially independent from semantic
    // text-like layout observations.
    private const double MinimumIndependentFigureVisibleAreaRatio =
        0.25;

    #endregion

    #region ctor

    #endregion

    #region Methods Assessment

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
        var captions =
            observations
                .Where(
                    candidate =>
                        candidate.Kind ==
                        LayoutObservationKind.Caption)
                .ToArray();

        var matchingCaptionCount =
            captions.Count(
                caption =>
                    IsStrongCaptionAssociation(
                        figure,
                        caption));

        if (matchingCaptionCount ==
            1)
        {
            return VisualEvidenceKind.CaptionedMeaningfulVisual;
        }

        if (captions.Length >
            0)
        {
            return VisualEvidenceKind.Unknown;
        }

        return IsLargeIndependentVisual(
                figure,
                observations)
            ? VisualEvidenceKind.LargeIndependentVisual
            : VisualEvidenceKind.Unknown;
    }

    private static bool IsLargeIndependentVisual(
        LayoutObservation figure,
        IReadOnlyList<LayoutObservation> observations)
    {
        if (VisiblePageAreaRatio(
                figure.Bounds) <
            MinimumIndependentFigureVisibleAreaRatio)
        {
            return false;
        }

        return !observations.Any(
            candidate =>
                IsSemanticTextLike(
                    candidate.Kind) &&
                Intersects(
                    figure.Bounds,
                    candidate.Bounds));
    }

    private static bool IsSemanticTextLike(
        LayoutObservationKind kind) =>
        kind is
            LayoutObservationKind.Text or
            LayoutObservationKind.Heading or
            LayoutObservationKind.Table;

    #endregion

    #region Methods Geometry

    private static double VisiblePageAreaRatio(
        NormalizedRectangle bounds)
    {
        var left =
            Math.Clamp(
                bounds.Left,
                0,
                1);

        var top =
            Math.Clamp(
                bounds.Top,
                0,
                1);

        var right =
            Math.Clamp(
                bounds.Right,
                0,
                1);

        var bottom =
            Math.Clamp(
                bounds.Bottom,
                0,
                1);

        return Math.Max(
                   0,
                   right -
                   left) *
               Math.Max(
                   0,
                   bottom -
                   top);
    }

    private static bool Intersects(
        NormalizedRectangle first,
        NormalizedRectangle second) =>
        Math.Min(
            first.Right,
            second.Right) >
        Math.Max(
            first.Left,
            second.Left) &&
        Math.Min(
            first.Bottom,
            second.Bottom) >
        Math.Max(
            first.Top,
            second.Top);

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

    #endregion
}
