using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Selects a preview-capable format and coordinates lightweight page inspection.
/// </summary>
public sealed class PhysicalPagePreviewEngine
{
    #region Variables and Constants

    private readonly IReadOnlyList<IPhysicalPagePreviewDocumentFormat> _formats;

    #endregion

    #region ctor

    /// <summary>Creates a physical-page preview coordinator.</summary>
    public PhysicalPagePreviewEngine(IEnumerable<IDocumentFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);
        _formats = formats.OfType<IPhysicalPagePreviewDocumentFormat>().ToArray();
    }

    #endregion

    #region Methods

    /// <summary>Inspects a source without running full document processing.</summary>
    public async ValueTask<PhysicalPagePreviewInspection> InspectAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var format in _formats)
        {
            Reset(source);
            var pageCount =
                await format.TryGetPhysicalPageCountAsync(source, cancellationToken).ConfigureAwait(false);

            if (pageCount.HasValue)
            {
                Reset(source);
                return new PhysicalPagePreviewInspection(format.Format, pageCount.Value);
            }
        }

        Reset(source);
        throw new NotSupportedException("The document does not support physical-page previews.");
    }

    /// <summary>Renders one physical page through the recognized format.</summary>
    public async ValueTask RenderAsync(
        DocumentSource source,
        int physicalPageNumber,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var inspection = await InspectAsync(source, cancellationToken).ConfigureAwait(false);

        if (physicalPageNumber <= 0 || physicalPageNumber > inspection.PhysicalPageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalPageNumber));
        }

        var format = _formats.Single(candidate => candidate.Format == inspection.Format);
        Reset(source);
        await format.RenderPhysicalPagePreviewAsync(
                source,
                physicalPageNumber,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
        Reset(source);
    }

    private static void Reset(DocumentSource source)
    {
        if (source.Content.CanSeek)
        {
            source.Content.Position = 0;
        }
    }

    #endregion
}
