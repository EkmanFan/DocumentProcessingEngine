using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Manager.DPEngine;

/// <summary>
/// Strategy for encoding one canonical Engine result as durable consumer bytes.
/// </summary>
public interface IDocumentProcessingResultEncoder
{
    #region Properties

    /// <summary>
    /// Gets the media type of encoded result bytes.
    /// </summary>
    string MediaType { get; }

    /// <summary>
    /// Gets the schema identifier of encoded result bytes.
    /// </summary>
    string SchemaVersion { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Encodes one supported canonical result without altering its content.
    /// </summary>
    byte[] Encode(
        DocumentProcessingResult result);

    #endregion
}
