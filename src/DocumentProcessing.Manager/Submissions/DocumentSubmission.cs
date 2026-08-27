using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Submissions;

/// <summary>
/// Immutable custody manifest for one submitted source document.
/// </summary>
public sealed record DocumentSubmission
{
    #region Properties

    /// <summary>
    /// Gets the caller-stable submission identity used for idempotency.
    /// </summary>
    public DocumentSubmissionId SubmissionId { get; }

    /// <summary>
    /// Gets the immutable source-artifact descriptor.
    /// </summary>
    public SourceArtifact SourceArtifact { get; }

    /// <summary>
    /// Gets the leaf filename supplied at submission time.
    /// </summary>
    public string OriginalFileName { get; }

    /// <summary>
    /// Gets the optional untrusted media type declared by the caller.
    /// </summary>
    public string? DeclaredMediaType { get; }

    /// <summary>
    /// Gets the optional caller-supplied source-origin description.
    /// </summary>
    public string? SourceOrigin { get; }

    /// <summary>
    /// Gets the instant at which custody was first registered.
    /// </summary>
    public DateTimeOffset SubmittedAtUtc { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one immutable document-submission manifest.
    /// </summary>
    public DocumentSubmission(
        DocumentSubmissionId submissionId,
        SourceArtifact sourceArtifact,
        string originalFileName,
        string? declaredMediaType,
        string? sourceOrigin,
        DateTimeOffset submittedAtUtc)
    {
        if (submissionId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Document submission identifier cannot be empty.",
                nameof(submissionId));
        }

        SubmissionId =
            submissionId;

        SourceArtifact =
            sourceArtifact ??
            throw new ArgumentNullException(
                nameof(sourceArtifact));

        OriginalFileName =
            NormalizeFileName(
                originalFileName);

        DeclaredMediaType =
            NormalizeOptionalValue(
                declaredMediaType,
                nameof(declaredMediaType));

        SourceOrigin =
            NormalizeOptionalValue(
                sourceOrigin,
                nameof(sourceOrigin));

        SubmittedAtUtc =
            submittedAtUtc.ToUniversalTime();
    }

    #endregion

    #region Methods Validation

    internal static string NormalizeFileName(
        string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(
                originalFileName))
        {
            throw new ArgumentException(
                "Original source filename cannot be empty.",
                nameof(originalFileName));
        }

        var normalizedSeparators =
            originalFileName.Trim().Replace(
                '\\',
                '/');

        var lastSeparator =
            normalizedSeparators.LastIndexOf(
                '/');

        var leafName =
            lastSeparator >=
            0
                ? normalizedSeparators[(lastSeparator + 1)..]
                : normalizedSeparators;

        if (string.IsNullOrWhiteSpace(
                leafName) ||
            leafName is "." or ".." ||
            leafName.Any(
                char.IsControl))
        {
            throw new ArgumentException(
                "Original source filename must identify a valid leaf name.",
                nameof(originalFileName));
        }

        return leafName;
    }

    internal static string? NormalizeOptionalValue(
        string? value,
        string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length ==
            0 ||
            normalized.Any(
                char.IsControl))
        {
            throw new ArgumentException(
                "Optional custody metadata cannot be empty or contain control characters.",
                parameterName);
        }

        return normalized;
    }

    #endregion
}
