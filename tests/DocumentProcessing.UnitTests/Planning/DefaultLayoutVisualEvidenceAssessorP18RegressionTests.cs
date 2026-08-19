using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.UnitTests.Planning;

public sealed class DefaultLayoutVisualEvidenceAssessorP18RegressionTests
{
    #region Variables and Constants

    #endregion

    #region ctor

    #endregion

    #region Methods Tests

    [Fact]
    public void Assess_P18LikeHorizontalFigure_RemainsUnknownWithoutSourceEvidence()
    {
        var figure =
            new LayoutObservation(
                physicalPageNumber:
                    1,
                observationSequence:
                    1,
                readingOrder:
                    1,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.120516,
                    0.219213,
                    0.871020,
                    0.300456),
                rawLabel:
                    "image");

        var evidence =
            Assert.Single(
                new DefaultLayoutVisualEvidenceAssessor()
                    .Assess(
                        new LayoutAnalysisResult(
                            "fake-layout",
                            physicalPageNumber:
                                1,
                            [
                                figure
                            ])));

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            evidence.Kind);
    }

    [Fact]
    public void VisualDisposition_SourceBackedMeaningfulVisual_Preserves()
    {
        Assert.Equal(
            VisualDisposition.PreserveMeaningfulVisual,
            VisualEvidenceDispositionPolicy
                .Decide(
                    VisualEvidenceKind.SourceBackedMeaningfulVisual));
    }

    #endregion
}
