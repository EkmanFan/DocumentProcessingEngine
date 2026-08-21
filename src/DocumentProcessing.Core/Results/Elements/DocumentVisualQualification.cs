namespace DocumentProcessing.Core.Results;

/// <summary>
/// Semantic qualification attached to one preserved visual asset.
/// </summary>
public enum DocumentVisualQualification
{
    /// <summary>
    /// Available evidence did not qualify the preserved asset as meaningful.
    /// </summary>
    Unqualified = 0,

    /// <summary>
    /// Engine policy determined that the visual carries documentary meaning.
    /// </summary>
    Meaningful = 1
}
