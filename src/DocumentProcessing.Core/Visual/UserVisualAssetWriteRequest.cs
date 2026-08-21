using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Information supplied to the user's visual-asset writer before the Engine
/// writes one selected visual.
/// </summary>
public abstract record UserVisualAssetWriteRequest
{
    protected UserVisualAssetWriteRequest(
        DocumentFormatId format,
        DocumentSourceLocation location)
    {
        Format =
            format;

        Location =
            location ??
            throw new ArgumentNullException(
                nameof(location));
    }

    public DocumentFormatId Format { get; }

    public DocumentSourceLocation Location { get; }
}

/// <summary>
/// Request for a visual identified by paged layout analysis.
/// </summary>
public sealed record UserLayoutVisualAssetWriteRequest
    : UserVisualAssetWriteRequest
{
    public UserLayoutVisualAssetWriteRequest(
        DocumentFormatId format,
        LayoutObservation layoutObservation)
        : base(
            format,
            new PagedDocumentSourceLocation(
                (layoutObservation ??
                 throw new ArgumentNullException(
                     nameof(layoutObservation)))
                .PhysicalPageNumber,
                layoutObservation.Bounds))
    {
        LayoutObservation =
            layoutObservation;
    }

    public LayoutObservation LayoutObservation { get; }
}

/// <summary>
/// Request for an image embedded as an exact resource in a structured source.
/// </summary>
public sealed record UserSourceVisualAssetWriteRequest
    : UserVisualAssetWriteRequest
{
    public UserSourceVisualAssetWriteRequest(
        DocumentFormatId format,
        StructuredNativeVisual visual,
        DocumentVisualQualification qualification)
        : base(
            format,
            (visual ??
             throw new ArgumentNullException(
                 nameof(visual)))
            .Location)
    {
        VisualId =
            visual.VisualId;

        SourceResourceId =
            visual.SourceResourceId;

        MediaType =
            visual.MediaType;

        IsAuxiliary =
            visual.IsAuxiliary;

        if (!Enum.IsDefined(
                qualification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(qualification));
        }

        Qualification =
            qualification;
    }

    public string VisualId { get; }

    public string SourceResourceId { get; }

    public string MediaType { get; }

    public bool IsAuxiliary { get; }

    public DocumentVisualQualification Qualification { get; }
}
