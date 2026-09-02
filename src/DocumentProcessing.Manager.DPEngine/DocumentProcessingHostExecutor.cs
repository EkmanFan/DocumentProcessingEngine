using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.DPEngine;

/// <summary>
/// Manager execution adapter backed by the consumer-facing DPEngine Host.
/// </summary>
public sealed class DocumentProcessingHostExecutor
    : IDocumentProcessingExecutor
{
    #region Variables and Constants

    private readonly DocumentProcessingHost
        _host;

    private readonly IDocumentSubmissionReader
        _submissionReader;

    private readonly ISourceArtifactReader
        _sourceArtifactReader;

    private readonly IProcessingResultArtifactWriter
        _resultArtifactWriter;

    private readonly IProcessingResultArtifactReader
        _resultArtifactReader;

    private readonly IProcessingResultRegistryWriter
        _resultRegistryWriter;

    private readonly IProcessingResultRegistryReader
        _resultRegistryReader;

    private readonly IDocumentProcessingResultEncoder
        _resultEncoder;

    private readonly IManagerSettingsStore
        _settingsStore;

    private readonly IProcessingVisualAssetStore
        _visualAssetStore;

    private readonly IProcessingProgressReporter
        _progressReporter;

    private readonly TimeProvider
        _timeProvider;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the DPEngine execution adapter. The caller retains Host ownership.
    /// </summary>
    public DocumentProcessingHostExecutor(
        DocumentProcessingHost host,
        IDocumentSubmissionReader submissionReader,
        ISourceArtifactReader sourceArtifactReader,
        IProcessingResultArtifactWriter resultArtifactWriter,
        IProcessingResultArtifactReader resultArtifactReader,
        IProcessingResultRegistryWriter resultRegistryWriter,
        IProcessingResultRegistryReader resultRegistryReader,
        IDocumentProcessingResultEncoder resultEncoder,
        IManagerSettingsStore settingsStore,
        IProcessingVisualAssetStore visualAssetStore,
        IProcessingProgressReporter progressReporter,
        TimeProvider? timeProvider = null)
    {
        _host =
            host ??
            throw new ArgumentNullException(
                nameof(host));

        _submissionReader =
            submissionReader ??
            throw new ArgumentNullException(
                nameof(submissionReader));

        _sourceArtifactReader =
            sourceArtifactReader ??
            throw new ArgumentNullException(
                nameof(sourceArtifactReader));

        _resultArtifactWriter =
            resultArtifactWriter ??
            throw new ArgumentNullException(
                nameof(resultArtifactWriter));

        _resultArtifactReader =
            resultArtifactReader ??
            throw new ArgumentNullException(
                nameof(resultArtifactReader));

        _resultRegistryWriter =
            resultRegistryWriter ??
            throw new ArgumentNullException(
                nameof(resultRegistryWriter));

        _resultRegistryReader =
            resultRegistryReader ??
            throw new ArgumentNullException(
                nameof(resultRegistryReader));

        _resultEncoder =
            resultEncoder ??
            throw new ArgumentNullException(
                nameof(resultEncoder));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _visualAssetStore =
            visualAssetStore ??
            throw new ArgumentNullException(
                nameof(visualAssetStore));

        _progressReporter =
            progressReporter ??
            throw new ArgumentNullException(
                nameof(progressReporter));

        _timeProvider =
            timeProvider ??
            TimeProvider.System;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask<ProcessingExecutionOutcome> ExecuteAsync(
        ProcessingWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            workItem);

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.LoadingSource,
            completionPercentage:
                1);

        var existing =
            await _resultRegistryReader
                .GetByUnitAsync(
                    workItem.UnitId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.SubmissionId !=
                workItem.SubmissionId)
            {
                throw new InvalidOperationException(
                    $"Registered result '{existing.ResultReference}' belongs to a different submission.");
            }

            if (!await _resultArtifactReader
                    .VerifyAsync(
                        existing.Artifact,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new ProcessingResultIntegrityException(
                    existing.Artifact.Digest,
                    $"Registered result '{existing.ResultReference}' has missing or corrupted bytes.");
            }

            ReportProgress(
                workItem.UnitId,
                ProcessingProgressStage.PublishingResult,
                completionPercentage:
                    100);

            return new ProcessingExecutionOutcome.Success(
                existing.ResultReference);
        }

        var submission =
            await _submissionReader
                .GetAsync(
                    workItem.SubmissionId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                $"Processing unit '{workItem.UnitId}' references a missing submission.");

        if (submission.SubmissionId !=
            workItem.SubmissionId)
        {
            throw new InvalidOperationException(
                $"Submission reader returned the wrong manifest for processing unit '{workItem.UnitId}'.");
        }

        await using var sourceContent =
            await _sourceArtifactReader
                .OpenReadAsync(
                    submission.SourceArtifact,
                    cancellationToken)
                .ConfigureAwait(false);

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.LoadingSource,
            completionPercentage:
                4);

        var settings =
            await _settingsStore
                .GetAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var visualSession =
            settings.VisualDestinationRoot is null
                ? null
                : await _visualAssetStore
                    .BeginWriteAsync(
                        settings.VisualDestinationRoot,
                        workItem.UnitId,
                        submission.OriginalFileName,
                        cancellationToken)
                    .ConfigureAwait(false);

        UserVisualAssetWriter visualWriter =
            visualSession is null
                ? MissingVisualDestinationAsync
                : (_, visual, token) =>
                    visualSession.OpenWriteAsync(
                        ResolveVisualMediaType(
                            visual),
                        token);

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.PreparingSource,
            completionPercentage:
                5);

        var outcome =
            await _host
                .ProcessDocumentAsync(
                    new DocumentSource(
                        sourceContent,
                        submission.OriginalFileName,
                        submission.DeclaredMediaType),
                    new DocumentProcessingRequestOptions(
                        userVisualAssetWriter:
                            visualWriter,
                        physicalPageRange:
                            ToPhysicalPageRange(
                                workItem.Scope),
                        contentUnitRange:
                            ToContentUnitRange(
                                workItem.Scope),
                        progressReporter:
                            progress =>
                                ReportEngineProgress(
                                    workItem.UnitId,
                                    progress)),
                    cancellationToken)
                .ConfigureAwait(false);

        if (!outcome.IsSuccess)
        {
            return new ProcessingExecutionOutcome.Failure(
                "document_processing.functional_failure",
                outcome.ErrorMessage ??
                "Document processing failed without a diagnostic message.");
        }

        var result =
            outcome.Result ??
            throw new InvalidOperationException(
                "Successful document processing did not return a result.");

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.StoringResult,
            completionPercentage:
                88);

        EnsureSourceCustodyMatches(
            submission.SourceArtifact.Digest.Value,
            submission.SourceArtifact.ByteLength,
            result);

        var encoded =
            _resultEncoder.Encode(
                result);

        await using var encodedStream =
            new MemoryStream(
                encoded,
                writable:
                    false);

        var artifact =
            await _resultArtifactWriter
                .StoreAsync(
                    encodedStream,
                    cancellationToken)
                .ConfigureAwait(false);

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.PublishingResult,
            completionPercentage:
                94);

        var publicationDirectory =
            visualSession is null
                ? null
                : await visualSession
                    .CompleteAsync(
                        result.VisualAssets
                            .Select(
                                visual =>
                                    new ProcessingVisualAssetDescriptor(
                                        visual.AssetId,
                                        visual.MediaType,
                                        visual.ContentLength,
                                        new Sha256Digest(
                                            visual.ContentSha256)))
                            .ToArray(),
                        encoded,
                        artifact,
                        cancellationToken)
                    .ConfigureAwait(false);

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.PublishingResult,
            completionPercentage:
                98);

        var registration =
            await _resultRegistryWriter
                .RegisterAsync(
                    new ProcessingResultRecord(
                        $"manager-result:{Guid.NewGuid():N}",
                        workItem.UnitId,
                        workItem.SubmissionId,
                        artifact,
                        _resultEncoder.MediaType,
                        _resultEncoder.SchemaVersion,
                        _timeProvider.GetUtcNow(),
                        publicationDirectory),
                    cancellationToken)
                .ConfigureAwait(false);

        ReportProgress(
            workItem.UnitId,
            ProcessingProgressStage.PublishingResult,
            completionPercentage:
                100);

        return new ProcessingExecutionOutcome.Success(
            registration.Result.ResultReference);
    }

    private static void EnsureSourceCustodyMatches(
        string expectedDigest,
        long expectedByteLength,
        DocumentProcessingResult result)
    {
        if (!string.Equals(
                expectedDigest,
                result.Source.Sha256,
                StringComparison.Ordinal) ||
            expectedByteLength !=
            result.Source.ByteLength)
        {
            throw new InvalidOperationException(
                "DPEngine result source custody does not match the submitted source artifact.");
        }
    }

    private void ReportEngineProgress(
        ProcessingUnitId unitId,
        DocumentProcessingProgress progress)
    {
        ArgumentNullException.ThrowIfNull(
            progress);

        ReportProgress(
            unitId,
            progress.Stage switch
            {
                DocumentProcessingProgressStage.PreparingSource =>
                    ProcessingProgressStage.PreparingSource,
                DocumentProcessingProgressStage.InspectingFormat =>
                    ProcessingProgressStage.InspectingFormat,
                DocumentProcessingProgressStage.Planning =>
                    ProcessingProgressStage.Planning,
                DocumentProcessingProgressStage.AnalyzingContent =>
                    ProcessingProgressStage.AnalyzingContent,
                DocumentProcessingProgressStage.ProcessingContent =>
                    ProcessingProgressStage.ProcessingContent,
                DocumentProcessingProgressStage.AssemblingResult =>
                    ProcessingProgressStage.AssemblingResult,
                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(progress),
                        progress.Stage,
                        "Unknown DPEngine processing-progress stage.")
            },
            completionPercentage:
                5 +
                progress.CompletionPercentage *
                80 /
                100,
            progress.CompletedUnitCount,
            progress.TotalUnitCount);
    }

    private void ReportProgress(
        ProcessingUnitId unitId,
        ProcessingProgressStage stage,
        int completionPercentage,
        int? completedUnitCount = null,
        int? totalUnitCount = null)
    {
        _progressReporter.Report(
            unitId,
            new ProcessingProgressSnapshot(
                stage,
                completionPercentage,
                completedUnitCount,
                totalUnitCount,
                _timeProvider.GetUtcNow()));
    }

    private static PhysicalPageRange? ToPhysicalPageRange(
        ProcessingUnitScope scope) =>
        scope switch
        {
            ProcessingUnitScope.WholeDocument =>
                null,
            ProcessingUnitScope.PageRange range =>
                new PhysicalPageRange(
                    range.StartPhysicalPageNumber,
                    range.EndPhysicalPageNumber),
            ProcessingUnitScope.ContentUnitRange =>
                null,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(scope),
                    scope,
                    "Unknown processing-unit scope.")
        };

    internal static ContentUnitRange? ToContentUnitRange(
        ProcessingUnitScope scope) =>
        scope switch
        {
            ProcessingUnitScope.WholeDocument =>
                null,
            ProcessingUnitScope.PageRange =>
                null,
            ProcessingUnitScope.ContentUnitRange range =>
                new ContentUnitRange(
                    range.StartContentUnitIndex,
                    range.StartContentUnitId,
                    range.EndContentUnitIndex,
                    range.EndContentUnitId),
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(scope),
                    scope,
                    "Unknown processing-unit scope.")
        };

    private static ValueTask<Stream> MissingVisualDestinationAsync(
        DocumentSource source,
        UserVisualAssetWriteRequest visual,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromException<Stream>(
            new InvalidOperationException(
                "The Manager visual destination is not configured. " +
                "Choose an existing directory in Manager Settings before processing this document."));
    }

    private static string ResolveVisualMediaType(
        UserVisualAssetWriteRequest visual) =>
        visual switch
        {
            UserLayoutVisualAssetWriteRequest =>
                "image/png",
            UserSourceVisualAssetWriteRequest sourceVisual =>
                sourceVisual.MediaType,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(visual),
                    visual.GetType().FullName,
                    "Unknown visual destination request.")
        };

    #endregion
}
