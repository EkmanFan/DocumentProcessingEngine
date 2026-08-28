using DocumentProcessing.ProviderLifecycle;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class ManagedDockerProcessingProviderRuntimeTests
{
    #region Tests

    [Fact]
    public async Task EnsureAvailableAsync_StartsOnlyRequestedProviderAndReusesIt()
    {
        var runner =
            new RecordingCommandRunner();

        var probe =
            new SequencedEndpointProbe(
                new Dictionary<int, bool[]>
                {
                    [8080] =
                    [false, true]
                });

        var memory =
            new FixedMemoryReader(
                32L *
                1024 *
                1024 *
                1024);

        var runtime =
            CreateRuntime(
                runner,
                probe,
                memory);

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Layout,
            CancellationToken.None);

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Layout,
            CancellationToken.None);

        Assert.Single(
            runner.Commands,
            command =>
                command.Arguments.FirstOrDefault() ==
                "run");

        Assert.Contains(
            runner.Commands,
            command =>
                command.Arguments.Contains(
                    "127.0.0.1:8080:8080") &&
                command.Arguments.Contains(
                    "PP-StructureV3"));

        Assert.DoesNotContain(
            runner.Commands,
            command =>
                command.Arguments.Contains(
                    "127.0.0.1:8081:8080"));

        Assert.Equal(
            1,
            memory.CallCount);

        runtime.Dispose();
        runtime.Dispose();

        Assert.Single(
            runner.Commands,
            command =>
                command.Arguments.FirstOrDefault() ==
                "stop");

        Assert.Equal(
            1,
            probe.DisposeCount);
    }

    [Fact]
    public async Task EnsureAvailableAsync_ReusesHealthyExternalEndpointWithoutDocker()
    {
        var runner =
            new RecordingCommandRunner();

        var runtime =
            CreateRuntime(
                runner,
                new SequencedEndpointProbe(
                    new Dictionary<int, bool[]>
                    {
                        [8080] =
                        [true]
                    }),
                new FixedMemoryReader(
                    0));

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Layout,
            CancellationToken.None);

        runtime.Dispose();

        Assert.Empty(
            runner.Commands);
    }

    [Fact]
    public async Task EnsureAvailableAsync_StartsLayoutAndOcrLazilyUnderOneRuntime()
    {
        var runner =
            new RecordingCommandRunner();

        var runtime =
            CreateRuntime(
                runner,
                new SequencedEndpointProbe(
                    new Dictionary<int, bool[]>
                    {
                        [8080] =
                        [false, true],
                        [8081] =
                        [false, true]
                    }),
                new FixedMemoryReader(
                    32L *
                    1024 *
                    1024 *
                    1024));

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Layout,
            CancellationToken.None);

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Ocr,
            CancellationToken.None);

        Assert.Equal(
            2,
            runner.Commands.Count(
                command =>
                    command.Arguments.FirstOrDefault() ==
                    "run"));

        Assert.Equal(
            1,
            runner.Commands.Count(
                command =>
                    command.Arguments.SequenceEqual(
                        ["info"])));

        runtime.Dispose();

        Assert.Equal(
            2,
            runner.Commands.Count(
                command =>
                    command.Arguments.FirstOrDefault() ==
                    "stop"));
    }

    [Fact]
    public async Task ReportUnavailable_RechecksOwnedProviderWithoutStartingDuplicate()
    {
        var runner =
            new RecordingCommandRunner();

        var runtime =
            CreateRuntime(
                runner,
                new SequencedEndpointProbe(
                    new Dictionary<int, bool[]>
                    {
                        [8080] =
                        [false, true, false, true]
                    }),
                new FixedMemoryReader(
                    32L *
                    1024 *
                    1024 *
                    1024));

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Layout,
            CancellationToken.None);

        runtime.ReportUnavailable(
            ProcessingProviderCapability.Layout);

        await runtime.EnsureAvailableAsync(
            ProcessingProviderCapability.Layout,
            CancellationToken.None);

        Assert.Single(
            runner.Commands,
            command =>
                command.Arguments.FirstOrDefault() ==
                "run");

        Assert.Contains(
            runner.Commands,
            command =>
                command.Arguments.FirstOrDefault() ==
                "inspect");

        runtime.Dispose();
    }

    [Fact]
    public async Task EnsureAvailableAsync_FailsBeforeImageOrContainerWhenMemoryIsUnsafe()
    {
        var runner =
            new RecordingCommandRunner();

        using var runtime =
            CreateRuntime(
                runner,
                new SequencedEndpointProbe(
                    new Dictionary<int, bool[]>
                    {
                        [8080] =
                        [false]
                    }),
                new FixedMemoryReader(
                    1));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await runtime.EnsureAvailableAsync(
                        ProcessingProviderCapability.Layout,
                        CancellationToken.None));

        Assert.Contains(
            "available memory",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Single(
            runner.Commands,
            command =>
                command.Arguments.SequenceEqual(
                    ["info"]));
    }

    [Fact]
    public async Task EnsureAvailableAsync_BuildsMissingPinnedImagesFromConfiguredRepository()
    {
        var repositoryRoot =
            Path.Combine(
                Path.GetTempPath(),
                $"dpengine-provider-runtime-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            Path.Combine(
                repositoryRoot,
                "tools",
                "layout-benchmarks",
                "ppstructurev3"));

        Directory.CreateDirectory(
            Path.Combine(
                repositoryRoot,
                "tools",
                "ocr-benchmarks",
                "paddleocr"));

        try
        {
            var runner =
                new RecordingCommandRunner(
                    imagesExist:
                        false);

            using var runtime =
                CreateRuntime(
                    runner,
                    new SequencedEndpointProbe(
                        new Dictionary<int, bool[]>
                        {
                            [8080] =
                            [false, true]
                        }),
                    new FixedMemoryReader(
                        32L *
                        1024 *
                        1024 *
                        1024),
                    repositoryRoot);

            await runtime.EnsureAvailableAsync(
                ProcessingProviderCapability.Layout,
                CancellationToken.None);

            Assert.Equal(
                2,
                runner.Commands.Count(
                    command =>
                        command.Arguments.FirstOrDefault() ==
                        "build"));
        }
        finally
        {
            Directory.Delete(
                repositoryRoot,
                recursive:
                    true);
        }
    }

    #endregion

    #region Fixtures

    private static ManagedDockerProcessingProviderRuntime CreateRuntime(
        IRuntimeCommandRunner runner,
        IProviderEndpointProbe probe,
        IAvailableMemoryReader memoryReader,
        string? repositoryRoot = null) =>
        new(
            new ManagedDockerProcessingProviderOptions(
                minimumAvailableMemoryBytes:
                    12L *
                    1024 *
                    1024 *
                    1024,
                startupTimeout:
                    TimeSpan.FromSeconds(
                        5),
                readinessPollingInterval:
                    TimeSpan.FromMilliseconds(
                        1),
                repositoryRoot:
                    repositoryRoot),
            new Uri(
                "http://127.0.0.1:8080/layout-parsing"),
            new Uri(
                "http://127.0.0.1:8081/ocr"),
            runner,
            probe,
            memoryReader);

    #endregion

    #region Test Doubles

    private sealed class RecordingCommandRunner(
        bool imagesExist = true)
        : IRuntimeCommandRunner
    {
        public List<RecordedCommand> Commands { get; } =
            [];

        public ValueTask<RuntimeCommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Commands.Add(
                new RecordedCommand(
                    executable,
                    arguments.ToArray()));

            if (arguments.Count >=
                    2 &&
                arguments[0] ==
                    "image" &&
                arguments[1] ==
                    "inspect" &&
                !imagesExist)
            {
                return ValueTask.FromResult(
                    new RuntimeCommandResult(
                        1,
                        string.Empty,
                        "missing"));
            }

            if (arguments.FirstOrDefault() ==
                "inspect")
            {
                return ValueTask.FromResult(
                    new RuntimeCommandResult(
                        0,
                        "true\n",
                        string.Empty));
            }

            return ValueTask.FromResult(
                new RuntimeCommandResult(
                    0,
                    "ok\n",
                    string.Empty));
        }
    }

    private sealed class SequencedEndpointProbe(
        IReadOnlyDictionary<int, bool[]> responses)
        : IProviderEndpointProbe
    {
        private readonly Dictionary<int, Queue<bool>>
            _responses =
                responses.ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        new Queue<bool>(
                            pair.Value));

        public int DisposeCount { get; private set; }

        public ValueTask<bool> IsReadyAsync(
            Uri processingEndpoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var queue =
                _responses[processingEndpoint.Port];

            return ValueTask.FromResult(
                queue.Count ==
                    1
                    ? queue.Peek()
                    : queue.Dequeue());
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class FixedMemoryReader(
        long availableBytes)
        : IAvailableMemoryReader
    {
        public int CallCount { get; private set; }

        public long ReadAvailableBytes()
        {
            CallCount++;

            return availableBytes;
        }
    }

    private sealed record RecordedCommand(
        string Executable,
        IReadOnlyList<string> Arguments);

    #endregion
}
