using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Epub.Validation;

/// <summary>
/// Converts internal conformance states into the two consumer-safe acquisition
/// failures allowed by EPUB-1.
/// </summary>
internal static class EpubConformanceOutcomeMapper
{
    #region Variables and Constants

    internal const string NonConformantMessage =
        "Le fichier EPUB n’est pas conforme.";

    internal const string ValidationUnavailableMessage =
        "La validation EPUB est temporairement indisponible.";

    #endregion

    #region Methods Mapping

    public static NativeEvidenceExtractionResult? MapFailure(
        EpubCheckConformanceStatus status) =>
        status switch
        {
            EpubCheckConformanceStatus.Conformant =>
                null,

            EpubCheckConformanceStatus.NonConformant =>
                new NativeEvidenceExtractionResult.Invalid(
                    NonConformantMessage,
                    isConsumerSafeReason:
                        true),

            EpubCheckConformanceStatus.Unavailable or
                EpubCheckConformanceStatus.Failed or
                EpubCheckConformanceStatus.TimedOut =>
                new NativeEvidenceExtractionResult.Unavailable(
                    ValidationUnavailableMessage),

            _ =>
                new NativeEvidenceExtractionResult.Unavailable(
                    ValidationUnavailableMessage)
        };

    #endregion
}
