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
