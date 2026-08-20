using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;

namespace DocumentProcessing.Shared;


/// <summary>
/// Owns the current Host-lifetime shared processing capabilities.
/// </summary>
/// <remarks>
/// Concrete PP-StructureV3 and PaddleOCR providers are selected by the Host
/// composition root. The configured Engine consumes them through their neutral
/// Core contracts.
///
/// This type owns the service <see cref="HttpClient"/> instances and therefore
/// owns their deterministic disposal.
/// </remarks>
internal sealed class SharedProcessingCapabilities
    : IDisposable
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity
        PpStructureV3Identity =
            new(
                "pp-structurev3",
                "pp-structurev3-3.7.0-paddle3.2.2-cpu-v1");

    private readonly HttpClient _layoutHttpClient;
    private readonly HttpClient _ocrHttpClient;

    private bool _disposed;

    #endregion

    #region Properties

    public IPageLayoutAnalyzer LayoutAnalyzer { get; }

    public IRegionTextRecognizer TextRecognizer { get; }

    public ProcessingComponentIdentity LayoutAnalysisIdentity =>
        PpStructureV3Identity;

    #endregion

    #region ctor

    public SharedProcessingCapabilities(
        PpStructureV3Options ppStructureV3,
        PaddleOcrOptions paddleOcr)
    {
        ArgumentNullException.ThrowIfNull(
            ppStructureV3);

        ArgumentNullException.ThrowIfNull(
            paddleOcr);

        _layoutHttpClient =
            CreateServiceHttpClient();

        _ocrHttpClient =
            CreateServiceHttpClient();

        try
        {
            LayoutAnalyzer =
                new PpStructureV3PageLayoutAnalyzer(
                    new PpStructureV3ServingClient(
                        _layoutHttpClient,
                        ppStructureV3.Endpoint,
                        ppStructureV3.RequestTimeout));

            TextRecognizer =
                new PaddleOcrRegionTextRecognizer(
                    new PaddleOcrServingClient(
                        _ocrHttpClient,
                        paddleOcr.Endpoint,
                        paddleOcr.ProfileId,
                        paddleOcr.RequestTimeout));
        }
        catch
        {
            _layoutHttpClient.Dispose();
            _ocrHttpClient.Dispose();

            throw;
        }
    }

    #endregion

    #region Methods Lifecycle

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _layoutHttpClient.Dispose();
        _ocrHttpClient.Dispose();
    }

    private static HttpClient CreateServiceHttpClient() =>
        new()
        {
            Timeout =
                Timeout.InfiniteTimeSpan
        };

    #endregion
}
