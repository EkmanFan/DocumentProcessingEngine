using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// User-selected options for one document-processing request.
/// </summary>
public sealed record DocumentProcessingRequestOptions
{
    #region Properties

    /// <summary>Gets the default processing-request options.</summary>
    public static DocumentProcessingRequestOptions Default { get; } =
        new();

    /// <summary>
    /// Gets whether visuals unresolved by deterministic format facts may be
    /// sent to the configured visual-analysis capability for qualification.
    /// </summary>
    public bool QualifyUnresolvedVisuals { get; }

    /// <summary>
    /// Gets the request-scoped destination writer for selected visual assets.
    /// It overrides the Host-wide writer when supplied.
    /// </summary>
    public UserVisualAssetWriter? UserVisualAssetWriter { get; }

    #endregion

    #region ctor

    /// <summary>Creates options for one document-processing request.</summary>
    public DocumentProcessingRequestOptions(
        bool qualifyUnresolvedVisuals = false,
        UserVisualAssetWriter? userVisualAssetWriter = null)
    {
        QualifyUnresolvedVisuals =
            qualifyUnresolvedVisuals;

        UserVisualAssetWriter =
            userVisualAssetWriter;
    }

    #endregion
}
