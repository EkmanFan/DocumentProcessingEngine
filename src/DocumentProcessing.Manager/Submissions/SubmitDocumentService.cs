using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Submissions;

/// <summary>
/// Preserves exact source bytes and registers initial processing atomically.
/// </summary>
/// <remarks>
/// Physical custody is completed before relational registration. A database
/// failure may therefore leave an unreferenced content-addressed artifact, but
/// can never leave a durable submission pointing to missing source bytes.
/// </remarks>
public sealed class SubmitDocumentService
{
    #region Variables and Constants

    private readonly ISourceArtifactWriter
        _artifactWriter;

    private readonly IDocumentSubmissionWriter
        _submissionWriter;

    private readonly TimeProvider
        _timeProvider;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the document-submission use case.
    /// </summary>
    public SubmitDocumentService(
        ISourceArtifactWriter artifactWriter,
        IDocumentSubmissionWriter submissionWriter,
        TimeProvider? timeProvider = null)
    {
        _artifactWriter =
            artifactWriter ??
            throw new ArgumentNullException(
                nameof(artifactWriter));

        _submissionWriter =
            submissionWriter ??
            throw new ArgumentNullException(
                nameof(submissionWriter));

        _timeProvider =
            timeProvider ??
            TimeProvider.System;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Preserves a source and idempotently registers its requested processing units.
    /// </summary>
    public async ValueTask<DocumentSubmissionRegistration> SubmitAsync(
        SubmitDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        var sourceArtifact =
            await _artifactWriter
                .StoreAsync(
                    command.Content,
                    cancellationToken)
                .ConfigureAwait(false);

        var submission =
            new DocumentSubmission(
                command.SubmissionId,
                sourceArtifact,
                command.OriginalFileName,
                command.DeclaredMediaType,
                command.SourceOrigin,
                _timeProvider.GetUtcNow());

        var intakes =
            command.Scopes
                .Select(
                    scope =>
                        new ProcessingUnitIntake(
                            new ProcessingWorkItem(
                                ProcessingUnitId.New(),
                                submission.SubmissionId,
                                scope,
                                attemptNumber:
                                    1),
                            command.InitialDispatchState))
                .ToArray();

        return await _submissionWriter
            .RegisterAsync(
                submission,
                intakes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion
}
