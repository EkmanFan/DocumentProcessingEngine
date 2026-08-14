using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Layout;

namespace DocumentProcessing.UnitTests.Layout;

public sealed class LayoutTreatmentPolicyTests
{
    [Theory]
    [InlineData(
        LayoutObservationKind.Text,
        LayoutTreatment.RecognizeText)]
    [InlineData(
        LayoutObservationKind.Heading,
        LayoutTreatment.RecognizeText)]
    [InlineData(
        LayoutObservationKind.Caption,
        LayoutTreatment.RecognizeText)]
    [InlineData(
        LayoutObservationKind.Figure,
        LayoutTreatment.PreserveVisualWithoutOcr)]
    [InlineData(
        LayoutObservationKind.Table,
        LayoutTreatment.Deferred)]
    [InlineData(
        LayoutObservationKind.Unknown,
        LayoutTreatment.Deferred)]
    public void Decide_MapsKnownKindsConservatively(
        LayoutObservationKind kind,
        LayoutTreatment expected)
    {
        var actual =
            LayoutTreatmentPolicy.Decide(kind);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Decide_UnknownEnumValue_FailsClosedToDeferred()
    {
        var undefinedKind =
            (LayoutObservationKind)int.MaxValue;

        var actual =
            LayoutTreatmentPolicy.Decide(undefinedKind);

        Assert.Equal(
            LayoutTreatment.Deferred,
            actual);
    }

    [Fact]
    public void Decide_Ehrman233RepresentativeSequence_DoesNotOcrFigure()
    {
        LayoutObservationKind[] kinds =
        [
            LayoutObservationKind.Heading,
            LayoutObservationKind.Text,
            LayoutObservationKind.Figure,
            LayoutObservationKind.Caption,
            LayoutObservationKind.Text
        ];

        var treatments =
            kinds
                .Select(LayoutTreatmentPolicy.Decide)
                .ToArray();

        Assert.Equal(
            [
                LayoutTreatment.RecognizeText,
                LayoutTreatment.RecognizeText,
                LayoutTreatment.PreserveVisualWithoutOcr,
                LayoutTreatment.RecognizeText,
                LayoutTreatment.RecognizeText
            ],
            treatments);

        Assert.NotEqual(
            LayoutTreatment.RecognizeText,
            treatments[2]);
    }
}
