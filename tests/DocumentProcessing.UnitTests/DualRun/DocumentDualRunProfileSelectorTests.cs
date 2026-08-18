using DocumentProcessing.Core.DualRun;

namespace DocumentProcessing.UnitTests.DualRun;

public sealed class DocumentDualRunProfileSelectorTests
{
    #region Methods Selection

    [Fact]
    public void Select_Disabled_PerformsNoDualRunWork()
    {
        var selection =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.Disabled);

        Assert.False(
            selection.IsSelected);

        Assert.Null(
            selection.ExecutionMode);

        Assert.Null(
            selection.SamplingBucket);
    }

    [Fact]
    public void Select_PlanningOnly_ResolvesPlanningOnly()
    {
        var selection =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.PlanningOnly);

        Assert.True(
            selection.IsSelected);

        Assert.Equal(
            DocumentDualRunExecutionMode.PlanningOnly,
            selection.ExecutionMode);

        Assert.Null(
            selection.SamplingBucket);
    }

    [Fact]
    public void Select_Full_ResolvesFull()
    {
        var selection =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.Full);

        Assert.True(
            selection.IsSelected);

        Assert.Equal(
            DocumentDualRunExecutionMode.Full,
            selection.ExecutionMode);

        Assert.Null(
            selection.SamplingBucket);
    }

    [Fact]
    public void Select_Sampled_IsStableForSameSourceHash()
    {
        const string sha256 =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var first =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.Sampled,
                    sha256,
                    sampledBasisPoints:
                        2500);

        var second =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.Sampled,
                    sha256,
                    sampledBasisPoints:
                        2500);

        Assert.Equal(
            first,
            second);

        Assert.NotNull(
            first.SamplingBucket);

        Assert.InRange(
            first.SamplingBucket!.Value,
            0,
            DocumentDualRunProfileSelector.SamplingResolution -
            1);
    }

    [Fact]
    public void Select_SampledZero_SelectsNothing()
    {
        const string sha256 =
            "0000000000000000000000000000000000000000000000000000000000000000";

        var selection =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.Sampled,
                    sha256,
                    sampledBasisPoints:
                        0);

        Assert.False(
            selection.IsSelected);

        Assert.Null(
            selection.ExecutionMode);

        Assert.Equal(
            0,
            selection.SamplingBucket);
    }

    [Fact]
    public void Select_SampledFullResolution_SelectsEverythingAsFull()
    {
        const string sha256 =
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

        var selection =
            DocumentDualRunProfileSelector
                .Select(
                    DocumentDualRunProfile.Sampled,
                    sha256,
                    DocumentDualRunProfileSelector.SamplingResolution);

        Assert.True(
            selection.IsSelected);

        Assert.Equal(
            DocumentDualRunExecutionMode.Full,
            selection.ExecutionMode);

        Assert.NotNull(
            selection.SamplingBucket);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Select_Sampled_InvalidSha_FailsClosed(
        string sha256)
    {
        Assert.Throws<ArgumentException>(
            () =>
                DocumentDualRunProfileSelector
                    .Select(
                        DocumentDualRunProfile.Sampled,
                        sha256,
                        sampledBasisPoints:
                            1000));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Select_InvalidSamplingBasisPoints_FailsClosed(
        int sampledBasisPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                DocumentDualRunProfileSelector
                    .Select(
                        DocumentDualRunProfile.Sampled,
                        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                        sampledBasisPoints));
    }

    [Fact]
    public void Select_UndefinedProfile_FailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                DocumentDualRunProfileSelector
                    .Select(
                        (DocumentDualRunProfile)int.MaxValue));
    }

    #endregion
}
