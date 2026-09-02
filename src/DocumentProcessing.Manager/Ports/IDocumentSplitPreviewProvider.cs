using DocumentProcessing.Manager.Partitioning;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Provides lightweight native document structure and capability-specific
/// physical-page previews for split approval.
/// </summary>
public interface IDocumentSplitPreviewProvider
{
    /// <summary>Inspects a pending whole-document unit.</summary>
    ValueTask<DocumentSplitPreviewManifest> InspectAsync(
        ProcessingUnitId unitId,
        CancellationToken cancellationToken = default);

    /// <summary>Renders one physical page as PNG bytes.</summary>
    ValueTask<byte[]> RenderPageAsync(
        ProcessingUnitId unitId,
        int physicalPageNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes one preview-capable pending document.</summary>
public sealed record DocumentSplitPreviewManifest
{
    #region Properties

    /// <summary>Gets the pending processing-unit identity.</summary>
    public ProcessingUnitId UnitId { get; }

    /// <summary>Gets the source submission identity.</summary>
    public DocumentSubmissionId SubmissionId { get; }

    /// <summary>Gets the original file name.</summary>
    public string OriginalFileName { get; }

    /// <summary>Gets the complete native coordinate axis.</summary>
    public DocumentPartitionAxis Axis { get; }

    /// <summary>Gets whether splitting is recommended.</summary>
    public bool SplitSuggested { get; }

    /// <summary>Gets the optional non-destructive evidence-backed proposal.</summary>
    public DocumentPartitionProposal? SuggestedProposal { get; }

    /// <summary>Gets optional user-visible labels for ordered content units.</summary>
    public IReadOnlyList<DocumentSplitContentUnitLabel> ContentUnitLabels { get; }

    #endregion

    #region ctor

    /// <summary>Creates one validated document split preview manifest.</summary>
    public DocumentSplitPreviewManifest(
        ProcessingUnitId unitId,
        DocumentSubmissionId submissionId,
        string originalFileName,
        DocumentPartitionAxis axis,
        bool splitSuggested,
        DocumentPartitionProposal? suggestedProposal = null,
        IReadOnlyList<DocumentSplitContentUnitLabel>? contentUnitLabels = null)
    {
        if (string.IsNullOrWhiteSpace(
                originalFileName))
        {
            throw new ArgumentException(
                "Original file name cannot be empty.",
                nameof(originalFileName));
        }

        ArgumentNullException.ThrowIfNull(
            axis);

        var labels =
            contentUnitLabels?.ToArray() ??
            [];

        if (axis is DocumentPartitionAxis.PhysicalPages &&
            labels.Length >
            0)
        {
            throw new ArgumentException(
                "Physical-page previews cannot contain content-unit labels.",
                nameof(contentUnitLabels));
        }

        if (axis is DocumentPartitionAxis.ContentUnits contentAxis &&
            labels.Any(
                label =>
                    label is null ||
                    label.ContentUnitIndex >=
                    contentAxis.ContentUnitIds.Count ||
                    !string.Equals(
                        contentAxis.ContentUnitIds[label.ContentUnitIndex],
                        label.ContentUnitId,
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every content-unit label must belong to the preview axis.",
                nameof(contentUnitLabels));
        }

        if (suggestedProposal is not null &&
            !Equals(
                suggestedProposal.Axis,
                axis))
        {
            throw new ArgumentException(
                "The suggested proposal must use the preview axis.",
                nameof(suggestedProposal));
        }

        UnitId =
            unitId;

        SubmissionId =
            submissionId;

        OriginalFileName =
            originalFileName.Trim();

        Axis =
            axis;

        SplitSuggested =
            splitSuggested;

        SuggestedProposal =
            suggestedProposal;

        ContentUnitLabels =
            labels;
    }

    #endregion
}

/// <summary>Provides an optional publisher label for one ordered content unit.</summary>
public sealed record DocumentSplitContentUnitLabel
{
    #region Properties

    /// <summary>Gets the zero-based content-unit index.</summary>
    public int ContentUnitIndex { get; }

    /// <summary>Gets the stable content-unit identifier.</summary>
    public string ContentUnitId { get; }

    /// <summary>Gets the optional publisher-supplied title.</summary>
    public string? SuggestedTitle { get; }

    #endregion

    #region ctor

    /// <summary>Creates one content-unit label.</summary>
    public DocumentSplitContentUnitLabel(
        int contentUnitIndex,
        string contentUnitId,
        string? suggestedTitle)
    {
        if (contentUnitIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentUnitIndex));
        }

        if (string.IsNullOrWhiteSpace(
                contentUnitId))
        {
            throw new ArgumentException(
                "Content-unit identifier cannot be empty.",
                nameof(contentUnitId));
        }

        ContentUnitIndex =
            contentUnitIndex;

        ContentUnitId =
            contentUnitId.Trim();

        SuggestedTitle =
            string.IsNullOrWhiteSpace(
                suggestedTitle)
                ? null
                : suggestedTitle.Trim();
    }

    #endregion
}
