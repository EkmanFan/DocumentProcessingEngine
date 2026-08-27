using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;

namespace DocumentProcessing.Manager.Runtime;

/// <summary>
/// Coordinates durable lifecycle commands and globally sequential dispatch.
/// </summary>
/// <remarks>
/// This application service is host-agnostic. A hosting adapter is responsible
/// only for invoking <see cref="RunAsync"/> and forwarding control commands.
/// </remarks>
public sealed class DocumentProcessingManagerRuntime
{
    #region Variables and Constants

    private readonly IManagerStateStore
        _stateStore;

    private readonly IManagerRuntimeLeaseStore
        _runtimeLeaseStore;

    private readonly SequentialProcessingDispatcher
        _dispatcher;

    private readonly DocumentProcessingManagerRuntimeOptions
        _options;

    private readonly ManagerControlService
        _controlService;

    private readonly TimeProvider
        _timeProvider;

    private readonly SemaphoreSlim
        _lifecycleGate =
            new(
                initialCount:
                    1,
                maxCount:
                    1);

    private readonly SemaphoreSlim
        _wakeSignal =
            new(
                initialCount:
                    0,
                maxCount:
                    1);

    private ActiveDispatch?
        _activeDispatch;

    private int
        _runStarted;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the durable globally sequential Manager runtime.
    /// </summary>
    public DocumentProcessingManagerRuntime(
        IManagerStateStore stateStore,
        IManagerRuntimeLeaseStore runtimeLeaseStore,
        SequentialProcessingDispatcher dispatcher,
        DocumentProcessingManagerRuntimeOptions options,
        TimeProvider? timeProvider = null)
    {
        _stateStore =
            stateStore ??
            throw new ArgumentNullException(
                nameof(stateStore));

        _runtimeLeaseStore =
            runtimeLeaseStore ??
            throw new ArgumentNullException(
                nameof(runtimeLeaseStore));

        _dispatcher =
            dispatcher ??
            throw new ArgumentNullException(
                nameof(dispatcher));

        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        _controlService =
            new ManagerControlService(
                _stateStore);

        _timeProvider =
            timeProvider ??
            TimeProvider.System;
    }

    #endregion

    #region Methods Control

    /// <summary>
    /// Applies one durable lifecycle command to this Manager runtime.
    /// </summary>
    /// <remarks>
    /// Pause lets the active unit finish. Stop cooperatively cancels the active
    /// unit and returns only after the dispatcher has attempted to requeue it.
    /// </remarks>
    public async ValueTask<ManagerControlResult> ExecuteAsync(
        ManagerControlCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        ActiveDispatch? interrupted =
            null;

        ManagerControlResult result;

        await _lifecycleGate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            result =
                await _controlService
                    .ExecuteAsync(
                        command,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (command is StopManagerCommand)
            {
                interrupted =
                    _activeDispatch;

                interrupted?.Cancel(
                    ProcessingInterruptionReason.ManagerStop);
            }

            Signal();
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (interrupted is not null)
        {
            await interrupted.Completion
                .ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Wakes the runtime after work is added or reordered locally.
    /// </summary>
    public void NotifyQueueChanged() =>
        Signal();

    #endregion

    #region Methods Runtime

    /// <summary>
    /// Runs until the process host requests shutdown.
    /// </summary>
    public async Task RunAsync(
        CancellationToken hostStoppingToken = default)
    {
        if (Interlocked.Exchange(
                ref _runStarted,
                1) ==
            1)
        {
            throw new InvalidOperationException(
                "The Manager runtime can only be started once.");
        }

        try
        {
            while (true)
            {
                hostStoppingToken.ThrowIfCancellationRequested();

                var state =
                    await _stateStore
                        .GetAsync(
                            hostStoppingToken)
                        .ConfigureAwait(false);

                if (state.State !=
                    ManagerOperatingState.Running)
                {
                    await WaitForSignalAsync(
                            hostStoppingToken)
                        .ConfigureAwait(false);

                    continue;
                }

                var lease =
                    await _runtimeLeaseStore
                        .TryAcquireAsync(
                            _options.WorkerId,
                            _timeProvider.GetUtcNow() +
                            _options.RuntimeLeaseDuration,
                            hostStoppingToken)
                        .ConfigureAwait(false);

                if (lease is null)
                {
                    await WaitForSignalAsync(
                            hostStoppingToken)
                        .ConfigureAwait(false);

                    continue;
                }

                if (!string.Equals(
                        lease.WorkerId,
                        _options.WorkerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The durable store returned a runtime lease owned by a different worker.");
                }

                await RunAsOwnerAsync(
                        lease,
                        hostStoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (hostStoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunAsOwnerAsync(
        ManagerRuntimeLease lease,
        CancellationToken hostStoppingToken)
    {
        using var ownership =
            new RuntimeOwnership(
                hostStoppingToken);

        using var heartbeatCancellation =
            new CancellationTokenSource();

        var heartbeat =
            RenewRuntimeLeaseAsync(
                lease,
                ownership,
                heartbeatCancellation.Token);

        try
        {
            await _dispatcher
                .RecoverExpiredLeasesAsync(
                    ownership.Token)
                .ConfigureAwait(false);

            while (!ownership.Token.IsCancellationRequested)
            {
                var active =
                    await TryStartDispatchAsync(
                            ownership)
                        .ConfigureAwait(false);

                if (active is null)
                {
                    return;
                }

                ProcessingDispatchOutcome outcome;

                try
                {
                    outcome =
                        await _dispatcher
                            .DispatchNextAsync(
                                lease,
                                () =>
                                    active.InterruptionReason,
                                active.Token)
                            .ConfigureAwait(false);
                }
                finally
                {
                    await CompleteDispatchAsync(
                            active)
                        .ConfigureAwait(false);
                }

                if (ownership.Token.IsCancellationRequested)
                {
                    return;
                }

                if (outcome.Status ==
                    ProcessingDispatchStatus.QueueEmpty)
                {
                    await WaitForSignalAsync(
                            ownership.Token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
            when (ownership.Token.IsCancellationRequested)
        {
        }
        finally
        {
            heartbeatCancellation.Cancel();

            await heartbeat.ConfigureAwait(false);

            await _runtimeLeaseStore
                .ReleaseAsync(
                    lease,
                    _timeProvider.GetUtcNow(),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<ActiveDispatch?> TryStartDispatchAsync(
        RuntimeOwnership ownership)
    {
        await _lifecycleGate
            .WaitAsync(
                ownership.Token)
            .ConfigureAwait(false);

        try
        {
            var state =
                await _stateStore
                    .GetAsync(
                        ownership.Token)
                    .ConfigureAwait(false);

            if (state.State !=
                ManagerOperatingState.Running)
            {
                return null;
            }

            var active =
                new ActiveDispatch(
                    ownership);

            _activeDispatch =
                active;

            return active;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task CompleteDispatchAsync(
        ActiveDispatch active)
    {
        await _lifecycleGate
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (ReferenceEquals(
                    _activeDispatch,
                    active))
            {
                _activeDispatch =
                    null;
            }

            active.Complete();
        }
        finally
        {
            _lifecycleGate.Release();
            active.Dispose();
        }
    }

    #endregion

    #region Methods Lease

    private async Task RenewRuntimeLeaseAsync(
        ManagerRuntimeLease lease,
        RuntimeOwnership ownership,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(
                        _options.RuntimeLeaseRenewalInterval,
                        _timeProvider,
                        cancellationToken)
                    .ConfigureAwait(false);

                var renewed =
                    await _runtimeLeaseStore
                        .RenewAsync(
                            lease,
                            _timeProvider.GetUtcNow() +
                            _options.RuntimeLeaseDuration,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (renewed)
                {
                    continue;
                }

                ownership.Cancel(
                    ProcessingInterruptionReason.RuntimeLeaseLost);

                return;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            ownership.Cancel(
                ProcessingInterruptionReason.RuntimeLeaseLost);
        }
    }

    #endregion

    #region Methods Signal

    private async Task WaitForSignalAsync(
        CancellationToken cancellationToken)
    {
        await _wakeSignal
            .WaitAsync(
                _options.IdlePollingInterval,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void Signal()
    {
        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    #endregion

    #region Internal Types

    private sealed class RuntimeOwnership
        : IDisposable
    {
        private readonly CancellationTokenSource
            _cancellation;

        private int
            _interruptionReason =
                (int)ProcessingInterruptionReason.HostShutdown;

        public RuntimeOwnership(
            CancellationToken hostStoppingToken)
        {
            _cancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    hostStoppingToken);
        }

        public CancellationToken Token =>
            _cancellation.Token;

        public ProcessingInterruptionReason InterruptionReason =>
            (ProcessingInterruptionReason)Volatile.Read(
                ref _interruptionReason);

        public void Cancel(
            ProcessingInterruptionReason reason)
        {
            Volatile.Write(
                ref _interruptionReason,
                (int)reason);

            _cancellation.Cancel();
        }

        public void Dispose() =>
            _cancellation.Dispose();
    }

    private sealed class ActiveDispatch
        : IDisposable
    {
        private readonly RuntimeOwnership
            _ownership;

        private readonly CancellationTokenSource
            _cancellation;

        private readonly TaskCompletionSource
            _completion =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private int
            _interruptionReasonOverride =
                -1;

        public ActiveDispatch(
            RuntimeOwnership ownership)
        {
            _ownership =
                ownership;

            _cancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    ownership.Token);
        }

        public CancellationToken Token =>
            _cancellation.Token;

        public ProcessingInterruptionReason InterruptionReason
        {
            get
            {
                var overridden =
                    Volatile.Read(
                        ref _interruptionReasonOverride);

                return overridden >=
                       0
                    ? (ProcessingInterruptionReason)overridden
                    : _ownership.InterruptionReason;
            }
        }

        public Task Completion =>
            _completion.Task;

        public void Cancel(
            ProcessingInterruptionReason reason)
        {
            Volatile.Write(
                ref _interruptionReasonOverride,
                (int)reason);

            _cancellation.Cancel();
        }

        public void Complete() =>
            _completion.TrySetResult();

        public void Dispose() =>
            _cancellation.Dispose();
    }

    #endregion
}
