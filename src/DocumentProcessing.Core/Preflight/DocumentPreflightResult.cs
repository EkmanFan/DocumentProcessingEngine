using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Preflight;

public sealed record DocumentPreflightResult(
    DocumentFormatId Format,
    int PageCount,
    int PagesWithNativeText,
    int PagesWithoutNativeText,
    double TextLayerCoveragePercent,
    IReadOnlyList<int> TextlessPageNumbers,
    IReadOnlyList<int> TextlessDominantRasterPageNumbers,
    DocumentPreflightClassification Classification);
