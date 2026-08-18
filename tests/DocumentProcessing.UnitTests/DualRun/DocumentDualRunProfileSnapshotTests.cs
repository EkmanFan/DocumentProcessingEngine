using DocumentProcessing.Core.DualRun;

namespace DocumentProcessing.UnitTests.DualRun;

public sealed class DocumentDualRunProfileSnapshotTests
{
    #region Variables and Constants

    private const string SourceSha =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    #endregion

    #region Methods Snapshot

    [Fact]
    public void Resolve_Disabled_IsNeverSelected()
    {
        var snapshot =
            new DocumentDualRunProfileSnapshot(
                DocumentDualRunProfile.Disabled);

        var selection =
            snapshot.Resolve(
                SourceSha);

        Assert.False(
            selection.IsSelected);

        Assert.Null(
            selection.ExecutionMode);
    }

    [Fact]
    public void Resolve_PlanningOnly_ResolvesPlanningOnly()
    {
        var snapshot =
            new DocumentDualRunProfileSnapshot(
                DocumentDualRunProfile.PlanningOnly);

        var selection =
            snapshot.Resolve(
                SourceSha);

        Assert.True(
            selection.IsSelected);

        Assert.Equal(
            DocumentDualRunExecutionMode.PlanningOnly,
            selection.ExecutionMode);
    }

    [Fact]
    public void Resolve_Full_ResolvesFull()
    {
        var snapshot =
            new DocumentDualRunProfileSnapshot(
                DocumentDualRunProfile.Full);

        var selection =
            snapshot.Resolve(
                SourceSha);

        Assert.True(
            selection.IsSelected);

        Assert.Equal(
            DocumentDualRunExecutionMode.Full,
            selection.ExecutionMode);
    }

    [Fact]
    public void Resolve_SampledZero_IsStableAndUnselected()
    {
        var snapshot =
            new DocumentDualRunProfileSnapshot(
                DocumentDualRunProfile.Sampled,
                sampledBasisPoints:
                    0);

        var first =
            snapshot.Resolve(
                SourceSha);

        var second =
            snapshot.Resolve(
                SourceSha);

        Assert.False(
            first.IsSelected);

        Assert.Equal(
            first.SamplingBucket,
            second.SamplingBucket);

        Assert.Null(
            first.ExecutionMode);
    }

    [Fact]
    public void Constructor_InvalidBasisPoints_FailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentDualRunProfileSnapshot(
                    DocumentDualRunProfile.Sampled,
                    DocumentDualRunProfileSelector
                        .SamplingResolution +
                    1));
    }

    #endregion
}
