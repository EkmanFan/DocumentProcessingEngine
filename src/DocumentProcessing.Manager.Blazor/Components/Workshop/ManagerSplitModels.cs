namespace DocumentProcessing.Manager.Blazor.Components.Workshop;

/// <summary>Reports a newly submitted document to the workshop.</summary>
public sealed record ManagerDocumentSubmissionNotice(
    string OriginalFileName,
    Guid ProcessingUnitId,
    ManagerDocumentSubmissionBehavior RequestedBehavior);

/// <summary>Describes one editable range on a native document axis.</summary>
public abstract record ManagerSplitRangeDraft
{
    private ManagerSplitRangeDraft()
    {
    }

    /// <summary>Describes one editable physical-page range.</summary>
    public sealed record PhysicalPageRange(
        int StartPhysicalPageNumber,
        int EndPhysicalPageNumber,
        string Title)
        : ManagerSplitRangeDraft;

    /// <summary>Describes one editable ordered-content-unit range.</summary>
    public sealed record ContentUnitRange(
        int StartContentUnitIndex,
        string StartContentUnitId,
        int EndContentUnitIndex,
        string EndContentUnitId,
        string Title)
        : ManagerSplitRangeDraft;
}
