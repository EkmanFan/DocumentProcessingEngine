using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerResultDownloadFileNameTests
{
    #region Tests

    [Fact]
    public void Create_UsesSafeSourceStemForWholeDocument()
    {
        var result =
            ManagerResultDownloadFileName.Create(
                "/untrusted/path/Historical Theology_ Gregg Allison.pdf",
                WholeDocument());

        Assert.Equal(
            "Historical-Theology_-Gregg-Allison.dpengine-result.json",
            result);
    }

    [Fact]
    public void Create_AddsPhysicalPagesForPageRange()
    {
        var result =
            ManagerResultDownloadFileName.Create(
                "DeCretis.pdf",
                new ManagerWorkItemScopeView(
                    ManagerWorkItemScopeKind.PageRange,
                    StartPhysicalPageNumber:
                        27,
                    EndPhysicalPageNumber:
                        29,
                    Title:
                        "Chapter"));

        Assert.Equal(
            "DeCretis.pages-27-29.dpengine-result.json",
            result);
    }

    [Fact]
    public void Create_AddsOneBasedUnitPositionsForContentUnitRange()
    {
        var result =
            ManagerResultDownloadFileName.Create(
                "Habermas.epub",
                new ManagerWorkItemScopeView(
                    ManagerWorkItemScopeKind.ContentUnitRange,
                    StartPhysicalPageNumber:
                        null,
                    EndPhysicalPageNumber:
                        null,
                    Title:
                        "Chapter",
                    StartContentUnitIndex:
                        4,
                    StartContentUnitId:
                        "OPS/chapter4.xhtml",
                    EndContentUnitIndex:
                        8,
                    EndContentUnitId:
                        "OPS/chapter8.xhtml"));

        Assert.Equal(
            "Habermas.units-5-9.dpengine-result.json",
            result);
    }

    [Fact]
    public void Create_FallsBackWhenSourceStemHasNoSafeCharacters()
    {
        Assert.Equal(
            "document.dpengine-result.json",
            ManagerResultDownloadFileName.Create(
                "...pdf",
                WholeDocument()));
    }

    #endregion

    #region Methods

    private static ManagerWorkItemScopeView WholeDocument() =>
        new(
            ManagerWorkItemScopeKind.WholeDocument,
            StartPhysicalPageNumber:
                null,
            EndPhysicalPageNumber:
                null,
            Title:
                null);

    #endregion
}
