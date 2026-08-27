using DocumentProcessing.Manager.Control;
using DocumentProcessing.Manager.Ports;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for the durable versioned Manager operating state.
/// </summary>
public sealed class PostgresManagerStateStore
    : IManagerStateStore
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL Manager-state adapter.
    /// </summary>
    public PostgresManagerStateStore(
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
    public async ValueTask<ManagerStateSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await using var command =
            _dataSource.CreateCommand(
                """
                SELECT operating_state, version
                FROM document_processing_manager.manager_state
                WHERE singleton = TRUE;
                """);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "The PostgreSQL Manager schema has not been initialized.");
        }

        return new ManagerStateSnapshot(
            (ManagerOperatingState)reader.GetInt16(
                0),
            reader.GetInt64(
                1));
    }

    /// <inheritdoc />
    public async ValueTask<ManagerStateSnapshot?> TrySetAsync(
        long expectedVersion,
        ManagerOperatingState state,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedVersion),
                expectedVersion,
                "Expected Manager-state version cannot be negative.");
        }

        if (!Enum.IsDefined(
                state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Unknown Manager operating state.");
        }

        await using var command =
            _dataSource.CreateCommand(
                """
                UPDATE document_processing_manager.manager_state
                SET operating_state = @state,
                    version = version + 1
                WHERE singleton = TRUE
                    AND version = @expected_version
                RETURNING operating_state, version;
                """);

        command.Parameters.AddWithValue(
            "state",
            NpgsqlDbType.Smallint,
            (short)state);

        command.Parameters.AddWithValue(
            "expected_version",
            NpgsqlDbType.Bigint,
            expectedVersion);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ? new ManagerStateSnapshot(
                (ManagerOperatingState)reader.GetInt16(
                    0),
                reader.GetInt64(
                    1))
            : null;
    }

    #endregion
}
