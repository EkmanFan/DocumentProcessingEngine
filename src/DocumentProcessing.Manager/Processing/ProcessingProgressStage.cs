namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Identifies one observable stage of a Manager-owned processing execution.
/// </summary>
public enum ProcessingProgressStage
{
    /// <summary>The source and Manager custody records are being opened.</summary>
    LoadingSource = 0,

    /// <summary>DPEngine is preparing the replayable source.</summary>
    PreparingSource = 1,

    /// <summary>DPEngine is identifying the format and extracting native evidence.</summary>
    InspectingFormat = 2,

    /// <summary>DPEngine is selecting the deterministic execution plan.</summary>
    Planning = 3,

    /// <summary>DPEngine is acquiring selected layout or visual evidence.</summary>
    AnalyzingContent = 4,

    /// <summary>DPEngine is processing pages or native content units.</summary>
    ProcessingContent = 5,

    /// <summary>DPEngine is assembling the portable result.</summary>
    AssemblingResult = 6,

    /// <summary>The Manager is storing the canonical result artifact.</summary>
    StoringResult = 7,

    /// <summary>The Manager is publishing the result and its selected visuals.</summary>
    PublishingResult = 8
}
