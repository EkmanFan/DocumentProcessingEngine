using DocumentProcessing.Composition;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;

namespace DocumentProcessing.Formats;

/// <summary>
/// Host-lifetime registry and resolver for document-format processors.
/// </summary>
/// <remarks>
/// V1 deliberately uses explicit hard-coded registration. No assembly scanning,
/// reflection-based discovery, or hot loading is performed.
///
/// Shared processing infrastructure is composed and owned outside this resolver.
/// The resolver owns only processor registration and format selection.
/// </remarks>
internal sealed class DocumentFormatProcessorResolver
{
    #region Variables and Constants

    private readonly PdfDocumentProcessingComposition
        _pdfComposition;

    private readonly IReadOnlyDictionary<DocumentFormatId, IDocumentFormatProcessor>
        _formatProcessors;

    #endregion

    #region ctor

    public DocumentFormatProcessorResolver(
        DocumentProcessingHostOptions options,
        SharedProcessingCapabilities sharedProcessingCapabilities)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            sharedProcessingCapabilities);

        _pdfComposition =
            PdfDocumentFormatProcessorComposition.Create(
                options.EngineVersion,
                sharedProcessingCapabilities.LayoutAnalyzer,
                sharedProcessingCapabilities.TextRecognizer,
                sharedProcessingCapabilities.LayoutAnalysisIdentity,
                options.OpenPreservedLayoutVisualDestinationAsync);

        var pdfProcessor =
            _pdfComposition.LegacyProcessor;

        _formatProcessors =
            new Dictionary<DocumentFormatId, IDocumentFormatProcessor>
            {
                [pdfProcessor.Format] =
                    pdfProcessor
            };
    }

    #endregion

    #region Methods Resolution

    public async ValueTask<IDocumentFormatProcessor?> ResolveAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var processor in
                 _formatProcessors.Values)
        {
            if (await processor
                    .ValidateAsync(
                        source,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return processor;
            }
        }

        return null;
    }

    #endregion
}
