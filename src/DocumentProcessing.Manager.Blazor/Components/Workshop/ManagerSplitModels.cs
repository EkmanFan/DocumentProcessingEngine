namespace DocumentProcessing.Manager.Blazor.Components.Workshop;

/// <summary>Reports a newly submitted document to the workshop.</summary>
public sealed record ManagerDocumentSubmissionNotice(
    string OriginalFileName,
    Guid ProcessingUnitId,
    ManagerDocumentSubmissionBehavior RequestedBehavior);

/// <summary>Describes one editable physical-page range.</summary>
public sealed record ManagerSplitRangeDraft(
    int StartPhysicalPageNumber,
    int EndPhysicalPageNumber,
    string Title);
