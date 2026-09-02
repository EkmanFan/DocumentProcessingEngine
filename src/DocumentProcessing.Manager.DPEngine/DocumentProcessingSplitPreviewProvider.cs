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
    private readonly IDocumentPartitionStrategy _nativeNavigationStrategy;
    private readonly IDocumentPartitionStrategy _structuralHeadingStrategy;

    #endregion

    #region ctor

    /// <summary>Creates the DPEngine-backed split-preview adapter.</summary>
    public DocumentProcessingSplitPreviewProvider(
        DocumentProcessingHost host,
        IProcessingQueueReader queueReader,
        IDocumentSubmissionReader submissionReader,
        ISourceArtifactReader sourceReader,
        int complexDocumentPageThreshold = DefaultComplexDocumentPageThreshold,
        IDocumentPartitionStrategy? nativeNavigationStrategy = null,
        IDocumentPartitionStrategy? structuralHeadingStrategy = null)
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

        _nativeNavigationStrategy =
            nativeNavigationStrategy ??
            new NativeNavigationPartitionStrategy();

        _structuralHeadingStrategy =
            structuralHeadingStrategy ??
            new StructuralHeadingPartitionStrategy();
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

        var navigation =
            await _host
                .TryInspectNativeNavigationAsync(
                    documentSource,
                    cancellationToken)
                .ConfigureAwait(false);

        if (navigation?.Axis is
            DocumentStructureAxis.ContentUnits contentAxis)
        {
            return await BuildContentUnitPreviewAsync(
                unitId,
                context,
                documentSource,
                contentAxis,
                navigation,
                cancellationToken)
                .ConfigureAwait(false);
        }

        StructuralHeadingInspection? structuralHeadings =
            null;

        if (navigation is null)
        {
            structuralHeadings =
                await _host
                    .TryInspectStructuralHeadingsAsync(
                        documentSource,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (structuralHeadings?.Axis is
                DocumentStructureAxis.ContentUnits structuralContentAxis)
            {
                return BuildContentUnitPreview(
                    unitId,
                    context,
                    structuralContentAxis,
                    navigation:
                        null,
                    structuralHeadings);
            }
        }

        var inspection =
            await _host.InspectPhysicalPagesAsync(
                    documentSource,
                    cancellationToken)
                .ConfigureAwait(false);

        var physicalAxis =
            new DocumentPartitionAxis.PhysicalPages(
                inspection.PhysicalPageCount);

        var physicalProposal =
            navigation?.Axis is DocumentStructureAxis.PhysicalPages nativePages &&
            nativePages.PhysicalPageCount == inspection.PhysicalPageCount
                ? BuildNativeNavigationProposal(
                    physicalAxis,
                    navigation)
                : null;

        if (physicalProposal is null)
        {
            structuralHeadings ??=
                await _host
                    .TryInspectStructuralHeadingsAsync(
                        documentSource,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (structuralHeadings?.Axis is
                    DocumentStructureAxis.PhysicalPages structuralPages &&
                structuralPages.PhysicalPageCount ==
                inspection.PhysicalPageCount)
            {
                physicalProposal =
                    BuildStructuralHeadingProposal(
                        physicalAxis,
                        structuralHeadings);
            }
        }

        return new DocumentSplitPreviewManifest(
            unitId,
            context.Submission.SubmissionId,
            context.Submission.OriginalFileName,
            physicalAxis,
            inspection.PhysicalPageCount >= _complexDocumentPageThreshold ||
            physicalProposal is not null,
            physicalProposal);
    }

    private async ValueTask<DocumentSplitPreviewManifest>
        BuildContentUnitPreviewAsync(
            ProcessingUnitId unitId,
            PreviewContext context,
            DocumentSource source,
            DocumentStructureAxis.ContentUnits contentAxis,
            NativeDocumentNavigationInspection navigation,
            CancellationToken cancellationToken)
    {
        var axis =
            new DocumentPartitionAxis.ContentUnits(
                contentAxis.ContentUnitIds);

        var nativeProposal =
            BuildNativeNavigationProposal(
                axis,
                navigation);

        if (nativeProposal is not null)
        {
            return BuildContentUnitPreview(
                unitId,
                context,
                contentAxis,
                navigation,
                structuralHeadings:
                    null);
        }

        var structuralHeadings =
            await _host
                .TryInspectStructuralHeadingsAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        return BuildContentUnitPreview(
            unitId,
            context,
            contentAxis,
            navigation,
            structuralHeadings);
    }

    private DocumentSplitPreviewManifest BuildContentUnitPreview(
        ProcessingUnitId unitId,
        PreviewContext context,
        DocumentStructureAxis.ContentUnits contentAxis,
        NativeDocumentNavigationInspection? navigation,
        StructuralHeadingInspection? structuralHeadings)
    {
        var axis =
            new DocumentPartitionAxis.ContentUnits(
                contentAxis.ContentUnitIds);

        var nativeProposal =
            navigation is not null
                ? BuildNativeNavigationProposal(
                    axis,
                    navigation)
                : null;

        var structuralProposal =
            nativeProposal is null &&
            structuralHeadings?.Axis is
                DocumentStructureAxis.ContentUnits structuralAxis &&
            ContentUnitAxesMatch(
                structuralAxis,
                contentAxis)
                ? BuildStructuralHeadingProposal(
                    axis,
                    structuralHeadings)
                : null;

        var proposal =
            nativeProposal ??
            structuralProposal;

        var labels =
            structuralProposal is not null
                ? BuildContentUnitLabels(
                    contentAxis,
                    structuralHeadings!)
                : navigation is not null
                    ? BuildContentUnitLabels(
                        contentAxis,
                        navigation)
                    : [];

        return new DocumentSplitPreviewManifest(
            unitId,
            context.Submission.SubmissionId,
            context.Submission.OriginalFileName,
            axis,
            splitSuggested:
                proposal is not null,
            proposal,
            labels);
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

    private DocumentPartitionProposal? BuildNativeNavigationProposal(
        DocumentPartitionAxis axis,
        NativeDocumentNavigationInspection navigation)
    {
        var boundaries =
            navigation.Entries
                .Select(
                    entry =>
                        new DocumentPartitionBoundary(
                            ToPartitionPosition(
                                entry.Position),
                            entry.Title,
                            entry.HierarchyLevel,
                            entry.SourceOrder,
                            DocumentPartitionEvidenceOrigin.NativeNavigation))
                .ToArray();

        return _nativeNavigationStrategy.TryPropose(
            new DocumentPartitionEvidence(
                axis,
                boundaries));
    }

    private DocumentPartitionProposal? BuildStructuralHeadingProposal(
        DocumentPartitionAxis axis,
        StructuralHeadingInspection inspection)
    {
        var boundaries =
            inspection.Entries
                .Select(
                    entry =>
                        new DocumentPartitionBoundary(
                            ToPartitionPosition(
                                entry.Position),
                            entry.Title,
                            entry.HierarchyLevel,
                            entry.SourceOrder,
                            DocumentPartitionEvidenceOrigin.StructuralHeading))
                .ToArray();

        return _structuralHeadingStrategy.TryPropose(
            new DocumentPartitionEvidence(
                axis,
                boundaries));
    }

    private static DocumentPartitionPosition ToPartitionPosition(
        DocumentStructurePosition position) =>
        position switch
        {
            DocumentStructurePosition.PhysicalPage page =>
                new DocumentPartitionPosition.PhysicalPage(
                    page.PhysicalPageNumber),
            DocumentStructurePosition.ContentUnit unit =>
                new DocumentPartitionPosition.ContentUnit(
                    unit.ContentUnitIndex,
                    unit.ContentUnitId),
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    position,
                    "Unknown source-structure position.")
        };

    private static IReadOnlyList<DocumentSplitContentUnitLabel>
        BuildContentUnitLabels(
            DocumentStructureAxis.ContentUnits axis,
            NativeDocumentNavigationInspection navigation) =>
        navigation.Entries
            .Select(
                entry =>
                    (
                        Entry:
                            entry,
                        Position:
                            (DocumentStructurePosition.ContentUnit)
                            entry.Position
                    ))
            .GroupBy(
                item =>
                    item.Position.ContentUnitIndex)
            .Select(
                group =>
                    group
                        .OrderBy(
                            item =>
                                item.Entry.HierarchyLevel)
                        .ThenBy(
                            item =>
                                item.Entry.SourceOrder)
                        .First())
            .OrderBy(
                item =>
                    item.Position.ContentUnitIndex)
            .Select(
                item =>
                    new DocumentSplitContentUnitLabel(
                        item.Position.ContentUnitIndex,
                        axis.ContentUnitIds[item.Position.ContentUnitIndex],
                        item.Entry.Title))
            .ToArray();

    private static IReadOnlyList<DocumentSplitContentUnitLabel>
        BuildContentUnitLabels(
            DocumentStructureAxis.ContentUnits axis,
            StructuralHeadingInspection inspection) =>
        inspection.Entries
            .Select(
                entry =>
                    (
                        Entry:
                            entry,
                        Position:
                            (DocumentStructurePosition.ContentUnit)
                            entry.Position
                    ))
            .GroupBy(
                item =>
                    item.Position.ContentUnitIndex)
            .Select(
                group =>
                    group
                        .OrderBy(
                            item =>
                                item.Entry.HierarchyLevel)
                        .ThenBy(
                            item =>
                                item.Entry.SourceOrder)
                        .First())
            .OrderBy(
                item =>
                    item.Position.ContentUnitIndex)
            .Select(
                item =>
                    new DocumentSplitContentUnitLabel(
                        item.Position.ContentUnitIndex,
                        axis.ContentUnitIds[item.Position.ContentUnitIndex],
                        item.Entry.Title))
            .ToArray();

    private static bool ContentUnitAxesMatch(
        DocumentStructureAxis.ContentUnits first,
        DocumentStructureAxis.ContentUnits second) =>
        first.ContentUnitIds.SequenceEqual(
            second.ContentUnitIds,
            StringComparer.Ordinal);

    private sealed record PreviewContext(
        ProcessingQueueItemSnapshot Unit,
        Submissions.DocumentSubmission Submission);

    #endregion
}
