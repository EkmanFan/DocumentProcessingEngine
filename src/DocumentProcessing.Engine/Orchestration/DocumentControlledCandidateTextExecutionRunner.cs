using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// H.4D.1 controlled candidate execution.
///
/// Only candidate <see cref="TextExecutionMode.NativeText"/> is executed. All
/// OCR-backed text modes and every visual action remain explicitly deferred.
///
/// The legacy page list is already complete before this runner is invoked. This
/// runner produces comparison evidence only and cannot replace authoritative
/// output.
/// </summary>
public sealed class DocumentControlledCandidateTextExecutionRunner
{
    private readonly DocumentControlledCandidateTextExecutionDependencies
        _dependencies;

    public DocumentControlledCandidateTextExecutionRunner(
        DocumentControlledCandidateTextExecutionDependencies dependencies)
    {
        _dependencies =
            dependencies ??
            throw new ArgumentNullException(
                nameof(dependencies));
    }

    public async ValueTask<DocumentControlledCandidateTextExecutionReport>
        RunAsync(
            DocumentExtractionResult extraction,
            IReadOnlyList<HybridDocumentPage> authoritativeLegacyPages,
            DocumentShadowPlanningReport shadowPlanning,
            string sourceDocumentSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            authoritativeLegacyPages);

        ArgumentNullException.ThrowIfNull(
            shadowPlanning);

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Source document SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        cancellationToken.ThrowIfCancellationRequested();

        DocumentControlledCandidateTextExecutionReport report;

        int? currentPhysicalPageNumber =
            null;

        try
        {
            if (!shadowPlanning.IsCompleted)
            {
                report =
                    new DocumentControlledCandidateTextExecutionReport(
                        sourceDocumentSha256,
                        DocumentControlledCandidateTextExecutionStatus
                            .PlanningUnavailable,
                        pages:
                            []);
            }
            else
            {
                ValidateCoverage(
                    extraction,
                    authoritativeLegacyPages,
                    shadowPlanning,
                    sourceDocumentSha256);

                var comparisons =
                    new List<DocumentControlledCandidateTextPageComparison>(
                        extraction.Pages.Count);

                for (var index = 0;
                     index <
                     extraction.Pages.Count;
                     index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var extractionPage =
                        extraction.Pages[index];

                    var authoritativePage =
                        authoritativeLegacyPages[index];

                    var shadowPage =
                        shadowPlanning.Pages[index];

                    currentPhysicalPageNumber =
                        extractionPage.PhysicalPageNumber;

                    var candidatePlan =
                        shadowPage
                            .Shadow
                            .Candidate
                            .Plan;

                    if (candidatePlan.TextMode !=
                        TextExecutionMode.NativeText)
                    {
                        comparisons.Add(
                            new DocumentControlledCandidateTextPageComparison(
                                extractionPage.PhysicalPageNumber,
                                shadowPage
                                    .AuthoritativeLegacy
                                    .Plan
                                    .Route,
                                candidatePlan.TextMode,
                                DocumentControlledCandidateTextPageStatus
                                    .DeferredNonNativeTextMode,
                                candidateRemovesLegacyTextMl:
                                    false,
                                candidateHasIndependentVisualWork:
                                    shadowPage
                                        .Shadow
                                        .CandidateHasIndependentVisualWork));

                        continue;
                    }

                    var candidatePage =
                        NativeHybridPageAssembler
                            .Assemble(
                                extractionPage);

                    var authoritativeText =
                        authoritativePage
                            .AuthoritativeTextElements;

                    var candidateText =
                        candidatePage
                            .AuthoritativeTextElements;

                    comparisons.Add(
                        new DocumentControlledCandidateTextPageComparison(
                            extractionPage.PhysicalPageNumber,
                            shadowPage
                                .AuthoritativeLegacy
                                .Plan
                                .Route,
                            candidatePlan.TextMode,
                            DocumentControlledCandidateTextPageStatus
                                .ExecutedNativeText,
                            candidateRemovesLegacyTextMl:
                                shadowPage
                                    .CandidateRemovesLegacyTextMl,
                            candidateHasIndependentVisualWork:
                                shadowPage
                                    .Shadow
                                    .CandidateHasIndependentVisualWork,
                            SelectedTextSequenceExact(
                                authoritativeText,
                                candidateText),
                            TextProjectionExact(
                                authoritativeText,
                                candidateText),
                            authoritativeText.Count,
                            candidateText.Count,
                            authoritativeText.Count(
                                element =>
                                    element.Reconciliation is not null),
                            candidateText.Count(
                                element =>
                                    element.Reconciliation is not null)));
                }

                report =
                    new DocumentControlledCandidateTextExecutionReport(
                        sourceDocumentSha256,
                        DocumentControlledCandidateTextExecutionStatus.Completed,
                        comparisons);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException)
        {
            report =
                new DocumentControlledCandidateTextExecutionReport(
                    sourceDocumentSha256,
                    DocumentControlledCandidateTextExecutionStatus.Failed,
                    pages:
                        [],
                    new DocumentControlledCandidateTextExecutionFailure(
                        exception.GetType().FullName ??
                        exception.GetType().Name,
                        exception.Message,
                        currentPhysicalPageNumber));
        }

        await DeliverBestEffortAsync(
                report,
                cancellationToken)
            .ConfigureAwait(false);

        return report;
    }

    private static void ValidateCoverage(
        DocumentExtractionResult extraction,
        IReadOnlyList<HybridDocumentPage> authoritativeLegacyPages,
        DocumentShadowPlanningReport shadowPlanning,
        string sourceDocumentSha256)
    {
        if (!string.Equals(
                shadowPlanning.SourceDocumentSha256,
                sourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Controlled candidate source SHA-256 does not match shadow-planning evidence.");
        }

        if (authoritativeLegacyPages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Authoritative legacy page count {authoritativeLegacyPages.Count} " +
                $"does not match extraction page count {extraction.Pages.Count}.");
        }

        if (shadowPlanning.Pages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Shadow-planning page count {shadowPlanning.Pages.Count} " +
                $"does not match extraction page count {extraction.Pages.Count}.");
        }

        for (var index = 0;
             index <
             extraction.Pages.Count;
             index++)
        {
            var expected =
                extraction.Pages[index]
                    .PhysicalPageNumber;

            if (authoritativeLegacyPages[index]
                    .PhysicalPageNumber !=
                expected)
            {
                throw new InvalidDataException(
                    $"Authoritative legacy page at index {index} is " +
                    $"{authoritativeLegacyPages[index].PhysicalPageNumber}; " +
                    $"expected {expected}.");
            }

            if (shadowPlanning.Pages[index]
                    .PhysicalPageNumber !=
                expected)
            {
                throw new InvalidDataException(
                    $"Shadow-planning page at index {index} is " +
                    $"{shadowPlanning.Pages[index].PhysicalPageNumber}; " +
                    $"expected {expected}.");
            }
        }
    }

    private static bool SelectedTextSequenceExact(
        IReadOnlyList<HybridDocumentElement> authoritative,
        IReadOnlyList<HybridDocumentElement> candidate)
    {
        if (authoritative.Count !=
            candidate.Count)
        {
            return false;
        }

        for (var index = 0;
             index <
             authoritative.Count;
             index++)
        {
            if (!string.Equals(
                    authoritative[index].Text,
                    candidate[index].Text,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TextProjectionExact(
        IReadOnlyList<HybridDocumentElement> authoritative,
        IReadOnlyList<HybridDocumentElement> candidate)
    {
        if (authoritative.Count !=
            candidate.Count)
        {
            return false;
        }

        for (var index = 0;
             index <
             authoritative.Count;
             index++)
        {
            var left =
                authoritative[index];

            var right =
                candidate[index];

            if (left.ReadingOrder !=
                    right.ReadingOrder ||
                left.Kind !=
                    right.Kind ||
                left.Bounds !=
                    right.Bounds ||
                !string.Equals(
                    left.Text,
                    right.Text,
                    StringComparison.Ordinal) ||
                left.TextOrigin !=
                    right.TextOrigin ||
                left.NativeBlock?.SourceSequence !=
                    right.NativeBlock?.SourceSequence ||
                (left.Reconciliation is null) !=
                    (right.Reconciliation is null))
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask DeliverBestEffortAsync(
        DocumentControlledCandidateTextExecutionReport report,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dependencies
                .Observer
                .ObserveAsync(
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException)
        {
            // Candidate-execution telemetry is non-authoritative.
        }
    }
}
