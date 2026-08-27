using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Persistence.Postgres;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.IntegrationTests.Manager;

public sealed class PostgresManagerPersistenceTests
{
    #region Variables and Constants

    internal const string
        ConnectionStringEnvironmentVariable =
            "DOCUMENT_PROCESSING_MANAGER_POSTGRES_CONNECTION_STRING";

    #endregion

    #region Tests

    [PostgresFact]
    public async Task SchemaAndStateStore_AreIdempotentAndVersioned()
    {
        await using var context =
            await CreateContextAsync();

        await context.Schema.InitializeAsync();

        var initial =
            await context.StateStore.GetAsync();

        Assert.Equal(
            ManagerOperatingState.Stopped,
            initial.State);

        Assert.Equal(
            0,
            initial.Version);

        var changed =
            await context.StateStore.TrySetAsync(
                expectedVersion:
                    0,
                ManagerOperatingState.Running);

        Assert.NotNull(
            changed);

        Assert.Equal(
            1,
            changed.Version);

        var stale =
            await context.StateStore.TrySetAsync(
                expectedVersion:
                    0,
                ManagerOperatingState.Paused);

        Assert.Null(
            stale);
    }

    [PostgresFact]
    public async Task RuntimeLeaseStore_AllowsOnlyCurrentFencedOwner()
    {
        await using var context =
            await CreateContextAsync();

        var observedAtUtc =
            DateTimeOffset.UnixEpoch;

        var first =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-one",
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5));

        Assert.NotNull(
            first);

        Assert.InRange(
            first.ExpiresAtUtc,
            DateTimeOffset.UtcNow.AddMinutes(
                4),
            DateTimeOffset.UtcNow.AddMinutes(
                6));

        var competing =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-two",
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5));

        Assert.Null(
            competing);

        var stale =
            new ManagerRuntimeLease(
                Guid.NewGuid(),
                first.WorkerId,
                first.ExpiresAtUtc);

        Assert.False(
            await context.RuntimeLeaseStore.RenewAsync(
                stale,
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5)));

        Assert.False(
            await context.RuntimeLeaseStore.ReleaseAsync(
                stale,
                observedAtUtc));

        Assert.True(
            await context.RuntimeLeaseStore.RenewAsync(
                first,
                observedAtUtc,
                observedAtUtc.AddMinutes(
                    5)));

        Assert.True(
            await context.RuntimeLeaseStore.ReleaseAsync(
                first,
                observedAtUtc));
    }

    [PostgresFact]
    public async Task RuntimeLeaseStore_ConcurrentAcquisitionHasSingleWinner()
    {
        await using var context =
            await CreateContextAsync();

        var now =
            DateTimeOffset.UtcNow;

        var acquisitions =
            Enumerable.Range(
                    1,
                    12)
                .Select(
                    workerNumber =>
                        context.RuntimeLeaseStore
                            .TryAcquireAsync(
                                $"worker-{workerNumber}",
                                now,
                                now.AddMinutes(
                                    5))
                            .AsTask())
                .ToArray();

        var results =
            await Task.WhenAll(
                acquisitions);

        Assert.Single(
            results,
            lease =>
                lease is not null);
    }

    [PostgresFact]
    public async Task QueueStore_FencesStaleOwnerAndRecoversExpiredUnit()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        var now =
            DateTimeOffset.UtcNow;

        var firstRuntime =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-one",
                now,
                now.AddMinutes(
                    5));

        Assert.NotNull(
            firstRuntime);

        var claimed =
            await context.QueueStore.ClaimNextAsync(
                firstRuntime,
                firstRuntime.WorkerId,
                now,
                now.AddMilliseconds(
                    100));

        Assert.NotNull(
            claimed);

        Assert.True(
            await context.RuntimeLeaseStore.ReleaseAsync(
                firstRuntime,
                now));

        var secondRuntime =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-two",
                now,
                now.AddMinutes(
                    5));

        Assert.NotNull(
            secondRuntime);

        Assert.False(
            await context.QueueStore.CompleteAsync(
                claimed,
                new ProcessingExecutionOutcome.Success(
                    "stale-result"),
                DateTimeOffset.UtcNow));

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                150));

        Assert.Equal(
            1,
            await context.QueueStore.RecoverExpiredLeasesAsync(
                DateTimeOffset.UtcNow));

        var recovered =
            await context.QueueStore.ClaimNextAsync(
                secondRuntime,
                secondRuntime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            recovered);

        Assert.Equal(
            workItem.UnitId,
            recovered.WorkItem.UnitId);

        Assert.True(
            await context.QueueStore.CompleteAsync(
                recovered,
                new ProcessingExecutionOutcome.Success(
                    "durable-result"),
                DateTimeOffset.UtcNow));
    }

    [PostgresFact]
    public async Task QueueStore_RetriesAndReordersWithOptimisticConcurrency()
    {
        await using var context =
            await CreateContextAsync();

        var first =
            CreateWorkItem();

        var second =
            CreateWorkItem(
                new ProcessingUnitScope.PageRange(
                    startPhysicalPageNumber:
                        10,
                    endPhysicalPageNumber:
                        20,
                    title:
                        "Chapter two"));

        await InsertPendingAsync(
            context.DataSource,
            first,
            queuePosition:
                1);

        await InsertPendingAsync(
            context.DataSource,
            second,
            queuePosition:
                2);

        await context.QueueStore.ReorderPendingAsync(
            new ReorderProcessingQueueCommand(
                [second.UnitId, first.UnitId],
                expectedQueueVersion:
                    0));

        Assert.Equal(
            [second.UnitId, first.UnitId],
            await ReadPendingOrderAsync(
                context.DataSource));

        var conflict =
            await Assert.ThrowsAsync<ProcessingQueueConcurrencyException>(
                () =>
                    context.QueueStore
                        .ReorderPendingAsync(
                            new ReorderProcessingQueueCommand(
                                [first.UnitId, second.UnitId],
                                expectedQueueVersion:
                                    0))
                        .AsTask());

        Assert.Equal(
            1,
            conflict.ActualVersion);

        var now =
            DateTimeOffset.UtcNow;

        var runtime =
            await context.RuntimeLeaseStore.TryAcquireAsync(
                "worker-one",
                now,
                now.AddMinutes(
                    5));

        Assert.NotNull(
            runtime);

        var claimed =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                now,
                now.AddMinutes(
                    1));

        Assert.NotNull(
            claimed);

        Assert.Equal(
            second.UnitId,
            claimed.WorkItem.UnitId);

        Assert.IsType<ProcessingUnitScope.PageRange>(
            claimed.WorkItem.Scope);

        Assert.True(
            await context.QueueStore.RequeueAfterFailureAsync(
                claimed,
                new ProcessingFailure(
                    "temporary",
                    "Temporary failure."),
                DateTimeOffset.UtcNow));

        var next =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            next);

        Assert.Equal(
            first.UnitId,
            next.WorkItem.UnitId);

        Assert.True(
            await context.QueueStore.InterruptAndRequeueAsync(
                next,
                ProcessingInterruptionReason.ManagerStop,
                DateTimeOffset.UtcNow));

        var interrupted =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            interrupted);

        Assert.Equal(
            first.UnitId,
            interrupted.WorkItem.UnitId);

        Assert.Equal(
            1,
            interrupted.WorkItem.AttemptNumber);

        Assert.True(
            await context.QueueStore.FailAsync(
                interrupted,
                new ProcessingFailure(
                    "terminal",
                    "Terminal failure."),
                DateTimeOffset.UtcNow));

        var retried =
            await context.QueueStore.ClaimNextAsync(
                runtime,
                runtime.WorkerId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(
                    1));

        Assert.NotNull(
            retried);

        Assert.Equal(
            second.UnitId,
            retried.WorkItem.UnitId);

        Assert.Equal(
            2,
            retried.WorkItem.AttemptNumber);
    }

    [PostgresFact]
    public async Task QueueStore_RecoversExpiredUnitsInExpiryOrder()
    {
        await using var context =
            await CreateContextAsync();

        var earliestExpired =
            CreateWorkItem();

        var latestExpired =
            CreateWorkItem();

        var pending =
            CreateWorkItem();

        var now =
            DateTimeOffset.UtcNow;

        await InsertExpiredActiveAsync(
            context.DataSource,
            earliestExpired,
            now.AddMinutes(
                -2));

        await InsertExpiredActiveAsync(
            context.DataSource,
            latestExpired,
            now.AddMinutes(
                -1));

        await InsertPendingAsync(
            context.DataSource,
            pending,
            queuePosition:
                10);

        Assert.Equal(
            2,
            await context.QueueStore.RecoverExpiredLeasesAsync(
                now));

        Assert.Equal(
            [earliestExpired.UnitId, latestExpired.UnitId, pending.UnitId],
            await ReadPendingOrderAsync(
                context.DataSource));
    }

    [PostgresFact]
    public async Task Runtime_StopRequeuesActivePostgresUnitDurably()
    {
        await using var context =
            await CreateContextAsync();

        var workItem =
            CreateWorkItem();

        await InsertPendingAsync(
            context.DataSource,
            workItem,
            queuePosition:
                1);

        var entered =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var executor =
            new DelegateExecutor(
                async (_, cancellationToken) =>
                {
                    entered.TrySetResult();

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        var workerId =
            "postgres-runtime-worker";

        var dispatcher =
            new SequentialProcessingDispatcher(
                context.QueueStore,
                executor,
                new SequentialProcessingDispatcherOptions(
                    workerId,
                    leaseDuration:
                        TimeSpan.FromSeconds(
                            5),
                    leaseRenewalInterval:
                        TimeSpan.FromSeconds(
                            1)),
                new BoundedProcessingFailurePolicy(
                    maximumAttempts:
                        1));

        var runtime =
            new DocumentProcessingManagerRuntime(
                context.StateStore,
                context.RuntimeLeaseStore,
                dispatcher,
                new DocumentProcessingManagerRuntimeOptions(
                    workerId,
                    runtimeLeaseDuration:
                        TimeSpan.FromSeconds(
                            5),
                    runtimeLeaseRenewalInterval:
                        TimeSpan.FromSeconds(
                            1),
                    idlePollingInterval:
                        TimeSpan.FromMilliseconds(
                            20)));

        using var hostStopping =
            new CancellationTokenSource();

        var running =
            runtime.RunAsync(
                hostStopping.Token);

        await runtime.ExecuteAsync(
            new StartManagerCommand());

        await entered.Task
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var stopped =
            await runtime.ExecuteAsync(
                new StopManagerCommand());

        Assert.Equal(
            ManagerOperatingState.Stopped,
            stopped.Snapshot.State);

        Assert.Equal(
            [workItem.UnitId],
            await ReadPendingOrderAsync(
                context.DataSource));

        hostStopping.Cancel();

        await running.WaitAsync(
            TimeSpan.FromSeconds(
                5));
    }

    #endregion

    #region Helpers

    private static async Task<PostgresTestContext> CreateContextAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionStringEnvironmentVariable) ??
            throw new InvalidOperationException(
                $"Missing {ConnectionStringEnvironmentVariable}.");

        var dataSource =
            NpgsqlDataSource.Create(
                connectionString);

        var context =
            new PostgresTestContext(
                dataSource);

        await context.Schema.InitializeAsync();

        await ResetAsync(
            dataSource);

        return context;
    }

    private static ProcessingWorkItem CreateWorkItem(
        ProcessingUnitScope? scope = null) =>
        new(
            ProcessingUnitId.New(),
            DocumentSubmissionId.New(),
            scope ??
            new ProcessingUnitScope.WholeDocument(),
            attemptNumber:
                1);

    private static async Task ResetAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command =
            dataSource.CreateCommand(
                """
                TRUNCATE TABLE document_processing_manager.processing_units;

                UPDATE document_processing_manager.queue_metadata
                SET version = 0
                WHERE singleton = TRUE;

                UPDATE document_processing_manager.runtime_lease
                SET token = NULL,
                    worker_id = NULL,
                    expires_at_utc = NULL
                WHERE singleton = TRUE;

                UPDATE document_processing_manager.manager_state
                SET operating_state = 0,
                    version = 0
                WHERE singleton = TRUE;
                """);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertPendingAsync(
        NpgsqlDataSource dataSource,
        ProcessingWorkItem workItem,
        long queuePosition)
    {
        var scopeKind =
            workItem.Scope is ProcessingUnitScope.WholeDocument
                ? (short)0
                : (short)1;

        var pageRange =
            workItem.Scope as ProcessingUnitScope.PageRange;

        await using var command =
            dataSource.CreateCommand(
                """
                INSERT INTO document_processing_manager.processing_units
                (
                    unit_id,
                    submission_id,
                    scope_kind,
                    start_physical_page_number,
                    end_physical_page_number,
                    scope_title,
                    attempt_number,
                    status,
                    queue_position
                )
                VALUES
                (
                    @unit_id,
                    @submission_id,
                    @scope_kind,
                    @start_page,
                    @end_page,
                    @scope_title,
                    @attempt_number,
                    0,
                    @queue_position
                );
                """);

        command.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            workItem.UnitId.Value);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            workItem.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "scope_kind",
            NpgsqlDbType.Smallint,
            scopeKind);

        command.Parameters.AddWithValue(
            "start_page",
            NpgsqlDbType.Integer,
            pageRange is null
                ? DBNull.Value
                : pageRange.StartPhysicalPageNumber);

        command.Parameters.AddWithValue(
            "end_page",
            NpgsqlDbType.Integer,
            pageRange is null
                ? DBNull.Value
                : pageRange.EndPhysicalPageNumber);

        command.Parameters.AddWithValue(
            "scope_title",
            NpgsqlDbType.Text,
            pageRange is null
                ? DBNull.Value
                : pageRange.Title);

        command.Parameters.AddWithValue(
            "attempt_number",
            NpgsqlDbType.Integer,
            workItem.AttemptNumber);

        command.Parameters.AddWithValue(
            "queue_position",
            NpgsqlDbType.Bigint,
            queuePosition);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertExpiredActiveAsync(
        NpgsqlDataSource dataSource,
        ProcessingWorkItem workItem,
        DateTimeOffset expiredAtUtc)
    {
        await using var command =
            dataSource.CreateCommand(
                """
                INSERT INTO document_processing_manager.processing_units
                (
                    unit_id,
                    submission_id,
                    scope_kind,
                    attempt_number,
                    status,
                    unit_lease_token,
                    runtime_lease_token,
                    worker_id,
                    unit_lease_expires_at_utc
                )
                VALUES
                (
                    @unit_id,
                    @submission_id,
                    0,
                    @attempt_number,
                    1,
                    @unit_lease_token,
                    @runtime_lease_token,
                    @worker_id,
                    @expired_at_utc
                );
                """);

        command.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            workItem.UnitId.Value);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            workItem.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "attempt_number",
            NpgsqlDbType.Integer,
            workItem.AttemptNumber);

        command.Parameters.AddWithValue(
            "unit_lease_token",
            NpgsqlDbType.Uuid,
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "runtime_lease_token",
            NpgsqlDbType.Uuid,
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            "crashed-worker");

        command.Parameters.AddWithValue(
            "expired_at_utc",
            NpgsqlDbType.TimestampTz,
            expiredAtUtc.ToUniversalTime());

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<ProcessingUnitId>>
        ReadPendingOrderAsync(
        NpgsqlDataSource dataSource)
    {
        await using var command =
            dataSource.CreateCommand(
                """
                SELECT unit_id
                FROM document_processing_manager.processing_units
                WHERE status = 0
                ORDER BY queue_position, unit_id;
                """);

        await using var reader =
            await command.ExecuteReaderAsync();

        var result =
            new List<ProcessingUnitId>();

        while (await reader.ReadAsync())
        {
            result.Add(
                new ProcessingUnitId(
                    reader.GetGuid(
                        0)));
        }

        return result;
    }

    #endregion

    #region Internal Types

    private sealed class PostgresTestContext(
        NpgsqlDataSource dataSource)
        : IAsyncDisposable
    {
        public NpgsqlDataSource DataSource
        {
            get;
        } =
            dataSource;

        public PostgresManagerSchema Schema
        {
            get;
        } =
            new(
                dataSource);

        public PostgresManagerStateStore StateStore
        {
            get;
        } =
            new(
                dataSource);

        public PostgresManagerRuntimeLeaseStore RuntimeLeaseStore
        {
            get;
        } =
            new(
                dataSource);

        public PostgresProcessingQueueStore QueueStore
        {
            get;
        } =
            new(
                dataSource);

        public ValueTask DisposeAsync() =>
            DataSource.DisposeAsync();
    }

    private sealed class DelegateExecutor(
        Func<ProcessingWorkItem, CancellationToken, Task<ProcessingExecutionOutcome>>
            execute)
        : IDocumentProcessingExecutor
    {
        public ValueTask<ProcessingExecutionOutcome> ExecuteAsync(
            ProcessingWorkItem workItem,
            CancellationToken cancellationToken = default) =>
            new(
                execute(
                    workItem,
                    cancellationToken));
    }

    #endregion
}
