using DocumentProcessing.Core.Documents;
using DocumentProcessing.Epub;
using DocumentProcessing.Epub.Validation;

namespace DocumentProcessing.UnitTests.Epub;

public sealed class EpubCheckConformanceValidatorTests
{
    #region Methods Tests

    [Fact]
    public void EpubCheckOptions_DefaultTimeoutIsTwoMinutesAndCanBeOverridden()
    {
        var distributionDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "epubcheck-options");

        var defaultOptions =
            new EpubCheckOptions(
                distributionDirectory);

        Assert.Equal(
            TimeSpan.FromMinutes(
                2),
            defaultOptions.Timeout);

        var configuredTimeout =
            TimeSpan.FromMinutes(
                7);

        var configuredOptions =
            new EpubCheckOptions(
                distributionDirectory,
                timeout:
                    configuredTimeout);

        Assert.Equal(
            configuredTimeout,
            configuredOptions.Timeout);
    }

    [Fact]
    public async Task ValidateAsync_ConformantReport_ReturnsConformant()
    {
        using var fixture =
            new ValidationFixture(
                CompletedWithReport(
                    exitCode:
                        0,
                    """
                    { "messages": [] }
                    """));

        var result =
            await fixture.Validator
                .ValidateAsync(
                    fixture.EpubPath);

        Assert.Equal(
            EpubCheckConformanceStatus.Conformant,
            result.Status);
    }

    [Fact]
    public async Task ValidateAsync_ConformantReportLargerThanOneMegabyte_ReturnsConformant()
    {
        var publicationDescription =
            new string(
                'x',
                1024 *
                1024 +
                256 *
                1024);

        using var fixture =
            new ValidationFixture(
                CompletedWithReport(
                    exitCode:
                        0,
                    $$"""
                    {
                      "publication": {
                        "description": "{{publicationDescription}}"
                      },
                      "messages": []
                    }
                    """));

        var result =
            await fixture.Validator
                .ValidateAsync(
                    fixture.EpubPath);

        Assert.Equal(
            EpubCheckConformanceStatus.Conformant,
            result.Status);
    }

    [Fact]
    public async Task ValidateAsync_WarningReport_ReturnsNonConformant()
    {
        using var fixture =
            new ValidationFixture(
                CompletedWithReport(
                    exitCode:
                        1,
                    """
                    {
                      "messages": [
                        { "severity": "WARNING", "ID": "TEST-001" }
                      ]
                    }
                    """));

        var result =
            await fixture.Validator
                .ValidateAsync(
                    fixture.EpubPath);

        Assert.Equal(
            EpubCheckConformanceStatus.NonConformant,
            result.Status);
    }

    [Fact]
    public async Task ValidateAsync_MalformedMessage_ReturnsFailed()
    {
        using var fixture =
            new ValidationFixture(
                CompletedWithReport(
                    exitCode:
                        0,
                    """
                    { "messages": [ { "ID": "missing-severity" } ] }
                    """));

        var result =
            await fixture.Validator
                .ValidateAsync(
                    fixture.EpubPath);

        Assert.Equal(
            EpubCheckConformanceStatus.Failed,
            result.Status);
    }

    [Theory]
    [InlineData(
        EpubCheckProcessOutcome.Unavailable,
        EpubCheckConformanceStatus.Unavailable)]
    [InlineData(
        EpubCheckProcessOutcome.Failed,
        EpubCheckConformanceStatus.Failed)]
    [InlineData(
        EpubCheckProcessOutcome.TimedOut,
        EpubCheckConformanceStatus.TimedOut)]
    internal async Task ValidateAsync_ProcessFailure_PreservesInternalStatus(
        EpubCheckProcessOutcome processOutcome,
        EpubCheckConformanceStatus expectedStatus)
    {
        using var fixture =
            new ValidationFixture(
                (_, _) =>
                    Task.FromResult(
                        new EpubCheckProcessResult(
                            processOutcome,
                            StandardError:
                                "technical checker detail")));

        var result =
            await fixture.Validator
                .ValidateAsync(
                    fixture.EpubPath);

        Assert.Equal(
            expectedStatus,
            result.Status);
    }

    [Fact]
    public async Task ValidateAsync_MissingDistribution_ReturnsUnavailable()
    {
        var missingDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "dpengine-epubcheck-tests",
                Guid.NewGuid()
                    .ToString(
                        "N"));

        var validator =
            new EpubCheckConformanceValidator(
                new EpubCheckOptions(
                    missingDirectory));

        var result =
            await validator.ValidateAsync(
                Path.Combine(
                    missingDirectory,
                    "book.epub"));

        Assert.Equal(
            EpubCheckConformanceStatus.Unavailable,
            result.Status);
    }

    [Fact]
    public async Task ValidateAsync_UnexpectedJarIdentity_ReturnsFailedWithoutRunningChecker()
    {
        var runner =
            new StubProcessRunner(
                (_, _) =>
                    throw new InvalidOperationException(
                        "Runner must not be invoked."));

        using var fixture =
            new ValidationFixture(
                runner,
                new RejectingJarIdentityVerifier());

        var result =
            await fixture.Validator
                .ValidateAsync(
                    fixture.EpubPath);

        Assert.Equal(
            EpubCheckConformanceStatus.Failed,
            result.Status);
    }

    [Fact]
    public async Task ValidateAsync_CallerCancellation_RemainsCancellation()
    {
        using var fixture =
            new ValidationFixture(
                (_, _) =>
                    throw new InvalidOperationException(
                        "Runner must not be invoked."));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                fixture.Validator.ValidateAsync(
                    fixture.EpubPath,
                    cancellation.Token));
    }

    [Fact]
    public void ConformanceResult_ContainsNoTechnicalDiagnosticPayload()
    {
        var properties =
            typeof(EpubCheckConformanceResult)
                .GetProperties();

        var property =
            Assert.Single(
                properties);

        Assert.Equal(
            nameof(EpubCheckConformanceResult.Status),
            property.Name);
    }

    [Theory]
    [InlineData(EpubCheckConformanceStatus.Unavailable)]
    [InlineData(EpubCheckConformanceStatus.Failed)]
    [InlineData(EpubCheckConformanceStatus.TimedOut)]
    internal void MapFailure_TechnicalCheckerFailuresCollapseToOnePublicMessage(
        EpubCheckConformanceStatus status)
    {
        var failure =
            Assert.IsType<NativeEvidenceExtractionResult.Unavailable>(
                EpubConformanceOutcomeMapper.MapFailure(
                    status));

        Assert.Equal(
            "La validation EPUB est temporairement indisponible.",
            failure.Reason);

        Assert.DoesNotContain(
            "java",
            failure.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapFailure_NonConformantEpubReturnsInvalidDocumentMessage()
    {
        var failure =
            Assert.IsType<NativeEvidenceExtractionResult.Invalid>(
                EpubConformanceOutcomeMapper.MapFailure(
                    EpubCheckConformanceStatus.NonConformant));

        Assert.Equal(
            "Le fichier EPUB n’est pas conforme.",
            failure.Reason);
    }

    [Fact]
    public void MapFailure_ConformantEpubContinuesAcquisition()
    {
        Assert.Null(
            EpubConformanceOutcomeMapper.MapFailure(
                EpubCheckConformanceStatus.Conformant));
    }

    #endregion

    #region Methods Fixtures

    private static Func<EpubCheckProcessRequest, CancellationToken,
            Task<EpubCheckProcessResult>>
        CompletedWithReport(
            int exitCode,
            string reportJson) =>
        async (request, cancellationToken) =>
        {
            await File.WriteAllTextAsync(
                request.ReportPath,
                reportJson,
                cancellationToken);

            return new EpubCheckProcessResult(
                EpubCheckProcessOutcome.Completed,
                exitCode);
        };

    #endregion

    #region Test Types

    private sealed class ValidationFixture
        : IDisposable
    {
        public string DirectoryPath { get; }

        public string EpubPath { get; }

        public EpubCheckConformanceValidator Validator { get; }

        public ValidationFixture(
            Func<EpubCheckProcessRequest, CancellationToken,
                Task<EpubCheckProcessResult>> run) :
            this(
                new StubProcessRunner(
                    run),
                new AcceptingJarIdentityVerifier())
        {
        }

        public ValidationFixture(
            IEpubCheckProcessRunner processRunner,
            IEpubCheckJarIdentityVerifier jarIdentityVerifier)
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "dpengine-epubcheck-tests",
                    Guid.NewGuid()
                        .ToString(
                            "N"));

            Directory.CreateDirectory(
                DirectoryPath);

            File.WriteAllBytes(
                Path.Combine(
                    DirectoryPath,
                    "epubcheck.jar"),
                [1]);

            EpubPath =
                Path.Combine(
                    DirectoryPath,
                    "book.epub");

            File.WriteAllBytes(
                EpubPath,
                [2]);

            Validator =
                new EpubCheckConformanceValidator(
                    new EpubCheckOptions(
                        DirectoryPath),
                    processRunner,
                    jarIdentityVerifier);
        }

        public void Dispose()
        {
            Directory.Delete(
                DirectoryPath,
                recursive:
                    true);
        }
    }

    private sealed class StubProcessRunner(
        Func<EpubCheckProcessRequest, CancellationToken,
            Task<EpubCheckProcessResult>> run)
        : IEpubCheckProcessRunner
    {
        public Task<EpubCheckProcessResult> RunAsync(
            EpubCheckProcessRequest request,
            CancellationToken cancellationToken = default) =>
            run(
                request,
                cancellationToken);
    }

    private sealed class AcceptingJarIdentityVerifier
        : IEpubCheckJarIdentityVerifier
    {
        public bool MatchesPinnedVersion(
            string jarPath) =>
            true;
    }

    private sealed class RejectingJarIdentityVerifier
        : IEpubCheckJarIdentityVerifier
    {
        public bool MatchesPinnedVersion(
            string jarPath) =>
            false;
    }

    #endregion
}
