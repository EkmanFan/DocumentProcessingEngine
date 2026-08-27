using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Runtime;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for exclusive durable ownership of the Manager runtime.
/// </summary>
public sealed class PostgresManagerRuntimeLeaseStore
    : IManagerRuntimeLeaseStore
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL global runtime-lease adapter.
    /// </summary>
    public PostgresManagerRuntimeLeaseStore(
        NpgsqlDataSource dataSource)
    {
        _dataSource =
            dataSource ??
            throw new ArgumentNullException(
                nameof(dataSource));
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask<ManagerRuntimeLease?> TryAcquireAsync(
        string workerId,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                workerId))
        {
            throw new ArgumentException(
                "Manager runtime worker identifier cannot be empty.",
                nameof(workerId));
        }

        var duration =
            PostgresLeaseDuration.Calculate(
                observedAtUtc,
                leaseExpiresAtUtc,
                nameof(leaseExpiresAtUtc));

        await using var command =
            _dataSource.CreateCommand(
                """
                UPDATE document_processing_manager.runtime_lease
                SET token = @token,
                    worker_id = @worker_id,
                    expires_at_utc = clock_timestamp() + @lease_duration
                WHERE singleton = TRUE
                    AND
                    (
                        token IS NULL
                        OR expires_at_utc <= clock_timestamp()
                    )
                RETURNING token, worker_id, expires_at_utc;
                """);

        command.Parameters.AddWithValue(
            "token",
            NpgsqlDbType.Uuid,
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            workerId.Trim());

        command.Parameters.AddWithValue(
            "lease_duration",
            NpgsqlDbType.Interval,
            duration);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ? ReadLease(
                reader)
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RenewAsync(
        ManagerRuntimeLease lease,
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
                UPDATE document_processing_manager.runtime_lease
                SET expires_at_utc = clock_timestamp() + @lease_duration
                WHERE singleton = TRUE
                    AND token = @token
                    AND worker_id = @worker_id
                    AND expires_at_utc > clock_timestamp();
                """);

        command.Parameters.AddWithValue(
            "lease_duration",
            NpgsqlDbType.Interval,
            duration);

        command.Parameters.AddWithValue(
            "token",
            NpgsqlDbType.Uuid,
            lease.Token);

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            lease.WorkerId);

        return await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) ==
            1;
    }

    /// <inheritdoc />
    public async ValueTask<bool> ReleaseAsync(
        ManagerRuntimeLease lease,
        DateTimeOffset releasedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            lease);

        await using var command =
            _dataSource.CreateCommand(
                """
                UPDATE document_processing_manager.runtime_lease
                SET token = NULL,
                    worker_id = NULL,
                    expires_at_utc = NULL
                WHERE singleton = TRUE
                    AND token = @token
                    AND worker_id = @worker_id;
                """);

        command.Parameters.AddWithValue(
            "token",
            NpgsqlDbType.Uuid,
            lease.Token);

        command.Parameters.AddWithValue(
            "worker_id",
            NpgsqlDbType.Text,
            lease.WorkerId);

        return await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) ==
            1;
    }

    private static ManagerRuntimeLease ReadLease(
        NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(
                0),
            reader.GetString(
                1),
            reader.GetFieldValue<DateTimeOffset>(
                2));

    #endregion
}
