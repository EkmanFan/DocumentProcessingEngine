using System.Globalization;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Evidence-conservative V1 native page assessor.
///
/// The assessor deliberately distinguishes "Suspicious" from "Unverified".
/// A dominant raster backing a native text layer is not proof that the native
/// text is wrong; it only means native extraction alone cannot establish visual
/// fidelity. Such pages therefore require secondary verification.
///
/// This distinction is based on the Phase 21B real-corpus diagnostic in which
/// Ehrman physical pages 380 (historical Conflict control) and 405 (historical
/// Agreement control) had effectively identical dominant-raster evidence and
/// no intrinsic native structural/Unicode discriminator.
/// </summary>
public sealed class DefaultPageProcessingAssessor
    : IPageProcessingAssessor
{
    #region Variables and Constants

    /// <summary>
    /// V1 threshold identifying a page whose visible content is materially
    /// image-backed. It is used as a verification trigger, not as evidence of
    /// native-text corruption.
    /// </summary>
    public const double ImageBackedVerificationAreaRatio =
        0.60;

    #endregion

    #region ctor

    #endregion

    #region Methods

    public PageProcessingAssessment Assess(
        DocumentExtractionPage page)
    {
        ArgumentNullException.ThrowIfNull(
            page);

        if (page.WordCount ==
            0)
        {
            return Create(
                page,
                NativeTextStatus.Missing);
        }

        if (HasHardNativeEvidenceFailure(
                page))
        {
            return Create(
                page,
                NativeTextStatus.Suspicious);
        }

        if (page.RasterImageCount >
                0 &&
            page.LargestRasterImageAreaRatio >=
                ImageBackedVerificationAreaRatio)
        {
            return Create(
                page,
                NativeTextStatus.Unverified);
        }

        return Create(
            page,
            NativeTextStatus.Healthy);
    }

    private static PageProcessingAssessment Create(
        DocumentExtractionPage page,
        NativeTextStatus status) =>
        new(
            page.PhysicalPageNumber,
            status);

    private static bool HasHardNativeEvidenceFailure(
        DocumentExtractionPage page)
    {
        if (page.Blocks.Count ==
            0)
        {
            return true;
        }

        if (page.Words.Count !=
            page.WordCount)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(
                page.SourceText))
        {
            return true;
        }

        foreach (var character in
                 page.SourceText)
        {
            if (character ==
                '\uFFFD')
            {
                return true;
            }

            if (char.IsControl(
                    character) &&
                character is not '\r' and not '\n' and not '\t')
            {
                return true;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(
                    character) ==
                UnicodeCategory.PrivateUse)
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
