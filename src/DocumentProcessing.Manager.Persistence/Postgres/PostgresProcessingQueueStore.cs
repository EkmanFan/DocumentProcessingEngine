using System.Data;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Runtime;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for the durable globally ordered processing queue.
/// </summary>
public sealed class PostgresProcessingQueueStore
    : IProcessingQueueStore
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region Methods Shelve

    /// <inheritdoc />
    public async ValueTask ShelvePendingAsync(
        ShelveProcessingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction =
            await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

        var actualVersion =
            await LockQueueAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        if (actualVersion != command.ExpectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(
                command.ExpectedQueueVersion,
                actualVersion);
        }

        await using var shelve = new NpgsqlCommand(
            """
            UPDATE document_processing_manager.processing_units
            SET released_at_utc = NULL,
                updated_at_utc = clock_timestamp()
            WHERE unit_id = @unit_id
                AND status = 0
                AND released_at_utc IS NOT NULL;
            """,
            connection,
            transaction);

        shelve.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, command.UnitId.Value);

        if (await shelve.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException(
                "Only a ready pending processing unit can be shelved.");
        }

        await IncrementQueueVersionAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Methods Split

    /// <inheritdoc />
    public async ValueTask SplitPendingAsync(
        SplitPendingProcessingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction =
            await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

        var actualVersion =
            await LockQueueAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        if (actualVersion != command.ExpectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(
                command.ExpectedQueueVersion,
                actualVersion);
        }

        Guid submissionId;
        long queuePosition;

        await using (var read = new NpgsqlCommand(
                         """
                         SELECT submission_id, queue_position
                         FROM document_processing_manager.processing_units
                         WHERE unit_id = @unit_id
                             AND status = 0
                             AND scope_kind = 0
                         FOR UPDATE;
                         """,
                         connection,
                         transaction))
        {
            read.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, command.UnitId.Value);

            await using var reader =
                await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "Only a pending whole-document unit can be split.");
            }

            submissionId = reader.GetGuid(0);
            queuePosition = reader.GetInt64(1);
        }

        if (command.ReplacementUnits.Any(
                intake => intake.WorkItem.SubmissionId.Value != submissionId))
        {
            throw new ArgumentException(
                "Every replacement unit must belong to the original submission.",
                nameof(command));
        }

        await using (var delete = new NpgsqlCommand(
                         "DELETE FROM document_processing_manager.processing_units WHERE unit_id = @unit_id;",
                         connection,
                         transaction))
        {
            delete.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, command.UnitId.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var offset = command.ReplacementUnits.Count - 1;

        if (offset > 0)
        {
            await using var shift = new NpgsqlCommand(
                """
                UPDATE document_processing_manager.processing_units
                SET queue_position = queue_position + @offset,
                    updated_at_utc = clock_timestamp()
                WHERE status = 0
                    AND queue_position > @queue_position;
                """,
                connection,
                transaction);

            shift.Parameters.AddWithValue("offset", NpgsqlDbType.Bigint, (long)offset);
            shift.Parameters.AddWithValue("queue_position", NpgsqlDbType.Bigint, queuePosition);
            await shift.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        for (var index = 0; index < command.ReplacementUnits.Count; index++)
        {
            var intake = command.ReplacementUnits[index];
            var range = (ProcessingUnitScope.PageRange)intake.WorkItem.Scope;

            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.processing_units
                (
                    unit_id, submission_id, scope_kind,
                    start_physical_page_number, end_physical_page_number, scope_title,
                    attempt_number, submission_unit_ordinal, status, queue_position, released_at_utc
                )
                VALUES
                (
                    @unit_id, @submission_id, 1,
                    @start_page, @end_page, @title,
                    1, @ordinal, 0, @queue_position,
                    CASE WHEN @is_ready THEN clock_timestamp() ELSE NULL END
                );
                """,
                connection,
                transaction);

            insert.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, intake.WorkItem.UnitId.Value);
            insert.Parameters.AddWithValue("submission_id", NpgsqlDbType.Uuid, submissionId);
            insert.Parameters.AddWithValue("start_page", NpgsqlDbType.Integer, range.StartPhysicalPageNumber);
            insert.Parameters.AddWithValue("end_page", NpgsqlDbType.Integer, range.EndPhysicalPageNumber);
            insert.Parameters.AddWithValue("title", NpgsqlDbType.Text, range.Title);
            insert.Parameters.AddWithValue("ordinal", NpgsqlDbType.Integer, index + 1);
            insert.Parameters.AddWithValue("queue_position", NpgsqlDbType.Bigint, queuePosition + index);
            insert.Parameters.AddWithValue(
                "is_ready",
                NpgsqlDbType.Boolean,
                intake.DispatchState == ProcessingUnitDispatchState.Ready);

            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await IncrementQueueVersionAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL processing-queue adapter.
    /// </summary>
    public PostgresProcessingQueueStore(
        NpgsqlDataSource dataSource)
    {
        _dataSource =
            dataSource ??
            throw new ArgumentNullException(
                nameof(dataSource));
    }

    #endregion

    #region Methods Claim

    /// <inheritdoc />
    public async ValueTask<ProcessingLease?> ClaimNextAsync(
        ManagerRuntimeLease runtimeLease,
        string workerId,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeLease);

        if (!string.Equals(
                runtimeLease.WorkerId,
                workerId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The runtime lease and queue worker identifiers must match.",
                nameof(workerId));
        }

        var duration =
            PostgresLeaseDuration.Calculate(
                observedAtUtc,
                leaseExpiresAtUtc,
                nameof(leaseExpiresAtUtc));

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var transaction =
            await connection
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);

        await LockQueueAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        await using var command =
            new NpgsqlCommand(
                """
                WITH next_unit AS
                (
                    SELECT processing_unit.unit_id
                    FROM document_processing_manager.processing_units AS processing_unit
                    WHERE processing_unit.status = 0
                        AND processing_unit.released_at_utc IS NOT NULL
                        AND EXISTS
                        (
                            SELECT 1
                            FROM document_processing_manager.runtime_lease AS runtime_lease
                            WHERE runtime_lease.singleton = TRUE
                                AND runtime_lease.token = @runtime_lease_token
                                AND runtime_lease.worker_id = @worker_id
                                AND runtime_lease.expires_at_utc > clock_timestamp()
                        )
                    ORDER BY processing_unit.queue_position,
                        processing_unit.unit_id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE document_processing_manager.processing_units AS processing_unit
                SET status = 1,
                    queue_position = NULL,
                    unit_lease_token = @unit_lease_token,
                    runtime_lease_token = @runtime_lease_token,
                    worker_id = @worker_id,
                    unit_lease_expires_at_utc = clock_timestamp() + @lease_duration,
                    interruption_reason = NULL,
                    updated_at_utc = clock_timestamp()
                FROM next_unit
                WHERE processing_unit.unit_id = next_unit.unit_id
                RETURNING
                    processing_unit.unit_id,
                    processing_unit.submission_id,
                    processing_unit.scope_kind,
                    processing_unit.start_physical_page_number,
                    processing_unit.end_physical_page_number,
                    processing_unit.scope_title,
                    processing_unit.attempt_number,
                    processing_unit.unit_lease_token,
                    processing_unit.runtime_lease_token,
                    processing_unit.worker_id,
                    processing_unit.unit_lease_expires_at_utc;
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "runtime_lease_token",
            NpgsqlDbType.Uuid,
            runtimeLease.Token);

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            workerId);

        command.Parameters.AddWithValue(
            "unit_lease_token",
            NpgsqlDbType.Uuid,
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "lease_duration",
            NpgsqlDbType.Interval,
            duration);

        ProcessingLease? lease;

        await using (var reader =
                     await command
                         .ExecuteReaderAsync(
                             cancellationToken)
                         .ConfigureAwait(false))
        {
            lease =
                await reader
                    .ReadAsync(
                        cancellationToken)
                    .ConfigureAwait(false)
                    ? ReadLease(
                        reader)
                    : null;
        }

        if (lease is not null)
        {
            await IncrementQueueVersionAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return lease;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RenewLeaseAsync(
        ProcessingLease lease,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            lease);

        var duration =
            PostgresLeaseDuration.Calculate(
                observedAtUtc,
                leaseExpiresAtUtc,
                nameof(leaseExpiresAtUtc));

        await using var command =
            _dataSource.CreateCommand(
                """
                UPDATE document_processing_manager.processing_units AS processing_unit
                SET unit_lease_expires_at_utc = clock_timestamp() + @lease_duration,
                    updated_at_utc = clock_timestamp()
                WHERE processing_unit.unit_id = @unit_id
                    AND processing_unit.status = 1
                    AND processing_unit.unit_lease_token = @unit_lease_token
                    AND processing_unit.runtime_lease_token = @runtime_lease_token
                    AND processing_unit.worker_id = @worker_id
                    AND processing_unit.unit_lease_expires_at_utc > clock_timestamp()
                    AND EXISTS
                    (
                        SELECT 1
                        FROM document_processing_manager.runtime_lease AS runtime_lease
                        WHERE runtime_lease.singleton = TRUE
                            AND runtime_lease.token = processing_unit.runtime_lease_token
                            AND runtime_lease.worker_id = processing_unit.worker_id
                            AND runtime_lease.expires_at_utc > clock_timestamp()
                    );
                """);

        AddOwnedLeaseParameters(
            command,
            lease);

        command.Parameters.AddWithValue(
            "lease_duration",
            NpgsqlDbType.Interval,
            duration);

        return await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) ==
            1;
    }

    #endregion

    #region Methods Finalize

    /// <inheritdoc />
    public async ValueTask<bool> CompleteAsync(
        ProcessingLease lease,
        ProcessingExecutionOutcome.Success outcome,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            lease);

        ArgumentNullException.ThrowIfNull(
            outcome);

        await using var command =
            CreateOwnedLeaseCommand(
                """
                WITH completed AS
                (
                    UPDATE document_processing_manager.processing_units AS processing_unit
                    SET status = 2,
                        unit_lease_token = NULL,
                        runtime_lease_token = NULL,
                        worker_id = NULL,
                        unit_lease_expires_at_utc = NULL,
                        result_reference = @result_reference,
                        failure_code = NULL,
                        failure_message = NULL,
                        interruption_reason = NULL,
                        updated_at_utc = @completed_at_utc
                    WHERE {0}
                        AND EXISTS
                        (
                            SELECT 1
                            FROM document_processing_manager.processing_results AS processing_result
                            WHERE processing_result.result_reference = @result_reference
                                AND processing_result.processing_unit_id = processing_unit.unit_id
                        )
                    RETURNING result_reference, updated_at_utc
                )
                INSERT INTO document_processing_manager.result_available_events
                    (result_reference, available_at_utc)
                SELECT result_reference, updated_at_utc
                FROM completed;
                """,
                lease);

        command.Parameters.AddWithValue(
            "result_reference",
            NpgsqlDbType.Text,
            outcome.ResultReference);

        command.Parameters.AddWithValue(
            "completed_at_utc",
            NpgsqlDbType.TimestampTz,
            completedAtUtc.ToUniversalTime());

        return await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) ==
            1;
    }

    /// <inheritdoc />
    public async ValueTask<bool> FailAsync(
        ProcessingLease lease,
        ProcessingFailure failure,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            lease);

        ArgumentNullException.ThrowIfNull(
            failure);

        await using var command =
            CreateOwnedLeaseCommand(
                """
                UPDATE document_processing_manager.processing_units AS processing_unit
                SET status = 3,
                    unit_lease_token = NULL,
                    runtime_lease_token = NULL,
                    worker_id = NULL,
                    unit_lease_expires_at_utc = NULL,
                    result_reference = NULL,
                    failure_code = @failure_code,
                    failure_message = @failure_message,
                    interruption_reason = NULL,
                    updated_at_utc = @failed_at_utc
                WHERE {0};
                """,
                lease);

        AddFailureParameters(
            command,
            failure,
            "failed_at_utc",
            failedAtUtc);

        return await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) ==
            1;
    }

    #endregion

    #region Methods Requeue

    /// <inheritdoc />
    public ValueTask<bool> RequeueAfterFailureAsync(
        ProcessingLease lease,
        ProcessingFailure failure,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken = default) =>
        RequeueAsync(
            lease,
            queueAtFront:
                false,
            incrementAttempt:
                true,
            failure,
            interruptionReason:
                null,
            failedAtUtc,
            cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> InterruptAndRequeueAsync(
        ProcessingLease lease,
        ProcessingInterruptionReason reason,
        DateTimeOffset interruptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(
                reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "Unknown processing-interruption reason.");
        }

        return RequeueAsync(
            lease,
            queueAtFront:
                true,
            incrementAttempt:
                false,
            failure:
                null,
            reason,
            interruptedAtUtc,
            cancellationToken);
    }

    private async ValueTask<bool> RequeueAsync(
        ProcessingLease lease,
        bool queueAtFront,
        bool incrementAttempt,
        ProcessingFailure? failure,
        ProcessingInterruptionReason? interruptionReason,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            lease);

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var transaction =
            await connection
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);

        await LockQueueAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        var positionExpression =
            queueAtFront
                ? "COALESCE((SELECT MIN(queue_position) FROM document_processing_manager.processing_units WHERE status = 0), 1) - 1"
                : "COALESCE((SELECT MAX(queue_position) FROM document_processing_manager.processing_units WHERE status = 0), 0) + 1";

        var attemptExpression =
            incrementAttempt
                ? "attempt_number + 1"
                : "attempt_number";

        await using var command =
            new NpgsqlCommand(
                $$"""
                UPDATE document_processing_manager.processing_units AS processing_unit
                SET status = 0,
                    queue_position = {{positionExpression}},
                    attempt_number = {{attemptExpression}},
                    unit_lease_token = NULL,
                    runtime_lease_token = NULL,
                    worker_id = NULL,
                    unit_lease_expires_at_utc = NULL,
                    result_reference = NULL,
                    failure_code = @failure_code,
                    failure_message = @failure_message,
                    interruption_reason = @interruption_reason,
                    updated_at_utc = @observed_at_utc
                WHERE {{OwnedLeasePredicate}};
                """,
                connection,
                transaction);

        AddOwnedLeaseParameters(
            command,
            lease);

        AddNullableTextParameter(
            command,
            "failure_code",
            failure?.Code);

        AddNullableTextParameter(
            command,
            "failure_message",
            failure?.Message);

        command.Parameters.AddWithValue(
            "interruption_reason",
            NpgsqlDbType.Smallint,
            interruptionReason is null
                ? DBNull.Value
                : (short)interruptionReason.Value);

        command.Parameters.AddWithValue(
            "observed_at_utc",
            NpgsqlDbType.TimestampTz,
            observedAtUtc.ToUniversalTime());

        var requeued =
            await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) ==
            1;

        if (requeued)
        {
            await IncrementQueueVersionAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return requeued;
    }

    /// <inheritdoc />
    public async ValueTask<int> RecoverExpiredLeasesAsync(
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var transaction =
            await connection
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);

        await LockQueueAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        await using var command =
            new NpgsqlCommand(
                """
                WITH locked_expired AS
                (
                    SELECT processing_unit.unit_id,
                        processing_unit.unit_lease_expires_at_utc
                    FROM document_processing_manager.processing_units AS processing_unit
                    WHERE processing_unit.status = 1
                        AND processing_unit.unit_lease_expires_at_utc <= clock_timestamp()
                    FOR UPDATE
                ),
                expired AS
                (
                    SELECT locked_expired.unit_id,
                        row_number() OVER
                        (
                            ORDER BY locked_expired.unit_lease_expires_at_utc,
                                locked_expired.unit_id
                        ) AS recovery_order,
                        count(*) OVER () AS recovery_count
                    FROM locked_expired
                ),
                pending_front AS
                (
                    SELECT COALESCE(MIN(queue_position), 1) AS first_position
                    FROM document_processing_manager.processing_units
                    WHERE status = 0
                )
                UPDATE document_processing_manager.processing_units AS processing_unit
                SET status = 0,
                    queue_position = pending_front.first_position
                        - expired.recovery_count
                        + expired.recovery_order
                        - 1,
                    unit_lease_token = NULL,
                    runtime_lease_token = NULL,
                    worker_id = NULL,
                    unit_lease_expires_at_utc = NULL,
                    interruption_reason = NULL,
                    updated_at_utc = clock_timestamp()
                FROM expired, pending_front
                WHERE processing_unit.unit_id = expired.unit_id;
                """,
                connection,
                transaction);

        var recovered =
            await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (recovered >
            0)
        {
            await IncrementQueueVersionAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return recovered;
    }

    #endregion

    #region Methods Reorder

    /// <inheritdoc />
    public async ValueTask ReorderPendingAsync(
        ReorderProcessingQueueCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var transaction =
            await connection
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);

        var actualVersion =
            await LockQueueAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);

        if (actualVersion !=
            command.ExpectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(
                command.ExpectedQueueVersion,
                actualVersion);
        }

        var pendingIds =
            await ReadPendingUnitIdsAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!pendingIds.ToHashSet().SetEquals(
                command.OrderedPendingUnitIds))
        {
            throw new InvalidOperationException(
                "The reorder command must contain every currently pending processing unit exactly once.");
        }

        await using var reorder =
            new NpgsqlCommand(
                """
                UPDATE document_processing_manager.processing_units AS processing_unit
                SET queue_position = requested.position,
                    updated_at_utc = clock_timestamp()
                FROM unnest(@unit_ids::uuid[]) WITH ORDINALITY
                    AS requested(unit_id, position)
                WHERE processing_unit.unit_id = requested.unit_id
                    AND processing_unit.status = 0;
                """,
                connection,
                transaction);

        reorder.Parameters.AddWithValue(
            "unit_ids",
            NpgsqlDbType.Array |
            NpgsqlDbType.Uuid,
            command.OrderedPendingUnitIds
                .Select(
                    unitId =>
                        unitId.Value)
                .ToArray());

        var reordered =
            await reorder
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (reordered !=
            pendingIds.Count)
        {
            throw new InvalidOperationException(
                "The durable pending queue changed during its locked reorder transaction.");
        }

        await IncrementQueueVersionAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Methods Release

    /// <inheritdoc />
    public async ValueTask ReleasePendingAsync(
        ReleaseProcessingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var transaction =
            await connection
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);

        var actualVersion =
            await LockQueueAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);

        if (actualVersion !=
            command.ExpectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(
                command.ExpectedQueueVersion,
                actualVersion);
        }

        await using var release =
            new NpgsqlCommand(
                """
                UPDATE document_processing_manager.processing_units
                SET released_at_utc = clock_timestamp(),
                    updated_at_utc = clock_timestamp()
                WHERE unit_id = @unit_id
                    AND status = 0
                    AND released_at_utc IS NULL;
                """,
                connection,
                transaction);

        release.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            command.UnitId.Value);

        if (await release
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) !=
            1)
        {
            throw new InvalidOperationException(
                "Only a currently shelved pending processing unit can be released.");
        }

        await IncrementQueueVersionAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Methods Retry

    /// <inheritdoc />
    public async ValueTask RetryFailedAsync(
        RetryFailedProcessingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var transaction =
            await connection
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);

        var actualVersion =
            await LockQueueAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);

        if (actualVersion !=
            command.ExpectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(
                command.ExpectedQueueVersion,
                actualVersion);
        }

        await using var retry =
            new NpgsqlCommand(
                """
                UPDATE document_processing_manager.processing_units
                SET status = 0,
                    queue_position =
                    (
                        SELECT COALESCE(MAX(pending.queue_position), 0) + 1
                        FROM document_processing_manager.processing_units AS pending
                        WHERE pending.status = 0
                    ),
                    attempt_number = attempt_number + 1,
                    result_reference = NULL,
                    interruption_reason = NULL,
                    updated_at_utc = clock_timestamp()
                WHERE unit_id = @unit_id
                    AND status = 3;
                """,
                connection,
                transaction);

        retry.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            command.UnitId.Value);

        if (await retry
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) !=
            1)
        {
            throw new InvalidOperationException(
                "Only a terminally failed processing unit can be retried.");
        }

        await IncrementQueueVersionAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Methods SQL

    private const string
        OwnedLeasePredicate =
            """
            processing_unit.unit_id = @unit_id
                AND processing_unit.status = 1
                AND processing_unit.unit_lease_token = @unit_lease_token
                AND processing_unit.runtime_lease_token = @runtime_lease_token
                AND processing_unit.worker_id = @worker_id
                AND processing_unit.unit_lease_expires_at_utc > clock_timestamp()
                AND EXISTS
                (
                    SELECT 1
                    FROM document_processing_manager.runtime_lease AS runtime_lease
                    WHERE runtime_lease.singleton = TRUE
                        AND runtime_lease.token = processing_unit.runtime_lease_token
                        AND runtime_lease.worker_id = processing_unit.worker_id
                        AND runtime_lease.expires_at_utc > clock_timestamp()
                )
            """;

    private NpgsqlCommand CreateOwnedLeaseCommand(
        string sqlTemplate,
        ProcessingLease lease)
    {
        var command =
            _dataSource.CreateCommand(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    sqlTemplate,
                    OwnedLeasePredicate));

        AddOwnedLeaseParameters(
            command,
            lease);

        return command;
    }

    private static void AddOwnedLeaseParameters(
        NpgsqlCommand command,
        ProcessingLease lease)
    {
        command.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            lease.WorkItem.UnitId.Value);

        command.Parameters.AddWithValue(
            "unit_lease_token",
            NpgsqlDbType.Uuid,
            lease.Token);

        command.Parameters.AddWithValue(
            "runtime_lease_token",
            NpgsqlDbType.Uuid,
            lease.RuntimeLeaseToken);

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            lease.WorkerId);
    }

    private static void AddFailureParameters(
        NpgsqlCommand command,
        ProcessingFailure failure,
        string observedParameterName,
        DateTimeOffset observedAtUtc)
    {
        command.Parameters.AddWithValue(
            "failure_code",
            NpgsqlDbType.Text,
            failure.Code);

        command.Parameters.AddWithValue(
            "failure_message",
            NpgsqlDbType.Text,
            failure.Message);

        command.Parameters.AddWithValue(
            observedParameterName,
            NpgsqlDbType.TimestampTz,
            observedAtUtc.ToUniversalTime());
    }

    private static void AddNullableTextParameter(
        NpgsqlCommand command,
        string parameterName,
        string? value) =>
        command.Parameters.AddWithValue(
            parameterName,
            NpgsqlDbType.Text,
            value is null
                ? DBNull.Value
                : value);

    private static async ValueTask<long> LockQueueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT version
                FROM document_processing_manager.queue_metadata
                WHERE singleton = TRUE
                FOR UPDATE;
                """,
                connection,
                transaction);

        var result =
            await command
                .ExecuteScalarAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return result is long version
            ? version
            : throw new InvalidOperationException(
                "The PostgreSQL Manager schema has not been initialized.");
    }

    private static async ValueTask IncrementQueueVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                UPDATE document_processing_manager.queue_metadata
                SET version = version + 1
                WHERE singleton = TRUE;
                """,
                connection,
                transaction);

        if (await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) !=
            1)
        {
            throw new InvalidOperationException(
                "The PostgreSQL Manager schema has not been initialized.");
        }
    }

    private static async ValueTask<IReadOnlyList<ProcessingUnitId>>
        ReadPendingUnitIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT unit_id
                FROM document_processing_manager.processing_units
                WHERE status = 0
                ORDER BY queue_position, unit_id
                FOR UPDATE;
                """,
                connection,
                transaction);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        var unitIds =
            new List<ProcessingUnitId>();

        while (await reader
                   .ReadAsync(
                       cancellationToken)
                   .ConfigureAwait(false))
        {
            unitIds.Add(
                new ProcessingUnitId(
                    reader.GetGuid(
                        0)));
        }

        return unitIds;
    }

    private static ProcessingLease ReadLease(
        NpgsqlDataReader reader)
    {
        ProcessingUnitScope scope =
            reader.GetInt16(
                2) switch
            {
                0 =>
                    new ProcessingUnitScope.WholeDocument(),
                1 =>
                    new ProcessingUnitScope.PageRange(
                        reader.GetInt32(
                            3),
                        reader.GetInt32(
                            4),
                        reader.GetString(
                            5)),
                var scopeKind =>
                    throw new InvalidOperationException(
                        $"Unknown durable processing scope kind '{scopeKind}'.")
            };

        var workItem =
            new ProcessingWorkItem(
                new ProcessingUnitId(
                    reader.GetGuid(
                        0)),
                new DocumentSubmissionId(
                    reader.GetGuid(
                        1)),
                scope,
                reader.GetInt32(
                    6));

        return new ProcessingLease(
            workItem,
            reader.GetGuid(
                7),
            reader.GetGuid(
                8),
            reader.GetString(
                9),
            reader.GetFieldValue<DateTimeOffset>(
                10));
    }

    #endregion
}
