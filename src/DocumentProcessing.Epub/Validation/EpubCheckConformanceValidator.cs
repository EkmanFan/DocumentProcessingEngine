using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentProcessing.Epub.Validation;

/// <summary>
/// Applies the V1 EPUB acceptance policy through the pinned official
/// EPUBCheck distribution.
/// </summary>
internal sealed class EpubCheckConformanceValidator
{
    #region Variables and Constants

    private const long MaximumReportBytes =
        1024 *
        1024;

    private readonly EpubCheckOptions _options;
    private readonly IEpubCheckProcessRunner _processRunner;
    private readonly IEpubCheckJarIdentityVerifier
        _jarIdentityVerifier;
    private readonly ILogger<EpubCheckConformanceValidator>
        _logger;

    #endregion

    #region ctor

    public EpubCheckConformanceValidator(
        EpubCheckOptions options,
        IEpubCheckProcessRunner? processRunner = null,
        IEpubCheckJarIdentityVerifier? jarIdentityVerifier = null,
        ILogger<EpubCheckConformanceValidator>? logger = null)
    {
        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        _processRunner =
            processRunner ??
            new EpubCheckProcessRunner();

        _jarIdentityVerifier =
            jarIdentityVerifier ??
            new EpubCheckJarIdentityVerifier();

        _logger =
            logger ??
            NullLogger<EpubCheckConformanceValidator>
                .Instance;
    }

    #endregion

    #region Methods Validation

    public async Task<EpubCheckConformanceResult> ValidateAsync(
        string epubPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                epubPath))
        {
            throw new ArgumentException(
                "EPUB path cannot be empty.",
                nameof(epubPath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(
                _options.EpubCheckJarPath))
        {
            _logger.LogError(
                "EPUBCheck {EpubCheckVersion} is unavailable because its JAR is missing from {DistributionDirectoryPath}.",
                EpubCheckOptions.SupportedVersion,
                _options.DistributionDirectoryPath);

            return Result(
                EpubCheckConformanceStatus.Unavailable);
        }

        if (!_jarIdentityVerifier.MatchesPinnedVersion(
                _options.EpubCheckJarPath))
        {
            _logger.LogError(
                "EPUBCheck JAR identity does not match pinned version {EpubCheckVersion}.",
                EpubCheckOptions.SupportedVersion);

            return Result(
                EpubCheckConformanceStatus.Failed);
        }

        var workDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "dpengine-epubcheck",
                Guid.NewGuid()
                    .ToString(
                        "N"));

        try
        {
            Directory.CreateDirectory(
                workDirectory);

            var reportPath =
                Path.Combine(
                    workDirectory,
                    "report.json");

            var processResult =
                await _processRunner
                    .RunAsync(
                        new EpubCheckProcessRequest(
                            _options.JavaExecutablePath,
                            _options.EpubCheckJarPath,
                            Path.GetFullPath(
                                epubPath),
                            reportPath,
                            _options.Timeout),
                        cancellationToken)
                    .ConfigureAwait(false);

            return processResult.Outcome switch
            {
                EpubCheckProcessOutcome.Unavailable =>
                    LogAndReturn(
                        EpubCheckConformanceStatus.Unavailable,
                        processResult),

                EpubCheckProcessOutcome.TimedOut =>
                    LogAndReturn(
                        EpubCheckConformanceStatus.TimedOut,
                        processResult),

                EpubCheckProcessOutcome.Failed =>
                    LogAndReturn(
                        EpubCheckConformanceStatus.Failed,
                        processResult),

                EpubCheckProcessOutcome.Completed =>
                    ReadCompletedResult(
                        reportPath,
                        processResult),

                _ =>
                    LogAndReturn(
                        EpubCheckConformanceStatus.Failed,
                        processResult)
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  InvalidOperationException or
                  ArgumentException)
        {
            _logger.LogError(
                exception,
                "EPUBCheck validation failed before a conformance result was available.");

            return Result(
                EpubCheckConformanceStatus.Failed);
        }
        finally
        {
            TryDeleteWorkDirectory(
                workDirectory);
        }
    }

    #endregion

    #region Methods Result Classification

    private EpubCheckConformanceResult ReadCompletedResult(
        string reportPath,
        EpubCheckProcessResult processResult)
    {
        try
        {
            var report =
                new FileInfo(
                    reportPath);

            if (!report.Exists ||
                report.Length <=
                    0 ||
                report.Length >
                    MaximumReportBytes)
            {
                return LogAndReturn(
                    EpubCheckConformanceStatus.Failed,
                    processResult);
            }

            using var stream =
                File.OpenRead(
                    reportPath);

            using var document =
                JsonDocument.Parse(
                    stream);

            if (!document.RootElement.TryGetProperty(
                    "messages",
                    out var messages) ||
                messages.ValueKind !=
                    JsonValueKind.Array)
            {
                return LogAndReturn(
                    EpubCheckConformanceStatus.Failed,
                    processResult);
            }

            if (!TryClassifyMessages(
                    messages,
                    out var hasConformanceProblem))
            {
                return LogAndReturn(
                    EpubCheckConformanceStatus.Failed,
                    processResult);
            }

            if (hasConformanceProblem)
            {
                return Result(
                    EpubCheckConformanceStatus.NonConformant);
            }

            if (processResult.ExitCode ==
                0)
            {
                return Result(
                    EpubCheckConformanceStatus.Conformant);
            }

            return LogAndReturn(
                EpubCheckConformanceStatus.Failed,
                processResult);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  JsonException)
        {
            _logger.LogError(
                exception,
                "EPUBCheck produced an unreadable conformance report.");

            return Result(
                EpubCheckConformanceStatus.Failed);
        }
    }

    private static bool TryClassifyMessages(
        JsonElement messages,
        out bool hasConformanceProblem)
    {
        hasConformanceProblem =
            false;

        foreach (var message in
                 messages.EnumerateArray())
        {
            if (!message.TryGetProperty(
                    "severity",
                    out var severity) ||
                severity.ValueKind !=
                    JsonValueKind.String)
            {
                return false;
            }

            switch (severity.GetString())
            {
                case "FATAL":
                case "ERROR":
                case "WARNING":
                    hasConformanceProblem =
                        true;
                    break;

                case "USAGE":
                case "INFO":
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Methods Diagnostics and Cleanup

    private EpubCheckConformanceResult LogAndReturn(
        EpubCheckConformanceStatus status,
        EpubCheckProcessResult processResult)
    {
        _logger.LogError(
            processResult.Exception,
            "EPUBCheck ended with internal status {Status}, exit code {ExitCode}, stderr {StandardError} and stdout {StandardOutput}.",
            status,
            processResult.ExitCode,
            NormalizeDiagnostic(
                processResult.StandardError),
            NormalizeDiagnostic(
                processResult.StandardOutput));

        return Result(
            status);
    }

    private static string NormalizeDiagnostic(
        string value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? string.Empty
            : new string(
                value
                    .Where(
                        character =>
                            !char.IsControl(
                                character) ||
                            character is '\n' or '\r' or '\t')
                    .ToArray())
                .Trim();

    private static EpubCheckConformanceResult Result(
        EpubCheckConformanceStatus status) =>
        new(
            status);

    private static void TryDeleteWorkDirectory(
        string workDirectory)
    {
        try
        {
            Directory.Delete(
                workDirectory,
                recursive:
                    true);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
        }
    }

    #endregion
}
