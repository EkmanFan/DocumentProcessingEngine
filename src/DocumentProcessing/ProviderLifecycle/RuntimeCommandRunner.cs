using System.Diagnostics;

namespace DocumentProcessing.ProviderLifecycle;

internal interface IRuntimeCommandRunner
{
    ValueTask<RuntimeCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed record RuntimeCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed class RuntimeCommandRunner
    : IRuntimeCommandRunner
{
    #region Methods

    public async ValueTask<RuntimeCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            executable);

        ArgumentNullException.ThrowIfNull(
            arguments);

        if (timeout <=
                TimeSpan.Zero ||
            timeout ==
                Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout));
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    executable,
                RedirectStandardOutput =
                    true,
                RedirectStandardError =
                    true,
                UseShellExecute =
                    false,
                CreateNoWindow =
                    true
            };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

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
                throw new InvalidOperationException(
                    $"Could not start trusted runtime command '{executable}'.");
            }
        }
        catch (Exception exception)
            when (exception is not
                  InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Could not start trusted runtime command '{executable}'.",
                exception);
        }

        var standardOutput =
            process.StandardOutput
                .ReadToEndAsync(
                    cancellationToken);

        var standardError =
            process.StandardError
                .ReadToEndAsync(
                    cancellationToken);

        using var timeoutSource =
            new CancellationTokenSource(
                timeout);

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
        catch (OperationCanceledException exception)
        {
            TryKill(
                process);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException(
                $"Trusted runtime command '{executable}' exceeded {timeout}.",
                exception);
        }

        return new RuntimeCommandResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

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
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    #endregion
}
