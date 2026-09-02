namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Identifies one observable stage of the format-neutral processing pipeline.
/// </summary>
public enum DocumentProcessingProgressStage
{
    /// <summary>The replayable source and its custody identity are prepared.</summary>
    PreparingSource = 0,

    /// <summary>The document format and its native evidence are inspected.</summary>
    InspectingFormat = 1,

    /// <summary>The acquired evidence is assessed and an execution plan is built.</summary>
    Planning = 2,

    /// <summary>Layout or visual evidence is acquired for selected content.</summary>
    AnalyzingContent = 3,

    /// <summary>The selected pages or native content units are processed.</summary>
    ProcessingContent = 4,

    /// <summary>The portable processing result is assembled.</summary>
    AssemblingResult = 5
}
