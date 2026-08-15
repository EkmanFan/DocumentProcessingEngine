using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Engine.Layout;

/// <summary>
/// Testable orchestration adapter over the selected PP-StructureV3 serving
/// client.
///
/// The abstraction exists because the HTTP/model service is a real external
/// volatility boundary. It is not a generic layout plugin registry.
/// </summary>
public sealed class PpStructureV3PageLayoutAnalyzer
    : IPageLayoutAnalyzer
{
    private readonly PpStructureV3ServingClient _client;

    public PpStructureV3PageLayoutAnalyzer(
        PpStructureV3ServingClient client)
    {
        _client =
            client ??
            throw new ArgumentNullException(
                nameof(client));
    }

    public ValueTask<LayoutAnalysisResult> AnalyzeAsync(
        Stream rasterImage,
        int physicalPageNumber,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default) =>
        _client.AnalyzeAsync(
            rasterImage,
            physicalPageNumber,
            pixelWidth,
            pixelHeight,
            cancellationToken);
}
