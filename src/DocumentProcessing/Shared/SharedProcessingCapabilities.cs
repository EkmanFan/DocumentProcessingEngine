using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;
using DocumentProcessing.ProviderLifecycle;
using Microsoft.Extensions.Logging;

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

    private readonly IProcessingProviderRuntime
        _providerRuntime;

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
        PaddleOcrOptions paddleOcr,
        ProcessingProviderLifecycleOptions providerLifecycle,
        ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(
            ppStructureV3);

        ArgumentNullException.ThrowIfNull(
            paddleOcr);

        ArgumentNullException.ThrowIfNull(
            providerLifecycle);

        _providerRuntime =
            ProcessingProviderRuntimeFactory.Create(
                providerLifecycle,
                ppStructureV3,
                paddleOcr,
                loggerFactory);

        _layoutHttpClient =
            CreateServiceHttpClient();

        _ocrHttpClient =
            CreateServiceHttpClient();

        try
        {
            LayoutAnalyzer =
                new PpStructureV3LayoutAdapter(
                    new PpStructureV3ServingClient(
                        _layoutHttpClient,
                        ppStructureV3.Endpoint,
                        ppStructureV3.RequestTimeout,
                        ensureAvailable:
                            cancellationToken =>
                                _providerRuntime.EnsureAvailableAsync(
                                    ProcessingProviderCapability.Layout,
                                    cancellationToken),
                        reportUnavailable:
                            () =>
                                _providerRuntime.ReportUnavailable(
                                    ProcessingProviderCapability.Layout)));

            TextRecognizer =
                new PaddleOcrAdapter(
                    new PaddleOcrServingClient(
                        _ocrHttpClient,
                        paddleOcr.Endpoint,
                        paddleOcr.RequestTimeout,
                        ensureAvailable:
                            cancellationToken =>
                                _providerRuntime.EnsureAvailableAsync(
                                    ProcessingProviderCapability.Ocr,
                                    cancellationToken),
                        reportUnavailable:
                            () =>
                                _providerRuntime.ReportUnavailable(
                                    ProcessingProviderCapability.Ocr)),
                    paddleOcr.ProfileId);
        }
        catch
        {
            _layoutHttpClient.Dispose();
            _ocrHttpClient.Dispose();
            _providerRuntime.Dispose();

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
        _providerRuntime.Dispose();
    }

    private static HttpClient CreateServiceHttpClient() =>
        new()
        {
            Timeout =
                Timeout.InfiniteTimeSpan
        };

    #endregion
}
