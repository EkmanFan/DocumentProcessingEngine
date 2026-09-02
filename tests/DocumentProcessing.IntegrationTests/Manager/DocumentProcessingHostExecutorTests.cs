using DocumentProcessing.Manager.DPEngine;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class DocumentProcessingHostExecutorTests
{
    #region Tests

    [Fact]
    public void ContentUnitScope_MapsToNeutralEngineRangeWithoutLosingBoundaries()
    {
        var scope =
            new ProcessingUnitScope.ContentUnitRange(
                4,
                "OPS/chapter4.xhtml",
                8,
                "OPS/chapter8.xhtml",
                "Part two");

        var range =
            DocumentProcessingHostExecutor.ToContentUnitRange(
                scope);

        Assert.NotNull(
            range);

        Assert.Equal(
            4,
            range.StartContentUnitIndex);

        Assert.Equal(
            "OPS/chapter4.xhtml",
            range.StartContentUnitId);

        Assert.Equal(
            8,
            range.EndContentUnitIndex);

        Assert.Equal(
            "OPS/chapter8.xhtml",
            range.EndContentUnitId);
    }

    #endregion
}
