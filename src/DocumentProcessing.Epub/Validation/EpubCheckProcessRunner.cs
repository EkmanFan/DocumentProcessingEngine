using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DocumentProcessing.Epub.Validation;

/// <summary>
/// Supervised invocation of the official Java EPUBCheck command-line entry
/// point. No shell participates in argument interpretation.
/// </summary>
internal sealed class EpubCheckProcessRunner
    : IEpubCheckProcessRunner
{
    #region Variables and Constants

    private const int MaximumDiagnosticCharacters =
        8192;

    #endregion

    #region Methods Execution

    public async Task<EpubCheckProcessResult> RunAsync(
        EpubCheckProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        var startInfo =
            new ProcessStartInfo(
                request.JavaExecutablePath)
            {
                UseShellExecute =
                    false,
                CreateNoWindow =
                    true,
                RedirectStandardOutput =
                    true,
                RedirectStandardError =
                    true,
                WorkingDirectory =
                    Path.GetDirectoryName(
                        request.EpubCheckJarPath) ??
                    Environment.CurrentDirectory,
                StandardOutputEncoding =
                    Encoding.UTF8,
                StandardErrorEncoding =
                    Encoding.UTF8
            };

        startInfo.ArgumentList.Add(
            "-jar");

        startInfo.ArgumentList.Add(
            request.EpubCheckJarPath);

        startInfo.ArgumentList.Add(
            request.EpubPath);

        startInfo.ArgumentList.Add(
            "--failonwarnings");

        startInfo.ArgumentList.Add(
            "--json");

        startInfo.ArgumentList.Add(
            request.ReportPath);

        startInfo.ArgumentList.Add(
            "--quiet");

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        try
        {
            if (!process.Start())
            {
                return new EpubCheckProcessResult(
                    EpubCheckProcessOutcome.Unavailable);
            }
        }
        catch (Exception exception)
            when (exception is Win32Exception or
                  FileNotFoundException or
                  DirectoryNotFoundException)
        {
            return new EpubCheckProcessResult(
                EpubCheckProcessOutcome.Unavailable,
                Exception:
                    exception);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or
                  IOException or
                  UnauthorizedAccessException)
        {
            return new EpubCheckProcessResult(
                EpubCheckProcessOutcome.Failed,
                Exception:
                    exception);
        }

        var stdoutTask =
            DrainAsync(
                process.StandardOutput,
                MaximumDiagnosticCharacters);

        var stderrTask =
            DrainAsync(
                process.StandardError,
                MaximumDiagnosticCharacters);

        using var timeoutSource =
            new CancellationTokenSource(
                request.Timeout);

        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        try
        {
            await process
                .WaitForExitAsync(
                    linkedSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(
                process);

            var stdout =
                await CompleteDrainAsync(
                        stdoutTask)
                    .ConfigureAwait(false);

            var stderr =
                await CompleteDrainAsync(
                        stderrTask)
                    .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new EpubCheckProcessResult(
                EpubCheckProcessOutcome.TimedOut,
                StandardOutput:
                    stdout,
                StandardError:
                    stderr);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or
                  IOException)
        {
            TryKill(
                process);

            return new EpubCheckProcessResult(
                EpubCheckProcessOutcome.Failed,
                StandardOutput:
                    await CompleteDrainAsync(
                            stdoutTask)
                        .ConfigureAwait(false),
                StandardError:
                    await CompleteDrainAsync(
                            stderrTask)
                        .ConfigureAwait(false),
                Exception:
                    exception);
        }

        return new EpubCheckProcessResult(
            EpubCheckProcessOutcome.Completed,
            process.ExitCode,
            await CompleteDrainAsync(
                    stdoutTask)
                .ConfigureAwait(false),
            await CompleteDrainAsync(
                    stderrTask)
                .ConfigureAwait(false));
    }

    #endregion

    #region Methods Output

    private static async Task<string> DrainAsync(
        StreamReader reader,
        int captureLimit)
    {
        var captured =
            new StringBuilder(
                Math.Min(
                    captureLimit,
                    4096));

        var buffer =
            new char[4096];

        try
        {
            while (true)
            {
                var read =
                    await reader
                        .ReadAsync(
                            buffer.AsMemory())
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    break;
                }

                if (captured.Length >=
                    captureLimit)
                {
                    continue;
                }

                captured.Append(
                    buffer,
                    0,
                    Math.Min(
                        read,
                        captureLimit -
                        captured.Length));
            }
        }
        catch (Exception exception)
            when (exception is IOException or
                  ObjectDisposedException)
        {
        }

        return captured.ToString();
    }

    private static async Task<string> CompleteDrainAsync(
        Task<string> drainTask)
    {
        try
        {
            return await drainTask
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is IOException or
                  ObjectDisposedException)
        {
            return string.Empty;
        }
    }

    #endregion

    #region Methods Termination

    private static void TryKill(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree:
                        true);

                process.WaitForExit(
                    milliseconds:
                        5000);
            }
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or
                  Win32Exception)
        {
        }
    }

    #endregion
}
