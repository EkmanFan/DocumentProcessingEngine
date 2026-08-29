using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Submissions;

/// <summary>
/// Requests immutable custody and initial processing-unit intake.
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

    /// <summary>Gets the requested processing-unit scopes in queue order.</summary>
    public IReadOnlyList<ProcessingUnitScope> Scopes { get; }

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
            ProcessingUnitDispatchState.Shelved,
        IReadOnlyList<ProcessingUnitScope>? scopes = null)
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

        Scopes =
            ValidateScopes(
                scopes ??
                [new ProcessingUnitScope.WholeDocument()]);
    }

    #endregion

    #region Methods Validation

    private static IReadOnlyList<ProcessingUnitScope> ValidateScopes(
        IReadOnlyList<ProcessingUnitScope> scopes)
    {
        ArgumentNullException.ThrowIfNull(
            scopes);

        if (scopes.Count == 0)
        {
            throw new ArgumentException(
                "At least one processing-unit scope is required.",
                nameof(scopes));
        }

        if (scopes.Any(scope => scope is null))
        {
            throw new ArgumentException(
                "Processing-unit scopes cannot contain null entries.",
                nameof(scopes));
        }

        if (scopes.Any(scope => scope is ProcessingUnitScope.WholeDocument) &&
            scopes.Count != 1)
        {
            throw new ArgumentException(
                "A whole-document scope cannot be combined with page ranges.",
                nameof(scopes));
        }

        var orderedRanges =
            scopes
                .OfType<ProcessingUnitScope.PageRange>()
                .OrderBy(range => range.StartPhysicalPageNumber)
                .ThenBy(range => range.EndPhysicalPageNumber)
                .ToArray();

        for (var index = 1; index < orderedRanges.Length; index++)
        {
            if (orderedRanges[index].StartPhysicalPageNumber <=
                orderedRanges[index - 1].EndPhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Processing-unit page ranges cannot overlap.",
                    nameof(scopes));
            }
        }

        return scopes.ToArray();
    }

    #endregion
}
