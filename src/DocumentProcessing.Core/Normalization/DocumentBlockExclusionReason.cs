namespace DocumentProcessing.Core.Normalization;

/// <summary>
/// Deterministic reason for excluding a normalized block from downstream
/// content flow while retaining the original source evidence.
/// </summary>
public enum DocumentBlockExclusionReason
{
    RepeatedHeader = 0,
    RepeatedFooter = 1,
    NoteContent = 2
}
