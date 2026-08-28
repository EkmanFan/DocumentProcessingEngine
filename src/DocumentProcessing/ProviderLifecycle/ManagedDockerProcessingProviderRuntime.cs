using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.ProviderLifecycle;

internal sealed class ManagedDockerProcessingProviderRuntime
    : IProcessingProviderRuntime
{
    #region Variables and Constants

    private const int
        MaximumDiagnosticCharacters =
            4000;

    private readonly ManagedDockerProcessingProviderOptions
        _options;

    private readonly ProviderState
        _layout;

    private readonly ProviderState
        _ocr;

    private readonly IRuntimeCommandRunner
        _commandRunner;

    private readonly IProviderEndpointProbe
        _endpointProbe;

    private readonly IAvailableMemoryReader
        _memoryReader;

    private readonly ILogger?
        _logger;

    private readonly SemaphoreSlim
        _gate =
            new(
                1,
                1);

    private readonly HashSet<string>
        _verifiedImages =
            new(
                StringComparer.Ordinal);

    private bool
        _dockerVerified;

    private bool
        _memoryVerified;

    private bool
        _disposed;

    #endregion

    #region ctor

    public ManagedDockerProcessingProviderRuntime(
        ManagedDockerProcessingProviderOptions options,
        Uri layoutEndpoint,
        Uri ocrEndpoint,
        ILoggerFactory? loggerFactory)
        : this(
            options,
            layoutEndpoint,
            ocrEndpoint,
            new RuntimeCommandRunner(),
            new ProviderEndpointProbe(),
            new AvailableMemoryReader(),
            loggerFactory)
    {
    }

    internal ManagedDockerProcessingProviderRuntime(
        ManagedDockerProcessingProviderOptions options,
        Uri layoutEndpoint,
        Uri ocrEndpoint,
        IRuntimeCommandRunner commandRunner,
        IProviderEndpointProbe endpointProbe,
        IAvailableMemoryReader memoryReader,
        ILoggerFactory? loggerFactory = null)
    {
        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        _commandRunner =
            commandRunner ??
            throw new ArgumentNullException(
                nameof(commandRunner));

        _endpointProbe =
            endpointProbe ??
            throw new ArgumentNullException(
                nameof(endpointProbe));

        _memoryReader =
            memoryReader ??
            throw new ArgumentNullException(
                nameof(memoryReader));

        ValidateManagedEndpoint(
            layoutEndpoint,
            "/layout-parsing",
            nameof(layoutEndpoint));

        ValidateManagedEndpoint(
            ocrEndpoint,
            "/ocr",
            nameof(ocrEndpoint));

        if (layoutEndpoint.Port ==
            ocrEndpoint.Port)
        {
            throw new ArgumentException(
                "Managed Layout and OCR endpoints must use distinct ports.");
        }

        var instanceSuffix =
            $"{Environment.ProcessId}-{Guid.NewGuid():N}";

        _layout =
            new ProviderState(
                new ProviderDescriptor(
                    "PP-StructureV3",
                    layoutEndpoint,
                    options.LayoutBaseImage,
                    options.LayoutServingImage,
                    options.LayoutCacheVolume,
                    "tools/layout-benchmarks/ppstructurev3",
                    "scripts/tmp/model-cache/ppstructurev3-3.7.0-paddle3.2.2",
                    "PP-StructureV3",
                    $"dpengine-layout-{instanceSuffix}"));

        _ocr =
            new ProviderState(
                new ProviderDescriptor(
                    "PaddleOCR",
                    ocrEndpoint,
                    options.OcrBaseImage,
                    options.OcrServingImage,
                    options.OcrCacheVolume,
                    "tools/ocr-benchmarks/paddleocr",
                    "scripts/tmp/model-cache/paddleocr-3.7.0-paddle3.2.2",
                    "OCR",
                    $"dpengine-ocr-{instanceSuffix}"));

        _logger =
            loggerFactory?.CreateLogger(
                typeof(ManagedDockerProcessingProviderRuntime)
                    .FullName!);
    }

    #endregion

    #region Methods Lifecycle

    public async ValueTask EnsureAvailableAsync(
        ProcessingProviderCapability capability,
        CancellationToken cancellationToken)
    {
        var state =
            GetState(
                capability);

        if (Volatile.Read(
                ref state.Ready) ==
            1)
        {
            return;
        }

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (Volatile.Read(
                    ref state.Ready) ==
                1)
            {
                return;
            }

            if (await _endpointProbe
                    .IsReadyAsync(
                        state.Descriptor.Endpoint,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                Volatile.Write(
                    ref state.Ready,
                    1);

                return;
            }

            await EnsureOwnedProviderReadyAsync(
                    state,
                    cancellationToken)
                .ConfigureAwait(false);

            Volatile.Write(
                ref state.Ready,
                1);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ReportUnavailable(
        ProcessingProviderCapability capability)
    {
        var state =
            GetState(
                capability);

        Volatile.Write(
            ref state.Ready,
            0);
    }

    public void Dispose()
    {
        _gate.Wait();

        if (_disposed)
        {
            _gate.Release();
            return;
        }

        try
        {
            _disposed =
                true;

            StopOwnedProvider(
                _ocr);

            StopOwnedProvider(
                _layout);
        }
        finally
        {
            _gate.Release();
        }

        _endpointProbe.Dispose();
    }

    private async ValueTask EnsureOwnedProviderReadyAsync(
        ProviderState state,
        CancellationToken cancellationToken)
    {
        await EnsureDockerReadyAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await EnsureMemoryAvailableAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await EnsureServingImageAsync(
                state.Descriptor,
                cancellationToken)
            .ConfigureAwait(false);

        if (state.Owned &&
            !await IsContainerRunningAsync(
                    state.Descriptor.ContainerName,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            state.Owned =
                false;
        }

        if (!state.Owned)
        {
            await StartProviderAsync(
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await WaitForReadinessAsync(
                state,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void StopOwnedProvider(
        ProviderState state)
    {
        if (!state.Owned)
        {
            return;
        }

        try
        {
            var result =
                _commandRunner
                    .RunAsync(
                        _options.DockerExecutable,
                        [
                            "stop",
                            "--time",
                            "10",
                            state.Descriptor.ContainerName
                        ],
                        _options.CommandTimeout,
                        CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();

            if (result.ExitCode !=
                0)
            {
                _logger?.LogWarning(
                    "Could not stop owned {Provider} container {Container}: {Diagnostic}",
                    state.Descriptor.DisplayName,
                    state.Descriptor.ContainerName,
                    DescribeFailure(
                        result));
            }
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Could not stop owned {Provider} container {Container}.",
                state.Descriptor.DisplayName,
                state.Descriptor.ContainerName);
        }
        finally
        {
            state.Owned =
                false;

            Volatile.Write(
                ref state.Ready,
                0);
        }
    }

    #endregion

    #region Methods Docker

    private async ValueTask EnsureDockerReadyAsync(
        CancellationToken cancellationToken)
    {
        if (_dockerVerified)
        {
            return;
        }

        await RunRequiredDockerAsync(
                ["info"],
                _options.CommandTimeout,
                "Docker is unavailable to the DPEngine provider runtime",
                cancellationToken)
            .ConfigureAwait(false);

        _dockerVerified =
            true;
    }

    private ValueTask EnsureMemoryAvailableAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_memoryVerified)
        {
            return ValueTask.CompletedTask;
        }

        var availableBytes =
            _memoryReader.ReadAvailableBytes();

        if (availableBytes <
            _options.MinimumAvailableMemoryBytes)
        {
            throw new InvalidOperationException(
                $"DPEngine managed providers require at least " +
                $"{FormatGibibytes(_options.MinimumAvailableMemoryBytes)} GiB " +
                $"available memory before model startup; observed " +
                $"{FormatGibibytes(availableBytes)} GiB.");
        }

        _memoryVerified =
            true;

        return ValueTask.CompletedTask;
    }

    private async ValueTask EnsureServingImageAsync(
        ProviderDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (_verifiedImages.Contains(
                descriptor.ServingImage))
        {
            return;
        }

        if (await DockerCommandSucceedsAsync(
                [
                    "image",
                    "inspect",
                    descriptor.ServingImage
                ],
                cancellationToken)
            .ConfigureAwait(false))
        {
            _verifiedImages.Add(
                descriptor.ServingImage);

            return;
        }

        await BuildServingImageAsync(
                descriptor,
                cancellationToken)
            .ConfigureAwait(false);

        _verifiedImages.Add(
            descriptor.ServingImage);
    }

    private async ValueTask BuildServingImageAsync(
        ProviderDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var repositoryRoot =
            ResolveRepositoryRoot();

        var baseBuildContext =
            Path.Combine(
                repositoryRoot,
                descriptor.BaseBuildContext.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

        if (!Directory.Exists(
                baseBuildContext))
        {
            throw new DirectoryNotFoundException(
                $"Pinned {descriptor.DisplayName} build context was not found: " +
                baseBuildContext);
        }

        if (!await DockerCommandSucceedsAsync(
                [
                    "image",
                    "inspect",
                    descriptor.BaseImage
                ],
                cancellationToken)
            .ConfigureAwait(false))
        {
            await RunRequiredDockerAsync(
                    [
                        "build",
                        "--tag",
                        descriptor.BaseImage,
                        baseBuildContext
                    ],
                    _options.ImageBuildTimeout,
                    $"Could not build pinned {descriptor.DisplayName} base image",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var servingBuildContext =
            Path.Combine(
                Path.GetTempPath(),
                $"dpengine-provider-build-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            servingBuildContext);

        var dockerfile =
            Path.Combine(
                servingBuildContext,
                "Dockerfile");

        try
        {
            await File.WriteAllTextAsync(
                    dockerfile,
                    $"FROM {descriptor.BaseImage}\n" +
                    "RUN paddlex --install serving\n" +
                    "ENTRYPOINT [\"paddlex\"]\n",
                    cancellationToken)
                .ConfigureAwait(false);

            await RunRequiredDockerAsync(
                    [
                        "build",
                        "--tag",
                        descriptor.ServingImage,
                        "--file",
                        dockerfile,
                        servingBuildContext
                    ],
                    _options.ImageBuildTimeout,
                    $"Could not build pinned {descriptor.DisplayName} serving image",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            TryDeleteDirectory(
                servingBuildContext);
        }
    }

    private async ValueTask StartProviderAsync(
        ProviderState state,
        CancellationToken cancellationToken)
    {
        var descriptor =
            state.Descriptor;

        var cacheMount =
            ResolveCacheMount(
                descriptor);

        await RunRequiredDockerAsync(
                [
                    "run",
                    "--detach",
                    "--rm",
                    "--name",
                    descriptor.ContainerName,
                    "--memory",
                    _options.ModelMemoryLimit,
                    "--memory-swap",
                    _options.ModelMemoryLimit,
                    "--shm-size",
                    _options.SharedMemorySize,
                    "--publish",
                    $"127.0.0.1:{descriptor.Endpoint.Port}:8080",
                    "--volume",
                    cacheMount,
                    descriptor.ServingImage,
                    "--serve",
                    "--pipeline",
                    descriptor.Pipeline,
                    "--device",
                    "cpu",
                    "--host",
                    "0.0.0.0",
                    "--port",
                    "8080"
                ],
                _options.CommandTimeout,
                $"Could not start pinned {descriptor.DisplayName} provider",
                cancellationToken)
            .ConfigureAwait(false);

        state.Owned =
            true;

        _logger?.LogInformation(
            "DPEngine started owned {Provider} container {Container} for {Endpoint}.",
            descriptor.DisplayName,
            descriptor.ContainerName,
            descriptor.Endpoint);
    }

    private async ValueTask WaitForReadinessAsync(
        ProviderState state,
        CancellationToken cancellationToken)
    {
        var elapsed =
            Stopwatch.StartNew();

        while (elapsed.Elapsed <
               _options.StartupTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _endpointProbe
                    .IsReadyAsync(
                        state.Descriptor.Endpoint,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return;
            }

            if (!await IsContainerRunningAsync(
                    state.Descriptor.ContainerName,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                var logs =
                    await ReadContainerLogsAsync(
                            state.Descriptor.ContainerName,
                            cancellationToken)
                        .ConfigureAwait(false);

                state.Owned =
                    false;

                throw new InvalidOperationException(
                    $"Managed {state.Descriptor.DisplayName} stopped before readiness. " +
                    logs);
            }

            await Task
                .Delay(
                    _options.ReadinessPollingInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var timeoutLogs =
            await ReadContainerLogsAsync(
                    state.Descriptor.ContainerName,
                    cancellationToken)
                .ConfigureAwait(false);

        throw new TimeoutException(
            $"Managed {state.Descriptor.DisplayName} did not become ready " +
            $"within {_options.StartupTimeout}. {timeoutLogs}");
    }

    private async ValueTask<bool> IsContainerRunningAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        var result =
            await RunDockerAsync(
                    [
                        "inspect",
                        "--format",
                        "{{.State.Running}}",
                        containerName
                    ],
                    _options.CommandTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

        return result.ExitCode ==
                   0 &&
               string.Equals(
                   result.StandardOutput.Trim(),
                   "true",
                   StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<string> ReadContainerLogsAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        var result =
            await RunDockerAsync(
                    [
                        "logs",
                        "--tail",
                        "120",
                        containerName
                    ],
                    _options.CommandTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

        return DescribeFailure(
            result);
    }

    private async ValueTask<bool> DockerCommandSucceedsAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result =
            await RunDockerAsync(
                    arguments,
                    _options.CommandTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

        return result.ExitCode ==
            0;
    }

    private async ValueTask RunRequiredDockerAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var result =
            await RunDockerAsync(
                    arguments,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

        if (result.ExitCode !=
            0)
        {
            throw new InvalidOperationException(
                failureMessage +
                ": " +
                DescribeFailure(
                    result));
        }
    }

    private ValueTask<RuntimeCommandResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _commandRunner.RunAsync(
            _options.DockerExecutable,
            arguments,
            timeout,
            cancellationToken);

    #endregion

    #region Methods Helpers

    private ProviderState GetState(
        ProcessingProviderCapability capability) =>
        capability switch
        {
            ProcessingProviderCapability.Layout =>
                _layout,
            ProcessingProviderCapability.Ocr =>
                _ocr,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(capability),
                    capability,
                    "Unknown processing-provider capability.")
        };

    private string ResolveRepositoryRoot()
    {
        var repositoryRoot =
            TryResolveRepositoryRoot();

        if (repositoryRoot is not null)
        {
            return repositoryRoot;
        }

        throw new DirectoryNotFoundException(
            "Pinned provider image is missing and the DPEngine repository root " +
            "could not be located. Prebuild the image or configure an absolute " +
            "ManagedDockerProcessingProviderOptions.RepositoryRoot.");
    }

    private string ResolveCacheMount(
        ProviderDescriptor descriptor)
    {
        var repositoryRoot =
            TryResolveRepositoryRoot();

        if (repositoryRoot is not null)
        {
            var hostCache =
                Path.Combine(
                    repositoryRoot,
                    descriptor.RepositoryCachePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));

            Directory.CreateDirectory(
                hostCache);

            return $"{hostCache}:/root/.paddlex:Z";
        }

        return $"{descriptor.CacheVolume}:/root/.paddlex";
    }

    private string? TryResolveRepositoryRoot()
    {
        if (_options.RepositoryRoot is not null)
        {
            return _options.RepositoryRoot;
        }

        foreach (var startPath in new[]
                 {
                     AppContext.BaseDirectory,
                     Directory.GetCurrentDirectory()
                 })
        {
            var current =
                new DirectoryInfo(
                    startPath);

            while (current is not null)
            {
                if (Directory.Exists(
                        Path.Combine(
                            current.FullName,
                            "tools",
                            "layout-benchmarks",
                            "ppstructurev3")) &&
                    Directory.Exists(
                        Path.Combine(
                            current.FullName,
                            "tools",
                            "ocr-benchmarks",
                            "paddleocr")))
                {
                    return current.FullName;
                }

                current =
                    current.Parent;
            }
        }

        return null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private static void ValidateManagedEndpoint(
        Uri endpoint,
        string expectedPath,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            endpoint);

        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme !=
                Uri.UriSchemeHttp ||
            !endpoint.IsLoopback ||
            !string.Equals(
                endpoint.AbsolutePath,
                expectedPath,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Managed Docker provider endpoint must be loopback HTTP with " +
                $"path '{expectedPath}'.",
                parameterName);
        }
    }

    private static string DescribeFailure(
        RuntimeCommandResult result)
    {
        var diagnostic =
            string.IsNullOrWhiteSpace(
                result.StandardError)
                ? result.StandardOutput
                : result.StandardError;

        diagnostic =
            diagnostic.Trim();

        if (diagnostic.Length >
            MaximumDiagnosticCharacters)
        {
            diagnostic =
                diagnostic[^MaximumDiagnosticCharacters..];
        }

        return string.IsNullOrWhiteSpace(
            diagnostic)
            ? $"Docker exited with code {result.ExitCode}."
            : diagnostic;
    }

    private static string FormatGibibytes(
        long bytes) =>
        (bytes /
         (1024d *
          1024d *
          1024d))
        .ToString(
            "0.##",
            System.Globalization.CultureInfo.InvariantCulture);

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            Directory.Delete(
                path,
                recursive:
                    true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    #endregion

    #region Nested Types

    private sealed class ProviderState(
        ProviderDescriptor descriptor)
    {
        public ProviderDescriptor Descriptor { get; } =
            descriptor;

        public int Ready;

        public bool Owned;
    }

    private sealed record ProviderDescriptor(
        string DisplayName,
        Uri Endpoint,
        string BaseImage,
        string ServingImage,
        string CacheVolume,
        string BaseBuildContext,
        string RepositoryCachePath,
        string Pipeline,
        string ContainerName);

    #endregion
}
