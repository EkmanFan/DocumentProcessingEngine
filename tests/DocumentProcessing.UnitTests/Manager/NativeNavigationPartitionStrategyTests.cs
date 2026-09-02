using DocumentProcessing.Manager.Partitioning;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class NativeNavigationPartitionStrategyTests
{
    #region Tests

    [Fact]
    public void TryPropose_PdfLikeNavigationBuildsCompletePhysicalPageSegments()
    {
        var evidence =
            new DocumentPartitionEvidence(
                new DocumentPartitionAxis.PhysicalPages(
                    physicalPageCount:
                        30),
                [
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(1),
                        "Introduction",
                        sourceOrder:
                            0),
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(11),
                        "Main argument",
                        sourceOrder:
                            1),
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(21),
                        "Conclusion",
                        sourceOrder:
                            2)
                ]);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                new NativeNavigationPartitionStrategy()
                    .TryPropose(
                        evidence));

        Assert.Equal(
            NativeNavigationPartitionStrategy.NativeNavigationStrategyId,
            proposal.StrategyId);

        Assert.Equal(
            DocumentPartitionProposalReliability.Qualified,
            proposal.Reliability);

        Assert.Collection(
            proposal.Segments,
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Introduction",
                    1,
                    10),
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Main argument",
                    11,
                    20),
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Conclusion",
                    21,
                    30));
    }

    [Fact]
    public void TryPropose_EpubLikeNavigationUsesContentUnitsWithoutInventingPages()
    {
        var axis =
            new DocumentPartitionAxis.ContentUnits(
                [
                    "front.xhtml",
                    "chapter-1.xhtml",
                    "chapter-2.xhtml",
                    "notes.xhtml"
                ]);

        var evidence =
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
                ]);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                new NativeNavigationPartitionStrategy()
                    .TryPropose(
                        evidence));

        Assert.IsType<DocumentPartitionAxis.ContentUnits>(
            proposal.Axis);

        Assert.Collection(
            proposal.Segments,
            segment =>
                AssertContentUnitSegment(
                    segment,
                    expectedTitle:
                        null,
                    0,
                    "front.xhtml",
                    0,
                    "front.xhtml"),
            segment =>
                AssertContentUnitSegment(
                    segment,
                    "Chapter 1",
                    1,
                    "chapter-1.xhtml",
                    1,
                    "chapter-1.xhtml"),
            segment =>
                AssertContentUnitSegment(
                    segment,
                    "Chapter 2",
                    2,
                    "chapter-2.xhtml",
                    3,
                    "notes.xhtml"));
    }

    [Fact]
    public void TryPropose_SelectsShallowestHierarchyLevelWithTwoUsableBoundaries()
    {
        var evidence =
            new DocumentPartitionEvidence(
                new DocumentPartitionAxis.PhysicalPages(
                    physicalPageCount:
                        20),
                [
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(1),
                        "Book",
                        sourceOrder:
                            0,
                        hierarchyLevel:
                            0),
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(1),
                        "Chapter 1",
                        sourceOrder:
                            1,
                        hierarchyLevel:
                            1),
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(10),
                        "Chapter 2",
                        sourceOrder:
                            2,
                        hierarchyLevel:
                            1)
                ]);

        var proposal =
            Assert.IsType<DocumentPartitionProposal>(
                new NativeNavigationPartitionStrategy()
                    .TryPropose(
                        evidence));

        Assert.Collection(
            proposal.Segments,
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Chapter 1",
                    1,
                    9),
            segment =>
                AssertPhysicalSegment(
                    segment,
                    "Chapter 2",
                    10,
                    20));
    }

    [Fact]
    public void TryPropose_NonMonotonicNativeNavigationFailsClosed()
    {
        var evidence =
            new DocumentPartitionEvidence(
                new DocumentPartitionAxis.PhysicalPages(
                    physicalPageCount:
                        20),
                [
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(10),
                        "First in outline",
                        sourceOrder:
                            0),
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(5),
                        "Second in outline",
                        sourceOrder:
                            1)
                ]);

        var proposal =
            new NativeNavigationPartitionStrategy()
                .TryPropose(
                    evidence);

        Assert.Null(
            proposal);
    }

    [Fact]
    public void TryPropose_OneNativeBoundaryDoesNotInventAPlan()
    {
        var evidence =
            new DocumentPartitionEvidence(
                new DocumentPartitionAxis.PhysicalPages(
                    physicalPageCount:
                        20),
                [
                    Boundary(
                        new DocumentPartitionPosition.PhysicalPage(5),
                        "Only boundary",
                        sourceOrder:
                            0)
                ]);

        Assert.Null(
            new NativeNavigationPartitionStrategy()
                .TryPropose(
                    evidence));
    }

    [Fact]
    public void Evidence_RejectsPositionsFromAnotherCoordinateAxis()
    {
        var axis =
            new DocumentPartitionAxis.PhysicalPages(
                physicalPageCount:
                    10);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentPartitionEvidence(
                        axis,
                        [
                            Boundary(
                                new DocumentPartitionPosition.ContentUnit(
                                    0,
                                    "chapter.xhtml"),
                                "Chapter",
                                sourceOrder:
                                    0)
                        ]));

        Assert.Equal(
            "boundaries",
            exception.ParamName);
    }

    [Fact]
    public void Proposal_RejectsAnyGapThatCouldDiscardSourceContent()
    {
        var axis =
            new DocumentPartitionAxis.PhysicalPages(
                physicalPageCount:
                    10);

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentPartitionProposal(
                        "unsafe-test",
                        axis,
                        DocumentPartitionProposalReliability.Fallback,
                        [
                            new DocumentPartitionSegment(
                                "First",
                                new DocumentPartitionExtent(
                                    new DocumentPartitionPosition.PhysicalPage(1),
                                    new DocumentPartitionPosition.PhysicalPage(4))),
                            new DocumentPartitionSegment(
                                "Second",
                                new DocumentPartitionExtent(
                                    new DocumentPartitionPosition.PhysicalPage(6),
                                    new DocumentPartitionPosition.PhysicalPage(10)))
                        ]));

        Assert.Equal(
            "segments",
            exception.ParamName);
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
            DocumentPartitionEvidenceOrigin.NativeNavigation);

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

    private static void AssertContentUnitSegment(
        DocumentPartitionSegment segment,
        string? expectedTitle,
        int expectedStartIndex,
        string expectedStartId,
        int expectedEndIndex,
        string expectedEndId)
    {
        Assert.Equal(
            expectedTitle,
            segment.SuggestedTitle);

        var start =
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                segment.Extent.Start);

        var end =
            Assert.IsType<DocumentPartitionPosition.ContentUnit>(
                segment.Extent.End);

        Assert.Equal(
            expectedStartIndex,
            start.ContentUnitIndex);

        Assert.Equal(
            expectedStartId,
            start.ContentUnitId);

        Assert.Equal(
            expectedEndIndex,
            end.ContentUnitIndex);

        Assert.Equal(
            expectedEndId,
            end.ContentUnitId);
    }

    #endregion
}
