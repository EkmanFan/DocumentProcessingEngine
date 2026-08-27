using Npgsql;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// Installs the versioned PostgreSQL schema owned by the Manager adapter.
/// </summary>
public sealed class PostgresManagerSchema
{
    #region Variables and Constants

    private const string
        BootstrapSql =
            """
            SELECT pg_advisory_xact_lock(1129333332, 1296126535);

            CREATE SCHEMA IF NOT EXISTS document_processing_manager;

            CREATE TABLE IF NOT EXISTS document_processing_manager.schema_versions
            (
                version integer PRIMARY KEY,
                applied_at_utc timestamp with time zone NOT NULL
                    DEFAULT clock_timestamp()
            );
            """;

    private const string
        MigrationOneSql =
            """
            CREATE TABLE IF NOT EXISTS document_processing_manager.manager_state
            (
                singleton boolean PRIMARY KEY DEFAULT TRUE
                    CHECK (singleton),
                operating_state smallint NOT NULL
                    CHECK (operating_state BETWEEN 0 AND 2),
                version bigint NOT NULL
                    CHECK (version >= 0)
            );

            INSERT INTO document_processing_manager.manager_state
                (singleton, operating_state, version)
            VALUES
                (TRUE, 0, 0)
            ON CONFLICT (singleton) DO NOTHING;

            CREATE TABLE IF NOT EXISTS document_processing_manager.runtime_lease
            (
                singleton boolean PRIMARY KEY DEFAULT TRUE
                    CHECK (singleton),
                token uuid NULL,
                worker_id text NULL,
                expires_at_utc timestamp with time zone NULL,
                CHECK
                (
                    (token IS NULL AND worker_id IS NULL AND expires_at_utc IS NULL)
                    OR
                    (token IS NOT NULL AND worker_id IS NOT NULL AND expires_at_utc IS NOT NULL)
                )
            );

            INSERT INTO document_processing_manager.runtime_lease
                (singleton, token, worker_id, expires_at_utc)
            VALUES
                (TRUE, NULL, NULL, NULL)
            ON CONFLICT (singleton) DO NOTHING;

            CREATE TABLE IF NOT EXISTS document_processing_manager.queue_metadata
            (
                singleton boolean PRIMARY KEY DEFAULT TRUE
                    CHECK (singleton),
                version bigint NOT NULL
                    CHECK (version >= 0)
            );

            INSERT INTO document_processing_manager.queue_metadata
                (singleton, version)
            VALUES
                (TRUE, 0)
            ON CONFLICT (singleton) DO NOTHING;

            CREATE TABLE IF NOT EXISTS document_processing_manager.processing_units
            (
                unit_id uuid PRIMARY KEY,
                submission_id uuid NOT NULL,
                scope_kind smallint NOT NULL
                    CHECK (scope_kind BETWEEN 0 AND 1),
                start_physical_page_number integer NULL,
                end_physical_page_number integer NULL,
                scope_title text NULL,
                attempt_number integer NOT NULL
                    CHECK (attempt_number > 0),
                status smallint NOT NULL
                    CHECK (status BETWEEN 0 AND 3),
                queue_position bigint NULL,
                unit_lease_token uuid NULL,
                runtime_lease_token uuid NULL,
                worker_id text NULL,
                unit_lease_expires_at_utc timestamp with time zone NULL,
                result_reference text NULL,
                failure_code text NULL,
                failure_message text NULL,
                interruption_reason smallint NULL,
                created_at_utc timestamp with time zone NOT NULL
                    DEFAULT clock_timestamp(),
                updated_at_utc timestamp with time zone NOT NULL
                    DEFAULT clock_timestamp(),
                CHECK
                (
                    (scope_kind = 0
                        AND start_physical_page_number IS NULL
                        AND end_physical_page_number IS NULL
                        AND scope_title IS NULL)
                    OR
                    (scope_kind = 1
                        AND start_physical_page_number IS NOT NULL
                        AND start_physical_page_number > 0
                        AND end_physical_page_number IS NOT NULL
                        AND end_physical_page_number >= start_physical_page_number
                        AND scope_title IS NOT NULL
                        AND length(scope_title) > 0)
                ),
                CHECK
                (
                    (status = 0
                        AND queue_position IS NOT NULL
                        AND unit_lease_token IS NULL
                        AND runtime_lease_token IS NULL
                        AND worker_id IS NULL
                        AND unit_lease_expires_at_utc IS NULL)
                    OR
                    (status = 1
                        AND queue_position IS NULL
                        AND unit_lease_token IS NOT NULL
                        AND runtime_lease_token IS NOT NULL
                        AND worker_id IS NOT NULL
                        AND unit_lease_expires_at_utc IS NOT NULL)
                    OR
                    (status BETWEEN 2 AND 3
                        AND queue_position IS NULL
                        AND unit_lease_token IS NULL
                        AND runtime_lease_token IS NULL
                        AND worker_id IS NULL
                        AND unit_lease_expires_at_utc IS NULL)
                )
            );

            CREATE INDEX IF NOT EXISTS ix_processing_units_pending_order
                ON document_processing_manager.processing_units
                    (queue_position, unit_id)
                WHERE status = 0;

            CREATE INDEX IF NOT EXISTS ix_processing_units_expired_lease
                ON document_processing_manager.processing_units
                    (unit_lease_expires_at_utc)
                WHERE status = 1;
            """;

    private static readonly Migration[]
        Migrations =
        [
            new(
                Version:
                    1,
                MigrationOneSql)
        ];

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL schema installer.
    /// </summary>
    public PostgresManagerSchema(
        NpgsqlDataSource dataSource)
    {
        _dataSource =
            dataSource ??
            throw new ArgumentNullException(
                nameof(dataSource));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies all idempotent Manager schema migrations transactionally.
    /// </summary>
    public async Task InitializeAsync(
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
                    cancellationToken)
                .ConfigureAwait(false);

        await using var bootstrap =
            new NpgsqlCommand(
                BootstrapSql,
                connection,
                transaction);

        await bootstrap
            .ExecuteNonQueryAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await using var versionCommand =
            new NpgsqlCommand(
                """
                SELECT COALESCE(MAX(version), 0)
                FROM document_processing_manager.schema_versions;
                """,
                connection,
                transaction);

        var version =
            Convert.ToInt32(
                await versionCommand
                    .ExecuteScalarAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        var currentSchemaVersion =
            Migrations[^1].Version;

        if (version >
            currentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"PostgreSQL Manager schema version {version} is newer than supported version {currentSchemaVersion}.");
        }

        foreach (var migration in Migrations.Where(
                     migration =>
                         migration.Version >
                         version))
        {
            await using var migrationCommand =
                new NpgsqlCommand(
                    migration.Sql,
                    connection,
                    transaction);

            await migrationCommand
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            await using var recordMigration =
                new NpgsqlCommand(
                    """
                    INSERT INTO document_processing_manager.schema_versions
                        (version)
                    VALUES
                        (@version);
                    """,
                    connection,
                    transaction);

            recordMigration.Parameters.AddWithValue(
                "version",
                migration.Version);

            await recordMigration
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Internal Types

    private sealed record Migration(
        int Version,
        string Sql);

    #endregion
}
