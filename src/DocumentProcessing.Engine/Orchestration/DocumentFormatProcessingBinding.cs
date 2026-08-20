using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Explicit composition binding between one document-format acquisition boundary
/// and the Engine processor configured for the same format.
/// </summary>
/// <remarks>
/// This is composition data, not a runtime resolver or service locator.
/// Selection remains an Engine responsibility through <see cref="DocumentFormatSelector"/>.
/// </remarks>
public sealed class DocumentFormatProcessingBinding
{
    #region Variables and Constants

    private readonly IDocumentFormat
        _documentFormat;

    private readonly DocumentProcessor
        _processor;

    #endregion

    #region ctor

    public DocumentFormatProcessingBinding(
        IDocumentFormat documentFormat,
        DocumentProcessor processor)
    {
        _documentFormat =
            documentFormat ??
            throw new ArgumentNullException(
                nameof(documentFormat));

        _processor =
            processor ??
            throw new ArgumentNullException(
                nameof(processor));

        if (_documentFormat.Format !=
            _processor.Format)
        {
            throw new ArgumentException(
                $"Document format '{_documentFormat.Format}' cannot be bound to " +
                $"processor composition '{_processor.Format}'.",
                nameof(processor));
        }
    }

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        _documentFormat.Format;

    internal IDocumentFormat DocumentFormat =>
        _documentFormat;

    internal DocumentProcessor Processor =>
        _processor;

    #endregion
}
