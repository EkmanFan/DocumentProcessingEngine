using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Selects a structural-heading-capable format and coordinates lightweight
/// deterministic heading inspection.
/// </summary>
public sealed class StructuralHeadingEngine
{
    #region Variables and Constants

    private readonly IReadOnlyList<IStructuralHeadingDocumentFormat> _formats;

    #endregion

    #region ctor

    /// <summary>Creates a structural-heading inspection coordinator.</summary>
    public StructuralHeadingEngine(
        IEnumerable<IDocumentFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(
            formats);

        _formats =
            formats
                .OfType<IStructuralHeadingDocumentFormat>()
                .ToArray();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns heading evidence for the first format recognizing the source,
    /// or <see langword="null"/> when no registered capability recognizes it.
    /// </summary>
    public async ValueTask<StructuralHeadingInspection?> TryInspectAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        foreach (var format in
                 _formats)
        {
            Reset(
                source);

            var inspection =
                await format
                    .TryInspectStructuralHeadingsAsync(
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (inspection is not null)
            {
                Reset(
                    source);

                return inspection;
            }
        }

        Reset(
            source);

        return null;
    }

    private static void Reset(
        DocumentSource source)
    {
        if (source.Content.CanSeek)
        {
            source.Content.Position =
                0;
        }
    }

    #endregion
}
