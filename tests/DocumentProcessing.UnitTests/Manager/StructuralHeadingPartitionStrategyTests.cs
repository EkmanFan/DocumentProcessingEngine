using DocumentProcessing.Manager.Partitioning;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class StructuralHeadingPartitionStrategyTests
{
    #region Tests

    [Fact]
    public void TryPropose_PdfLikeHeadingsBuildCompleteFallbackSegments()
    {
        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                new StructuralHeadingPartitionStrategy()
                    .TryPropose(
                        new DocumentPartitionEvidence(
                            new DocumentPartitionAxis.PhysicalPages(
                                physicalPageCount:
                                    30),
                            [
                                Boundary(
                                    new DocumentPartitionPosition.PhysicalPage(5),
                                    "Chapter 1",
                                    sourceOrder:
                                        0),
                                Boundary(
                                    new DocumentPartitionPosition.PhysicalPage(18),
                                    "Chapter 2",
                                    sourceOrder:
                                        1)
                            ])));

        Assert.Equal(
            StructuralHeadingPartitionStrategy.StructuralHeadingStrategyId,
            proposal.StrategyId);

        Assert.Equal(
            DocumentPartitionProposalReliability.Fallback,
            proposal.Reliability);

        Assert.Collection(
            proposal.Segments,
            segment =>
                AssertPhysicalSegment(
                    segment,
                    expectedTitle:
                        null,
                    1,
                    4),
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Chapter 1",
                    5,
                    17),
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Chapter 2",
                    18,
                    30));
    }

    [Fact]
    public void TryPropose_EpubLikeHeadingsUseStableContentUnits()
    {
        var axis =
            new DocumentPartitionAxis.ContentUnits(
                [
                    "front.xhtml",
                    "chapter-1.xhtml",
                    "chapter-2.xhtml"
                ]);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                new StructuralHeadingPartitionStrategy()
                    .TryPropose(
                        new DocumentPartitionEvidence(
                            axis,
                            [
                                Boundary(
                                    new DocumentPartitionPosition.ContentUnit(
                                        1,
                                        "chapter-1.xhtml"),
                                    "Chapter 1",
                                    sourceOrder:
                                        0),
                                Boundary(
                                    new DocumentPartitionPosition.ContentUnit(
                                        2,
                                        "chapter-2.xhtml"),
                                    "Chapter 2",
                                    sourceOrder:
                                        1)
                            ])));

        Assert.IsType<DocumentPartitionAxis.ContentUnits>(
            proposal.Axis);

        Assert.Equal(
            0,
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                    proposal.Segments[0].Extent.Start)
                .ContentUnitIndex);

        Assert.Equal(
            2,
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                    proposal.Segments[^1].Extent.End)
                .ContentUnitIndex);
    }

    [Fact]
    public void TryPropose_DuplicateHeadingLevelOnOneCoordinateFailsClosed()
    {
        var proposal =
            new StructuralHeadingPartitionStrategy()
                .TryPropose(
                    new DocumentPartitionEvidence(
                        new DocumentPartitionAxis.PhysicalPages(
                            physicalPageCount:
                                20),
                        [
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(2),
                                "Chapter 1",
                                sourceOrder:
                                    0),
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(2),
                                "Ambiguous peer",
                                sourceOrder:
                                    1),
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(10),
                                "Chapter 2",
                                sourceOrder:
                                    2)
                        ]));

        Assert.Null(
            proposal);
    }

    [Fact]
    public void TryPropose_NonMonotonicHeadingsFailClosed()
    {
        var proposal =
            new StructuralHeadingPartitionStrategy()
                .TryPropose(
                    new DocumentPartitionEvidence(
                        new DocumentPartitionAxis.PhysicalPages(
                            physicalPageCount:
                                20),
                        [
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(10),
                                "First in reading order",
                                sourceOrder:
                                    0),
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(5),
                                "Second in reading order",
                                sourceOrder:
                                    1)
                        ]));

        Assert.Null(
            proposal);
    }

    [Fact]
    public void TryPropose_AmbiguousShallowLevelDoesNotFallThroughToDeeperHeadings()
    {
        var proposal =
            new StructuralHeadingPartitionStrategy()
                .TryPropose(
                    new DocumentPartitionEvidence(
                        new DocumentPartitionAxis.PhysicalPages(
                            physicalPageCount:
                                20),
                        [
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(2),
                                "First top-level title",
                                sourceOrder:
                                    0),
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(2),
                                "Ambiguous top-level peer",
                                sourceOrder:
                                    1),
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(5),
                                "Section 1",
                                sourceOrder:
                                    2,
                                hierarchyLevel:
                                    1),
                            Boundary(
                                new DocumentPartitionPosition.PhysicalPage(10),
                                "Section 2",
                                sourceOrder:
                                    3,
                                hierarchyLevel:
                                    1)
                        ]));

        Assert.Null(
            proposal);
    }

    #endregion

    #region Methods

    private static DocumentPartitionBoundary Boundary(
        DocumentPartitionPosition position,
        string title,
        int sourceOrder,
        int hierarchyLevel = 0) =>
        new(
            position,
            title,
            hierarchyLevel,
            sourceOrder,
            DocumentPartitionEvidenceOrigin.StructuralHeading);

    private static void AssertPhysicalSegment(
        DocumentPartitionSegment segment,
        string? expectedTitle,
        int expectedStart,
        int expectedEnd)
    {
        Assert.Equal(
            expectedTitle,
            segment.SuggestedTitle);

        Assert.Equal(
            expectedStart,
            Assert.IsType<DocumentPartitionPosition.PhysicalPage>(
                    segment.Extent.Start)
                .PhysicalPageNumber);

        Assert.Equal(
            expectedEnd,
            Assert.IsType<DocumentPartitionPosition.PhysicalPage>(
                    segment.Extent.End)
                .PhysicalPageNumber);
    }

    #endregion
}
