using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;

namespace DocumentProcessing.Core.DualRun.Transport;

/// <summary>
/// Compact authoritative page baseline transported to the isolated Dual Run
/// worker. It intentionally contains fingerprints and counts rather than the
/// authoritative HybridDocumentPage object graph.
/// </summary>
public sealed record DocumentDualRunAuthoritativePageBaseline
{
    #region Properties

    public int PhysicalPageNumber { get; }

    public NativeTextStatus NativeTextStatus { get; }

    public PageProcessingRoute AuthoritativeRoute { get; }

    public string SelectedTextSequenceSha256 { get; }

    public string TextProjectionSha256 { get; }

    public int AuthoritativeTextElementCount { get; }

    public int AuthoritativeReconciliationEvidenceCount { get; }

    #endregion

    #region ctor

    public DocumentDualRunAuthoritativePageBaseline(
        int physicalPageNumber,
        NativeTextStatus nativeTextStatus,
        PageProcessingRoute authoritativeRoute,
        string selectedTextSequenceSha256,
        string textProjectionSha256,
        int authoritativeTextElementCount,
        int authoritativeReconciliationEvidenceCount)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (!Enum.IsDefined(
                nativeTextStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nativeTextStatus));
        }

        if (!Enum.IsDefined(
                authoritativeRoute))
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoritativeRoute));
        }

        if (authoritativeTextElementCount <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoritativeTextElementCount));
        }

        if (authoritativeReconciliationEvidenceCount <
                0 ||
            authoritativeReconciliationEvidenceCount >
                authoritativeTextElementCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoritativeReconciliationEvidenceCount));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        NativeTextStatus =
            nativeTextStatus;

        AuthoritativeRoute =
            authoritativeRoute;

        SelectedTextSequenceSha256 =
            DocumentDualRunTransportValidation
                .Sha256(
                    selectedTextSequenceSha256,
                    nameof(selectedTextSequenceSha256));

        TextProjectionSha256 =
            DocumentDualRunTransportValidation
                .Sha256(
                    textProjectionSha256,
                    nameof(textProjectionSha256));

        AuthoritativeTextElementCount =
            authoritativeTextElementCount;

        AuthoritativeReconciliationEvidenceCount =
            authoritativeReconciliationEvidenceCount;
    }

    #endregion

    #region Methods Factory

    public static DocumentDualRunAuthoritativePageBaseline From(
        PageProcessingDecision authoritativeDecision,
        HybridDocumentPage authoritativePage)
    {
        ArgumentNullException.ThrowIfNull(
            authoritativeDecision);

        ArgumentNullException.ThrowIfNull(
            authoritativePage);

        if (authoritativeDecision.PhysicalPageNumber !=
            authoritativePage.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Authoritative decision and page must refer to the same physical page.",
                nameof(authoritativePage));
        }

        var authoritativeText =
            authoritativePage
                .AuthoritativeTextElements;

        return new DocumentDualRunAuthoritativePageBaseline(
            authoritativePage.PhysicalPageNumber,
            authoritativeDecision
                .Assessment
                .NativeTextStatus,
            authoritativeDecision
                .Plan
                .Route,
            DocumentDualRunTextFingerprint
                .SelectedTextSequenceSha256(
                    authoritativeText),
            DocumentDualRunTextFingerprint
                .TextProjectionSha256(
                    authoritativeText),
            authoritativeText.Count,
            authoritativeText.Count(
                element =>
                    element.Reconciliation is not null));
    }

    #endregion
}

/// <summary>
/// Immutable request accepted by the isolated Dual Run worker.
/// </summary>
public sealed record DocumentDualRunWorkerRequest
{
    #region Properties

    public Guid JobId { get; }

    public DocumentDualRunExecutionMode ExecutionMode { get; }

    public string EngineVersion { get; }

    public string SourceSnapshotPath { get; }

    public string SourceDocumentSha256 { get; }

    public long SourceByteLength { get; }

    public DocumentFormatId Format { get; }

    public string? FileName { get; }

    public string? DeclaredMediaType { get; }

    public IReadOnlyList<DocumentDualRunAuthoritativePageBaseline>
        AuthoritativePages { get; }

    #endregion

    #region ctor

    public DocumentDualRunWorkerRequest(
        Guid jobId,
        DocumentDualRunExecutionMode executionMode,
        string engineVersion,
        string sourceSnapshotPath,
        string sourceDocumentSha256,
        long sourceByteLength,
        DocumentFormatId format,
        IEnumerable<DocumentDualRunAuthoritativePageBaseline> authoritativePages,
        string? fileName = null,
        string? declaredMediaType = null)
    {
        if (jobId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Dual Run job ID cannot be empty.",
                nameof(jobId));
        }

        if (!Enum.IsDefined(
                executionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionMode));
        }

        if (sourceByteLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceByteLength));
        }

        ArgumentNullException.ThrowIfNull(
            authoritativePages);

        var materialized =
            authoritativePages
                .ToArray();

        if (materialized.Length ==
            0)
        {
            throw new ArgumentException(
                "Dual Run worker request requires at least one authoritative page baseline.",
                nameof(authoritativePages));
        }

        for (var index = 0;
             index <
             materialized.Length;
             index++)
        {
            var page =
                materialized[index] ??
                throw new ArgumentException(
                    "Authoritative page baselines cannot contain null values.",
                    nameof(authoritativePages));

            var expectedPhysicalPageNumber =
                index +
                1;

            if (page.PhysicalPageNumber !=
                expectedPhysicalPageNumber)
            {
                throw new ArgumentException(
                    $"Authoritative page baselines must be contiguous and one-based; " +
                    $"expected page {expectedPhysicalPageNumber}, observed " +
                    $"{page.PhysicalPageNumber}.",
                    nameof(authoritativePages));
            }
        }

        JobId =
            jobId;

        ExecutionMode =
            executionMode;

        EngineVersion =
            DocumentDualRunTransportValidation
                .RequiredText(
                    engineVersion,
                    nameof(engineVersion));

        SourceSnapshotPath =
            DocumentDualRunTransportValidation
                .SourceSnapshotPath(
                    sourceSnapshotPath,
                    nameof(sourceSnapshotPath));

        SourceDocumentSha256 =
            DocumentDualRunTransportValidation
                .Sha256(
                    sourceDocumentSha256,
                    nameof(sourceDocumentSha256));

        SourceByteLength =
            sourceByteLength;

        Format =
            format;

        FileName =
            DocumentDualRunTransportValidation
                .OptionalText(
                    fileName);

        DeclaredMediaType =
            DocumentDualRunTransportValidation
                .OptionalText(
                    declaredMediaType);

        AuthoritativePages =
            Array.AsReadOnly(
                materialized);
    }

    #endregion
}
