using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerStateMachineTests
{
    #region Tests State

    [Fact]
    public void Apply_ImplementsApprovedLifecycle()
    {
        var stateMachine =
            new ManagerStateMachine();

        var started =
            stateMachine.Apply(
                ManagerOperatingState.Stopped,
                new StartManagerCommand());

        var paused =
            stateMachine.Apply(
                started.CurrentState,
                new PauseManagerCommand());

        var resumed =
            stateMachine.Apply(
                paused.CurrentState,
                new ResumeManagerCommand());

        var stopped =
            stateMachine.Apply(
                resumed.CurrentState,
                new StopManagerCommand());

        Assert.Equal(
            ManagerOperatingState.Running,
            started.CurrentState);

        Assert.Equal(
            ManagerOperatingState.Paused,
            paused.CurrentState);

        Assert.Equal(
            ManagerOperatingState.Running,
            resumed.CurrentState);

        Assert.Equal(
            ManagerOperatingState.Stopped,
            stopped.CurrentState);
    }

    [Theory]
    [MemberData(
        nameof(IdempotentCommands))]
    public void Apply_IsIdempotentForRepeatedTargetCommand(
        ManagerOperatingState state,
        ManagerControlCommand command)
    {
        var transition =
            new ManagerStateMachine()
                .Apply(
                    state,
                    command);

        Assert.False(
            transition.Changed);

        Assert.Equal(
            state,
            transition.CurrentState);
    }

    [Theory]
    [MemberData(
        nameof(InvalidCommands))]
    public void Apply_RejectsCommandsWithDifferentSemantics(
        ManagerOperatingState state,
        ManagerControlCommand command)
    {
        var exception =
            Assert.Throws<InvalidManagerStateTransitionException>(
                () =>
                    new ManagerStateMachine()
                        .Apply(
                            state,
                            command));

        Assert.Equal(
            state,
            exception.State);

        Assert.Equal(
            command.GetType(),
            exception.CommandType);
    }

    #endregion

    #region Tests Control Service

    [Fact]
    public async Task ExecuteAsync_RetriesOptimisticConcurrencyConflict()
    {
        var stateStore =
            new ConflictOnceManagerStateStore();

        var result =
            await new ManagerControlService(
                    stateStore)
                .ExecuteAsync(
                    new StartManagerCommand());

        Assert.Equal(
            2,
            stateStore.TrySetCallCount);

        Assert.Equal(
            ManagerOperatingState.Running,
            result.Snapshot.State);

        Assert.Equal(
            1,
            result.Snapshot.Version);
    }

    [Fact]
    public async Task ExecuteAsync_LinearizesIdempotentCommand()
    {
        var stateStore =
            new ConflictOnceManagerStateStore(
                new ManagerStateSnapshot(
                    ManagerOperatingState.Running,
                    version:
                        4));

        var result =
            await new ManagerControlService(
                    stateStore)
                .ExecuteAsync(
                    new StartManagerCommand());

        Assert.Equal(
            1,
            stateStore.TrySetCallCount);

        Assert.False(
            result.Transition.Changed);

        Assert.Equal(
            5,
            result.Snapshot.Version);
    }

    #endregion

    #region Tests Contracts

    [Fact]
    public void PageRange_RejectsReversedPages()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        8,
                    endPhysicalPageNumber:
                        7,
                    title:
                        "Chapter"));
    }

    [Fact]
    public void ReorderCommand_SnapshotsOrderAndRejectsDuplicates()
    {
        var first =
            ProcessingUnitId.New();

        var second =
            ProcessingUnitId.New();

        var source =
            new List<ProcessingUnitId>
            {
                first,
                second
            };

        var command =
            new ReorderProcessingQueueCommand(
                source,
                expectedQueueVersion:
                    3);

        source.Reverse();

        Assert.Equal(
            [
                first,
                second
            ],
            command.OrderedPendingUnitIds);

        Assert.Throws<ArgumentException>(
            () =>
                new ReorderProcessingQueueCommand(
                    [
                        first,
                        first
                    ],
                    expectedQueueVersion:
                        3));
    }

    #endregion

    #region Test Data

    public static TheoryData<ManagerOperatingState, ManagerControlCommand>
        IdempotentCommands =>
        new()
        {
            {
                ManagerOperatingState.Stopped,
                new StopManagerCommand()
            },
            {
                ManagerOperatingState.Running,
                new StartManagerCommand()
            },
            {
                ManagerOperatingState.Running,
                new ResumeManagerCommand()
            },
            {
                ManagerOperatingState.Paused,
                new PauseManagerCommand()
            }
        };

    public static TheoryData<ManagerOperatingState, ManagerControlCommand>
        InvalidCommands =>
        new()
        {
            {
                ManagerOperatingState.Stopped,
                new PauseManagerCommand()
            },
            {
                ManagerOperatingState.Stopped,
                new ResumeManagerCommand()
            },
            {
                ManagerOperatingState.Paused,
                new StartManagerCommand()
            }
        };

    #endregion

    #region Test Doubles

    private sealed class ConflictOnceManagerStateStore
        : IManagerStateStore
    {
        private ManagerStateSnapshot _snapshot;

        public ConflictOnceManagerStateStore(
            ManagerStateSnapshot? snapshot = null)
        {
            _snapshot =
                snapshot ??
                new ManagerStateSnapshot(
                    ManagerOperatingState.Stopped,
                    version:
                        0);
        }

        public int TrySetCallCount
        {
            get;
            private set;
        }

        public ValueTask<ManagerStateSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                _snapshot);
        }

        public ValueTask<ManagerStateSnapshot?> TrySetAsync(
            long expectedVersion,
            ManagerOperatingState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TrySetCallCount++;

            if (TrySetCallCount ==
                    1 &&
                _snapshot.Version ==
                    0)
            {
                return ValueTask.FromResult<ManagerStateSnapshot?>(
                    null);
            }

            if (_snapshot.Version !=
                expectedVersion)
            {
                return ValueTask.FromResult<ManagerStateSnapshot?>(
                    null);
            }

            _snapshot =
                new ManagerStateSnapshot(
                    state,
                    _snapshot.Version +
                    1);

            return ValueTask.FromResult<ManagerStateSnapshot?>(
                _snapshot);
        }
    }

    #endregion
}
