namespace DocumentProcessing.Manager.History;

/// <summary>Defines deterministic archive ordering.</summary>
public enum ProcessingArchiveSort
{
    CompletedNewest,
    CompletedOldest,
    TitleAscending,
    TitleDescending
}
