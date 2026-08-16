using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Existing deterministic native-page execution mechanism shared by the legacy
/// NativeOnly route and the H.4D.1 candidate NativeText mode.
///
/// It performs no rasterization, layout analysis, OCR, reconciliation, or visual
/// work.
/// </summary>
internal static class NativeHybridPageAssembler
{
    public static HybridDocumentPage Assemble(
        DocumentExtractionPage page)
    {
        ArgumentNullException.ThrowIfNull(
            page);

        if (page.Blocks.Count ==
            0)
        {
            throw new InvalidDataException(
                $"Native-only page {page.PhysicalPageNumber} contains no native text blocks.");
        }

        var elements =
            page.Blocks
                .Select(
                    block =>
                        HybridDocumentElementFactory
                            .FromNative(
                                page.PhysicalPageNumber,
                                block))
                .ToArray();

        return HybridDocumentAssembler
            .AssemblePage(
                page,
                elements);
    }
}
