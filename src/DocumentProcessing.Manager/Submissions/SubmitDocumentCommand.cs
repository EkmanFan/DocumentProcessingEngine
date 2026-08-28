using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Submissions;

/// <summary>
/// Requests immutable custody and initial whole-document processing intake.
/// </summary>
public sealed class SubmitDocumentCommand
{
    #region Properties

    /// <summary>
    /// Gets the caller-stable submission identity used for retries.
    /// </summary>
    public DocumentSubmissionId SubmissionId { get; }

    /// <summary>
    /// Gets the readable exact source content.
    /// </summary>
    public Stream Content { get; }

    /// <summary>
    /// Gets the caller-supplied original filename.
    /// </summary>
    public string OriginalFileName { get; }

    /// <summary>
    /// Gets the optional untrusted caller-declared media type.
    /// </summary>
    public string? DeclaredMediaType { get; }

    /// <summary>
    /// Gets the optional caller-supplied origin description.
    /// </summary>
    public string? SourceOrigin { get; }

    /// <summary>
    /// Gets the initial dispatch state of the generated processing unit.
    /// </summary>
    public ProcessingUnitDispatchState InitialDispatchState { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a document-submission command.
    /// </summary>
    public SubmitDocumentCommand(
        DocumentSubmissionId submissionId,
        Stream content,
        string originalFileName,
        string? declaredMediaType = null,
        string? sourceOrigin = null,
        ProcessingUnitDispatchState initialDispatchState =
            ProcessingUnitDispatchState.Shelved)
    {
        if (submissionId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Document submission identifier cannot be empty.",
                nameof(submissionId));
        }

        ArgumentNullException.ThrowIfNull(
            content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "Document submission content must be readable.",
                nameof(content));
        }

        SubmissionId =
            submissionId;

        Content =
            content;

        OriginalFileName =
            DocumentSubmission.NormalizeFileName(
                originalFileName);

        DeclaredMediaType =
            DocumentSubmission.NormalizeOptionalValue(
                declaredMediaType,
                nameof(declaredMediaType));

        SourceOrigin =
            DocumentSubmission.NormalizeOptionalValue(
                sourceOrigin,
                nameof(sourceOrigin));

        if (!Enum.IsDefined(
                initialDispatchState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialDispatchState),
                initialDispatchState,
                "Unknown initial processing-unit dispatch state.");
        }

        InitialDispatchState =
            initialDispatchState;
    }

    #endregion
}
