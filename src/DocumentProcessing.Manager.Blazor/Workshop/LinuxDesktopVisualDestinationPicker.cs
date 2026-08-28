using System.Diagnostics;

namespace DocumentProcessing.Manager.Blazor.Workshop;

internal sealed class LinuxDesktopVisualDestinationPicker
    : IManagerVisualDestinationPicker
{
    #region Methods

    public async ValueTask<string?> PickAsync(
        string? currentDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "The standalone directory chooser currently supports Linux. Paste an absolute path instead.");
        }

        var command =
            ResolveCommand(
                currentDirectory);

        if (command is null)
        {
            throw new InvalidOperationException(
                "No supported Linux directory chooser was found. Install kdialog or zenity, or paste an absolute path.");
        }

        using var process =
            new Process
            {
                StartInfo =
                    command
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The directory chooser could not be started.");
        }

        var standardOutput =
            process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        var standardError =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        await process
            .WaitForExitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        var output =
            (await standardOutput.ConfigureAwait(false)).Trim();

        var error =
            (await standardError.ConfigureAwait(false)).Trim();

        if (process.ExitCode == 0)
        {
            return string.IsNullOrWhiteSpace(
                output)
                ? null
                : output;
        }

        if (process.ExitCode == 1 &&
            string.IsNullOrWhiteSpace(
                error))
        {
            return null;
        }

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(
                error)
                ? "The directory chooser failed."
                : error);
    }

    private static ProcessStartInfo? ResolveCommand(
        string? currentDirectory)
    {
        if (File.Exists(
                "/usr/bin/kdialog"))
        {
            var command =
                CreateCommand(
                    "/usr/bin/kdialog");

            command.ArgumentList.Add(
                "--getexistingdirectory");

            command.ArgumentList.Add(
                NormalizeStartDirectory(
                    currentDirectory));

            return command;
        }

        if (File.Exists(
                "/usr/bin/zenity"))
        {
            var command =
                CreateCommand(
                    "/usr/bin/zenity");

            command.ArgumentList.Add(
                "--file-selection");

            command.ArgumentList.Add(
                "--directory");

            command.ArgumentList.Add(
                $"--filename={NormalizeStartDirectory(currentDirectory)}{Path.DirectorySeparatorChar}");

            return command;
        }

        return null;
    }

    private static ProcessStartInfo CreateCommand(
        string executable) =>
        new()
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

    private static string NormalizeStartDirectory(
        string? currentDirectory) =>
        !string.IsNullOrWhiteSpace(
            currentDirectory) &&
        Path.IsPathFullyQualified(
            currentDirectory) &&
        Directory.Exists(
            currentDirectory)
            ? Path.GetFullPath(
                currentDirectory)
            : Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

    #endregion
}
