using DocumentProcessing.Core.Documents;
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

    /// <summary>Gets the optional inclusive range of original physical pages to process.</summary>
    public PhysicalPageRange? PhysicalPageRange { get; }

    /// <summary>Gets the optional inclusive range of stable native content units to process.</summary>
    public ContentUnitRange? ContentUnitRange { get; }

    #endregion

    #region ctor

    /// <summary>Creates options for one document-processing request.</summary>
    public DocumentProcessingRequestOptions(
        bool qualifyUnresolvedVisuals = false,
        UserVisualAssetWriter? userVisualAssetWriter = null,
        PhysicalPageRange? physicalPageRange = null,
        ContentUnitRange? contentUnitRange = null)
    {
        if (physicalPageRange is not null &&
            contentUnitRange is not null)
        {
            throw new ArgumentException(
                "A processing request cannot combine physical-page and content-unit ranges.",
                nameof(contentUnitRange));
        }

        QualifyUnresolvedVisuals =
            qualifyUnresolvedVisuals;

        UserVisualAssetWriter =
            userVisualAssetWriter;

        PhysicalPageRange =
            physicalPageRange;

        ContentUnitRange =
            contentUnitRange;
    }

    #endregion
}
