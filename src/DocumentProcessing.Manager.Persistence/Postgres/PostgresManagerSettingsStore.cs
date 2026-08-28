using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Settings;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for durable versioned Manager settings.
/// </summary>
public sealed class PostgresManagerSettingsStore
    : IManagerSettingsStore
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>Creates the PostgreSQL Manager-settings adapter.</summary>
    public PostgresManagerSettingsStore(
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
    public async ValueTask<ManagerSettingsSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await using var command =
            _dataSource.CreateCommand(
                """
                SELECT default_submission_dispatch_state,
                       visual_destination_root,
                       version,
                       completed_retention_days
                FROM document_processing_manager.manager_settings
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
                "The PostgreSQL Manager settings schema has not been initialized.");
        }

        return ReadSnapshot(
            reader);
    }

    /// <inheritdoc />
    public async ValueTask<ManagerSettingsSnapshot?> TryUpdateAsync(
        UpdateManagerSettingsCommand update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            update);

        await using var command =
            _dataSource.CreateCommand(
                """
                UPDATE document_processing_manager.manager_settings
                SET default_submission_dispatch_state = @dispatch_state,
                    visual_destination_root = @visual_destination_root,
                    completed_retention_days = @completed_retention_days,
                    version = version + 1
                WHERE singleton = TRUE
                    AND version = @expected_version
                RETURNING default_submission_dispatch_state,
                          visual_destination_root,
                          version,
                          completed_retention_days;
                """);

        command.Parameters.AddWithValue(
            "dispatch_state",
            NpgsqlDbType.Smallint,
            (short)update.DefaultSubmissionDispatchState);

        command.Parameters.AddWithValue(
            "visual_destination_root",
            NpgsqlDbType.Text,
            update.VisualDestinationRoot is null
                ? DBNull.Value
                : update.VisualDestinationRoot);

        command.Parameters.AddWithValue(
            "expected_version",
            NpgsqlDbType.Bigint,
            update.ExpectedVersion);

        command.Parameters.AddWithValue(
            "completed_retention_days",
            NpgsqlDbType.Integer,
            update.CompletedRetentionDays);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ? ReadSnapshot(
                reader)
            : null;
    }

    private static ManagerSettingsSnapshot ReadSnapshot(
        NpgsqlDataReader reader) =>
        new(
            (ProcessingUnitDispatchState)reader.GetInt16(
                0),
            reader.IsDBNull(
                1)
                ? null
                : reader.GetString(
                    1),
            reader.GetInt64(
                2),
            reader.GetInt32(
                3));

    #endregion
}
