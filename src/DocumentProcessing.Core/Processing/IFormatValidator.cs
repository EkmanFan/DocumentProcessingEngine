using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Processing;

/// <summary>
/// Validates whether a source conforms to one specific document format.
/// </summary>
/// <remarks>
/// The owning format processor decides which validator implementation belongs
/// to it. Generic routing code never sees or reasons about validators directly.
/// </remarks>
public interface IFormatValidator
{
    #region Methods Validation

    ValueTask<bool> ValidateAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);

    #endregion
}
