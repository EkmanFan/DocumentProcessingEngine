using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Layout;

namespace DocumentProcessing.UnitTests.Layout;

public sealed class LayoutTextPolicyTests
{
    [Theory]
    [InlineData(LayoutObservationKind.Text, true)]
    [InlineData(LayoutObservationKind.Heading, true)]
    [InlineData(LayoutObservationKind.Caption, true)]
    [InlineData(LayoutObservationKind.Table, true)]
    [InlineData(LayoutObservationKind.Figure, false)]
    [InlineData(LayoutObservationKind.Unknown, false)]
    public void IsTextRecognitionCandidate_MapsOnlyTextualKinds(
        LayoutObservationKind kind,
        bool expected)
    {
        Assert.Equal(
            expected,
            LayoutTextPolicy.IsTextRecognitionCandidate(
                kind));
    }

    [Fact]
    public void IsTextRecognitionCandidate_UndefinedKind_FailsClosed()
    {
        Assert.False(
            LayoutTextPolicy.IsTextRecognitionCandidate(
                (LayoutObservationKind)int.MaxValue));
    }
}
