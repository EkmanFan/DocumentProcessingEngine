using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Engine.DualRun.Dispatch;
using DocumentProcessing.Engine.DualRun.Isolation;

namespace DocumentProcessing.Engine.DualRun.Submission;

/// <summary>
/// Parent-side best-effort Dual Run submission coordinator.
///
/// Selection occurs before dispatcher resolution, authoritative baseline
/// construction, source snapshot creation, or request materialization.
/// Therefore Disabled and unselected Sampled documents perform no Dual Run
/// spool/queue/envelope work.
///
/// Ordinary Dual Run preparation/dispatch failures are converted to
/// non-authoritative results. OutOfMemoryException is deliberately not caught:
/// parent-process resource exhaustion is not made safe by exception handling and
/// remains a capacity concern until the worker process boundary is active.
/// </summary>
public sealed class DocumentDualRunSubmissionCoordinator
{
    #region Variables and Constants

    private readonly DocumentDualRunSourceSnapshotFactory _sourceSnapshotFactory;

    private readonly DocumentDualRunRequestMaterializer _requestMaterializer;

    private readonly Func<IDocumentDualRunJobDispatcher> _dispatcherFactory;

    #endregion

    #region ctor

    public DocumentDualRunSubmissionCoordinator(
        DocumentDualRunSourceSnapshotFactory sourceSnapshotFactory,
        DocumentDualRunRequestMaterializer requestMaterializer,
        Func<IDocumentDualRunJobDispatcher> dispatcherFactory)
    {
        _sourceSnapshotFactory =
            sourceSnapshotFactory ??
            throw new ArgumentNullException(
                nameof(sourceSnapshotFactory));

        _requestMaterializer =
            requestMaterializer ??
            throw new ArgumentNullException(
                nameof(requestMaterializer));

        _dispatcherFactory =
            dispatcherFactory ??
            throw new ArgumentNullException(
                nameof(dispatcherFactory));
    }

    #endregion

    #region Methods Submission

    public async ValueTask<DocumentDualRunSubmissionResult> SubmitAsync(
        DocumentDualRunProfileSnapshot profileSnapshot,
        string sourceDocumentSha256,
        Func<DocumentDualRunSelectedSubmission> createSelectedSubmission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            profileSnapshot);

        ArgumentNullException.ThrowIfNull(
            createSelectedSubmission);

        var selection =
            profileSnapshot.Resolve(
                sourceDocumentSha256);

        if (!selection.IsSelected)
        {
            return DocumentDualRunSubmissionResult
                .NotSelected(
                    selection);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return DocumentDualRunSubmissionResult
                .Cancelled(
                    selection);
        }

        IDocumentDualRunJobDispatcher dispatcher;

        try
        {
            dispatcher =
                _dispatcherFactory() ??
                throw new InvalidOperationException(
                    "Dual Run dispatcher factory returned null.");
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return Failed(
                selection,
                DocumentDualRunSubmissionFailureStage
                    .DispatcherResolution,
                exception);
        }

        DocumentDualRunSelectedSubmission selectedSubmission;

        try
        {
            selectedSubmission =
                createSelectedSubmission() ??
                throw new InvalidOperationException(
                    "Dual Run selected-submission factory returned null.");

            if (!string.Equals(
                    selectedSubmission.SourceDocumentSha256,
                    sourceDocumentSha256.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Selected Dual Run submission source SHA-256 does not match the selection identity.");
            }
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return Failed(
                selection,
                DocumentDualRunSubmissionFailureStage
                    .SelectedSubmissionCreation,
                exception);
        }

        var jobId =
            Guid.NewGuid();

        DocumentDualRunSourceSnapshot? sourceSnapshot =
            null;

        try
        {
            sourceSnapshot =
                await _sourceSnapshotFactory
                    .CreateAsync(
                        jobId,
                        selectedSubmission
                            .Source
                            .Content,
                        selectedSubmission
                            .SourceDocumentSha256,
                        selectedSubmission
                            .SourceByteLength,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return DocumentDualRunSubmissionResult
                .Cancelled(
                    selection);
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return Failed(
                selection,
                DocumentDualRunSubmissionFailureStage
                    .SourceSnapshot,
                exception);
        }

        DocumentDualRunPreparedJob? preparedJob =
            null;

        try
        {
            try
            {
                var request =
                    new DocumentDualRunWorkerRequest(
                        jobId,
                        selection.ExecutionMode!.Value,
                        selectedSubmission.EngineVersion,
                        sourceSnapshot.SourceSnapshotPath,
                        sourceSnapshot.SourceDocumentSha256,
                        sourceSnapshot.SourceByteLength,
                        selectedSubmission.Format,
                        selectedSubmission.AuthoritativePages,
                        selectedSubmission.Source.FileName,
                        selectedSubmission.Source.DeclaredMediaType);

                preparedJob =
                    await _requestMaterializer
                        .CreateAsync(
                            sourceSnapshot,
                            request,
                            cancellationToken)
                        .ConfigureAwait(false);

                sourceSnapshot =
                    null;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return DocumentDualRunSubmissionResult
                    .Cancelled(
                        selection);
            }
            catch (Exception exception)
                when (IsOrdinaryFailure(
                    exception))
            {
                return Failed(
                    selection,
                    DocumentDualRunSubmissionFailureStage
                        .RequestPreparation,
                    exception);
            }

            DocumentDualRunDispatchOutcome dispatchOutcome;

            try
            {
                dispatchOutcome =
                    dispatcher.TryDispatch(
                        preparedJob);
            }
            catch (Exception exception)
                when (IsOrdinaryFailure(
                    exception))
            {
                return Failed(
                    selection,
                    DocumentDualRunSubmissionFailureStage
                        .Dispatch,
                    exception);
            }

            switch (dispatchOutcome)
            {
                case DocumentDualRunDispatchOutcome.Enqueued:
                    preparedJob =
                        null;

                    return DocumentDualRunSubmissionResult
                        .Enqueued(
                            selection,
                            jobId);

                case DocumentDualRunDispatchOutcome.QueueFull:
                    return DocumentDualRunSubmissionResult
                        .QueueFull(
                            selection,
                            jobId);

                case DocumentDualRunDispatchOutcome.Stopped:
                    return DocumentDualRunSubmissionResult
                        .DispatcherStopped(
                            selection,
                            jobId);

                default:
                    return Failed(
                        selection,
                        DocumentDualRunSubmissionFailureStage
                            .Dispatch,
                        new InvalidOperationException(
                            $"Unsupported Dual Run dispatch outcome " +
                            $"'{dispatchOutcome}'."));
            }
        }
        finally
        {
            if (preparedJob is not null)
            {
                await preparedJob
                    .DisposeAsync()
                    .ConfigureAwait(false);
            }
            else if (sourceSnapshot is not null)
            {
                await sourceSnapshot
                    .DisposeAsync()
                    .ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region Methods Failure

    private static DocumentDualRunSubmissionResult Failed(
        DocumentDualRunSelection selection,
        DocumentDualRunSubmissionFailureStage stage,
        Exception exception) =>
        DocumentDualRunSubmissionResult
            .Failed(
                selection,
                DocumentDualRunSubmissionFailure
                    .FromException(
                        stage,
                        exception));

    private static bool IsOrdinaryFailure(
        Exception exception) =>
        exception is not OutOfMemoryException;

    #endregion
}
