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

    private const string
        MigrationTwoSql =
            """
            CREATE TABLE document_processing_manager.source_artifacts
            (
                sha256_digest text PRIMARY KEY
                    CHECK (sha256_digest ~ '^[0-9a-f]{64}$'),
                byte_length bigint NOT NULL
                    CHECK (byte_length > 0),
                first_stored_at_utc timestamp with time zone NOT NULL
                    DEFAULT clock_timestamp()
            );

            CREATE TABLE document_processing_manager.document_submissions
            (
                submission_id uuid PRIMARY KEY,
                source_sha256_digest text NOT NULL
                    REFERENCES document_processing_manager.source_artifacts
                        (sha256_digest),
                original_file_name text NOT NULL
                    CHECK (length(original_file_name) > 0),
                declared_media_type text NULL,
                source_origin text NULL,
                submitted_at_utc timestamp with time zone NOT NULL,
                UNIQUE
                    (submission_id, source_sha256_digest)
            );

            CREATE TABLE document_processing_manager.custody_events
            (
                event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                submission_id uuid NOT NULL,
                event_kind smallint NOT NULL
                    CHECK (event_kind >= 0),
                source_sha256_digest text NOT NULL,
                occurred_at_utc timestamp with time zone NOT NULL,
                FOREIGN KEY
                    (submission_id, source_sha256_digest)
                REFERENCES document_processing_manager.document_submissions
                    (submission_id, source_sha256_digest)
            );

            CREATE OR REPLACE FUNCTION
                document_processing_manager.reject_custody_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'Custody records are append-only: %.', TG_TABLE_NAME;
            END;
            $$;

            CREATE TRIGGER source_artifacts_are_immutable
            BEFORE UPDATE OR DELETE
            ON document_processing_manager.source_artifacts
            FOR EACH ROW
            EXECUTE FUNCTION document_processing_manager.reject_custody_mutation();

            CREATE TRIGGER document_submissions_are_immutable
            BEFORE UPDATE OR DELETE
            ON document_processing_manager.document_submissions
            FOR EACH ROW
            EXECUTE FUNCTION document_processing_manager.reject_custody_mutation();

            CREATE TRIGGER custody_events_are_append_only
            BEFORE UPDATE OR DELETE
            ON document_processing_manager.custody_events
            FOR EACH ROW
            EXECUTE FUNCTION document_processing_manager.reject_custody_mutation();

            ALTER TABLE document_processing_manager.processing_units
                ADD COLUMN submission_unit_ordinal integer NULL
                    CHECK (submission_unit_ordinal > 0);

            CREATE OR REPLACE FUNCTION
                document_processing_manager.reject_processing_unit_identity_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW.unit_id IS DISTINCT FROM OLD.unit_id
                    OR NEW.submission_id IS DISTINCT FROM OLD.submission_id
                    OR NEW.scope_kind IS DISTINCT FROM OLD.scope_kind
                    OR NEW.start_physical_page_number IS DISTINCT FROM OLD.start_physical_page_number
                    OR NEW.end_physical_page_number IS DISTINCT FROM OLD.end_physical_page_number
                    OR NEW.scope_title IS DISTINCT FROM OLD.scope_title
                    OR NEW.submission_unit_ordinal IS DISTINCT FROM OLD.submission_unit_ordinal
                    OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc
                THEN
                    RAISE EXCEPTION 'Processing-unit identity and source scope are immutable.';
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER processing_unit_identity_is_immutable
            BEFORE UPDATE
            ON document_processing_manager.processing_units
            FOR EACH ROW
            EXECUTE FUNCTION
                document_processing_manager.reject_processing_unit_identity_mutation();

            CREATE INDEX ix_document_submissions_source_digest
                ON document_processing_manager.document_submissions
                    (source_sha256_digest);

            CREATE INDEX ix_custody_events_submission
                ON document_processing_manager.custody_events
                    (submission_id, event_id);

            CREATE INDEX ix_processing_units_submission
                ON document_processing_manager.processing_units
                    (submission_id);

            CREATE UNIQUE INDEX ux_processing_units_submission_ordinal
                ON document_processing_manager.processing_units
                    (submission_id, submission_unit_ordinal)
                WHERE submission_unit_ordinal IS NOT NULL;

            ALTER TABLE document_processing_manager.processing_units
                ADD CONSTRAINT fk_processing_units_document_submission
                FOREIGN KEY (submission_id)
                REFERENCES document_processing_manager.document_submissions
                    (submission_id)
                NOT VALID;
            """;

    private const string
        MigrationThreeSql =
            """
            ALTER TABLE document_processing_manager.processing_units
                ADD CONSTRAINT uq_processing_units_unit_submission
                UNIQUE (unit_id, submission_id);

            CREATE TABLE document_processing_manager.processing_result_artifacts
            (
                sha256_digest text PRIMARY KEY
                    CHECK (sha256_digest ~ '^[0-9a-f]{64}$'),
                byte_length bigint NOT NULL
                    CHECK (byte_length > 0),
                first_stored_at_utc timestamp with time zone NOT NULL
                    DEFAULT clock_timestamp()
            );

            CREATE TABLE document_processing_manager.processing_results
            (
                result_reference text PRIMARY KEY
                    CHECK (length(result_reference) > 0),
                processing_unit_id uuid NOT NULL UNIQUE,
                submission_id uuid NOT NULL,
                result_sha256_digest text NOT NULL
                    REFERENCES document_processing_manager.processing_result_artifacts
                        (sha256_digest),
                media_type text NOT NULL
                    CHECK (length(media_type) > 0),
                schema_version text NOT NULL
                    CHECK (length(schema_version) > 0),
                produced_at_utc timestamp with time zone NOT NULL,
                FOREIGN KEY
                    (processing_unit_id, submission_id)
                REFERENCES document_processing_manager.processing_units
                    (unit_id, submission_id)
            );

            CREATE TRIGGER processing_result_artifacts_are_immutable
            BEFORE UPDATE OR DELETE
            ON document_processing_manager.processing_result_artifacts
            FOR EACH ROW
            EXECUTE FUNCTION document_processing_manager.reject_custody_mutation();

            CREATE TRIGGER processing_results_are_immutable
            BEFORE UPDATE OR DELETE
            ON document_processing_manager.processing_results
            FOR EACH ROW
            EXECUTE FUNCTION document_processing_manager.reject_custody_mutation();

            CREATE INDEX ix_processing_results_artifact_digest
                ON document_processing_manager.processing_results
                    (result_sha256_digest);
            """;

    private const string
        MigrationFourSql =
            """
            ALTER TABLE document_processing_manager.processing_units
                ADD COLUMN released_at_utc timestamp with time zone NULL
                    DEFAULT clock_timestamp();

            UPDATE document_processing_manager.processing_units
            SET released_at_utc = created_at_utc;

            ALTER TABLE document_processing_manager.processing_units
                ADD CONSTRAINT ck_processing_units_release_chronology
                CHECK
                (
                    released_at_utc IS NULL
                    OR released_at_utc >= created_at_utc
                );

            ALTER TABLE document_processing_manager.processing_units
                ADD CONSTRAINT ck_processing_units_dispatch_lifecycle
                CHECK
                (
                    status = 0
                    OR released_at_utc IS NOT NULL
                );

            CREATE OR REPLACE FUNCTION
                document_processing_manager.reject_processing_unit_release_reversal()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF OLD.released_at_utc IS NOT NULL
                    AND NEW.released_at_utc IS DISTINCT FROM OLD.released_at_utc
                THEN
                    RAISE EXCEPTION 'A processing-unit release is irreversible.';
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER processing_unit_release_is_irreversible
            BEFORE UPDATE
            ON document_processing_manager.processing_units
            FOR EACH ROW
            EXECUTE FUNCTION
                document_processing_manager.reject_processing_unit_release_reversal();

            CREATE INDEX ix_processing_units_ready_order
                ON document_processing_manager.processing_units
                    (queue_position, unit_id)
                WHERE status = 0
                    AND released_at_utc IS NOT NULL;
            """;

    private static readonly Migration[]
        Migrations =
        [
            new(
                Version:
                    1,
                MigrationOneSql),
            new(
                Version:
                    2,
                MigrationTwoSql),
            new(
                Version:
                    3,
                MigrationThreeSql),
            new(
                Version:
                    4,
                MigrationFourSql)
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
