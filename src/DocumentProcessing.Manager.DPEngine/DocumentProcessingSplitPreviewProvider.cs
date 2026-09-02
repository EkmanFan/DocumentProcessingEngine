using DocumentProcessing.Core.Documents;
using DocumentProcessing.Manager.Partitioning;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.DPEngine;

/// <summary>Manager split-preview adapter backed by the DPEngine Host.</summary>
public sealed class DocumentProcessingSplitPreviewProvider : IDocumentSplitPreviewProvider
{
    #region Variables and Constants

    /// <summary>Default physical-page threshold above which splitting is suggested.</summary>
    public const int DefaultComplexDocumentPageThreshold = 80;

    private readonly DocumentProcessingHost _host;
    private readonly IProcessingQueueReader _queueReader;
    private readonly IDocumentSubmissionReader _submissionReader;
    private readonly ISourceArtifactReader _sourceReader;
    private readonly int _complexDocumentPageThreshold;
    private readonly IDocumentPartitionStrategy _partitionStrategy;

    #endregion

    #region ctor

    /// <summary>Creates the DPEngine-backed split-preview adapter.</summary>
    public DocumentProcessingSplitPreviewProvider(
        DocumentProcessingHost host,
        IProcessingQueueReader queueReader,
        IDocumentSubmissionReader submissionReader,
        ISourceArtifactReader sourceReader,
        int complexDocumentPageThreshold = DefaultComplexDocumentPageThreshold,
        IDocumentPartitionStrategy? partitionStrategy = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _queueReader = queueReader ?? throw new ArgumentNullException(nameof(queueReader));
        _submissionReader = submissionReader ?? throw new ArgumentNullException(nameof(submissionReader));
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));

        if (complexDocumentPageThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(complexDocumentPageThreshold));
        }

        _complexDocumentPageThreshold = complexDocumentPageThreshold;

        _partitionStrategy =
            partitionStrategy ??
            new NativeNavigationPartitionStrategy();
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask<DocumentSplitPreviewManifest> InspectAsync(
        ProcessingUnitId unitId,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAsync(unitId, cancellationToken).ConfigureAwait(false);

        await using var source =
            await _sourceReader.OpenReadAsync(context.Submission.SourceArtifact, cancellationToken)
                .ConfigureAwait(false);

        var documentSource =
            new DocumentSource(
                source,
                context.Submission.OriginalFileName,
                context.Submission.DeclaredMediaType);

        var inspection =
            await _host.InspectPhysicalPagesAsync(
                    documentSource,
                    cancellationToken)
                .ConfigureAwait(false);

        var navigation =
            await _host
                .TryInspectNativeNavigationAsync(
                    documentSource,
                    cancellationToken)
                .ConfigureAwait(false);

        var suggestedRanges =
            BuildSuggestedRanges(
                inspection.PhysicalPageCount,
                navigation);

        return new DocumentSplitPreviewManifest(
            unitId,
            context.Submission.SubmissionId,
            context.Submission.OriginalFileName,
            inspection.PhysicalPageCount,
            inspection.PhysicalPageCount >= _complexDocumentPageThreshold,
            suggestedRanges);
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> RenderPageAsync(
        ProcessingUnitId unitId,
        int physicalPageNumber,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAsync(unitId, cancellationToken).ConfigureAwait(false);

        await using var source =
            await _sourceReader.OpenReadAsync(context.Submission.SourceArtifact, cancellationToken)
                .ConfigureAwait(false);

        await using var destination = new MemoryStream();

        await _host.RenderPhysicalPagePreviewAsync(
                new DocumentSource(
                    source,
                    context.Submission.OriginalFileName,
                    context.Submission.DeclaredMediaType),
                physicalPageNumber,
                destination,
                cancellationToken)
            .ConfigureAwait(false);

        return destination.ToArray();
    }

    private async ValueTask<PreviewContext> ResolveAsync(
        ProcessingUnitId unitId,
        CancellationToken cancellationToken)
    {
        var snapshot = await _queueReader.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var unit = snapshot.Items.SingleOrDefault(item => item.WorkItem.UnitId == unitId) ??
                   throw new InvalidOperationException("The processing unit no longer exists.");

        if (unit.Status != ProcessingUnitStatus.Pending ||
            unit.WorkItem.Scope is not ProcessingUnitScope.WholeDocument)
        {
            throw new InvalidOperationException(
                "Only a pending whole-document unit can be prepared for splitting.");
        }

        var submission =
            await _submissionReader.GetAsync(unit.WorkItem.SubmissionId, cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException("The processing unit references a missing submission.");

        return new PreviewContext(unit, submission);
    }

    private IReadOnlyList<DocumentSplitSuggestedRange> BuildSuggestedRanges(
        int physicalPageCount,
        NativeDocumentNavigationInspection? navigation)
    {
        if (navigation?.Axis is not
            NativeDocumentNavigationAxis.PhysicalPages navigationAxis ||
            navigationAxis.PhysicalPageCount !=
            physicalPageCount)
        {
            return [];
        }

        var axis =
            new DocumentPartitionAxis.PhysicalPages(
                physicalPageCount);

        var boundaries =
            navigation.Entries
                .Select(
                    entry =>
                        new DocumentPartitionBoundary(
                            new DocumentPartitionPosition.PhysicalPage(
                                ((NativeDocumentNavigationPosition.PhysicalPage)
                                    entry.Position)
                                .PhysicalPageNumber),
                            entry.Title,
                            entry.HierarchyLevel,
                            entry.SourceOrder,
                            DocumentPartitionEvidenceOrigin.NativeNavigation))
                .ToArray();

        var proposal =
            _partitionStrategy.TryPropose(
                new DocumentPartitionEvidence(
                    axis,
                    boundaries));

        if (proposal is null)
        {
            return [];
        }

        return proposal.Segments
            .Select(
                segment =>
                    new DocumentSplitSuggestedRange(
                        ((DocumentPartitionPosition.PhysicalPage)
                            segment.Extent.Start)
                        .PhysicalPageNumber,
                        ((DocumentPartitionPosition.PhysicalPage)
                            segment.Extent.End)
                        .PhysicalPageNumber,
                        segment.SuggestedTitle))
            .ToArray();
    }

    private sealed record PreviewContext(
        ProcessingQueueItemSnapshot Unit,
        Submissions.DocumentSubmission Submission);

    #endregion
}
