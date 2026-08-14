using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Engine.Layout;

/// <summary>
/// Conservative V1 policy translating neutral layout roles into processing
/// actions.
///
/// This policy is deterministic application code. A layout/OCR model does not
/// decide whether a detected figure may be OCRized.
/// </summary>
public static class LayoutTreatmentPolicy
{
    public static LayoutTreatment Decide(
        LayoutObservationKind kind) =>
        kind switch
        {
            LayoutObservationKind.Text or
            LayoutObservationKind.Heading or
            LayoutObservationKind.Caption or
            LayoutObservationKind.Table =>
                LayoutTreatment.RecognizeText,

            LayoutObservationKind.Figure =>
                LayoutTreatment.PreserveVisualWithoutOcr,

            LayoutObservationKind.Unknown =>
                LayoutTreatment.Deferred,

            _ =>
                LayoutTreatment.Deferred
        };
}
