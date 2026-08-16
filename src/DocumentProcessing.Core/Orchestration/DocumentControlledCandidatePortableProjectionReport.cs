namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Non-authoritative H.4D.4B.1 candidate portable-output/provenance report.
/// </summary>
public sealed record DocumentControlledCandidatePortableProjectionReport
{
    public DocumentControlledCandidatePortableProjectionReport(
        string sourceDocumentSha256,
        DocumentControlledCandidatePortableProjectionStatus status,
        DocumentControlledCandidatePortableOutput? output = null,
        DocumentControlledCandidatePortableProjectionFailure? failure = null)
    {
        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        var normalizedSourceSha =
            sourceDocumentSha256
                .Trim()
                .ToLowerInvariant();

        if (normalizedSourceSha.Length !=
                64 ||
            normalizedSourceSha.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Source SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(sourceDocumentSha256));
        }

        switch (status)
        {
            case DocumentControlledCandidatePortableProjectionStatus.Completed:
                if (output is null)
                {
                    throw new ArgumentNullException(
                        nameof(output),
                        "Completed projection requires candidate output.");
                }

                if (failure is not null)
                {
                    throw new ArgumentException(
                        "Completed projection cannot carry failure evidence.",
                        nameof(failure));
                }

                if (!string.Equals(
                        output.CandidateDocument.Source.Sha256,
                        normalizedSourceSha,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Candidate output belongs to a different source document.",
                        nameof(output));
                }

                break;

            case DocumentControlledCandidatePortableProjectionStatus.InputUnavailable:
                if (output is not null ||
                    failure is not null)
                {
                    throw new ArgumentException(
                        "Input-unavailable projection cannot carry output or failure.");
                }

                break;

            case DocumentControlledCandidatePortableProjectionStatus.Failed:
                if (output is not null)
                {
                    throw new ArgumentException(
                        "Failed projection cannot carry candidate output.",
                        nameof(output));
                }

                if (failure is null)
                {
                    throw new ArgumentNullException(
                        nameof(failure),
                        "Failed projection requires failure evidence.");
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status));
        }

        SourceDocumentSha256 =
            normalizedSourceSha;

        Status =
            status;

        Output =
            output;

        Failure =
            failure;
    }

    public string SourceDocumentSha256 { get; }

    public DocumentControlledCandidatePortableProjectionStatus Status { get; }

    public DocumentControlledCandidatePortableOutput? Output { get; }

    public DocumentControlledCandidatePortableProjectionFailure? Failure { get; }

    public bool CandidateDocumentBuilt =>
        Output is not null;

    public bool CandidateProvenanceBuilt =>
        Output is not null;

    public bool HasUnpersistedSourceVisualAssets =>
        Output?.HasUnpersistedSourceVisualAssets ??
        false;

    public bool HasUnresolvedVisualAnalysis =>
        Output?.HasUnresolvedVisualAnalysis ??
        false;

    /// <summary>
    /// H.4D.4B.1 intentionally cannot authorize cutover. This property only
    /// states whether the B.1 projection is structurally ready for the final
    /// B.2 comparison/persistence work.
    /// </summary>
    public bool ReadyForFinalCutoverComparison =>
        Status ==
            DocumentControlledCandidatePortableProjectionStatus.Completed &&
        Output is
            {
                IsCompleteForFinalCutoverComparison: true
            };
}
