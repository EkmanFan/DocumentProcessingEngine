using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Engine.Layout;

/// <summary>
/// Deterministic policy for deciding whether a neutral layout observation is
/// eligible for text recognition.
///
/// This policy owns only the text/OCR axis. It deliberately makes no visual
/// preservation or semantic-meaning decision.
/// </summary>
public static class LayoutTextPolicy
{
    public static bool IsTextRecognitionCandidate(
        LayoutObservationKind kind) =>
        kind is
            LayoutObservationKind.Text or
            LayoutObservationKind.Heading or
            LayoutObservationKind.Caption or
            LayoutObservationKind.Table;
}
