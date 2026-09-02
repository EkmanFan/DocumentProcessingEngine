using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Selects a native-navigation-capable format and coordinates lightweight
/// structural inspection.
/// </summary>
public sealed class NativeDocumentNavigationEngine
{
    #region Variables and Constants

    private readonly IReadOnlyList<INativeDocumentNavigationFormat> _formats;

    #endregion

    #region ctor

    /// <summary>Creates a native-document-navigation coordinator.</summary>
    public NativeDocumentNavigationEngine(
        IEnumerable<IDocumentFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(
            formats);

        _formats =
            formats
                .OfType<INativeDocumentNavigationFormat>()
                .ToArray();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns native navigation for the first format recognizing the source,
    /// or <see langword="null"/> when no registered capability recognizes it.
    /// </summary>
    public async ValueTask<NativeDocumentNavigationInspection?> TryInspectAsync(
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
                    .TryInspectNativeNavigationAsync(
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
