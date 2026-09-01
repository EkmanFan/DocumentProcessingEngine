using System.Data;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>PostgreSQL adapter for explicit administrative custody purges.</summary>
public sealed class PostgresProcessingUnitCustodyPurgeStore(
    NpgsqlDataSource dataSource)
    : IProcessingUnitCustodyPurgeStore
{
    private readonly NpgsqlDataSource _dataSource =
        dataSource ?? throw new ArgumentNullException(nameof(dataSource));

    public async ValueTask<ProcessingUnitCustodyPurge> BeginPurgeAsync(
        PurgeTerminalProcessingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var actualVersion = await LockQueueAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (actualVersion != command.ExpectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(command.ExpectedQueueVersion, actualVersion);
        }

        Guid submissionId;
        short status;
        string sourceDigest;

        await using (var readUnit = new NpgsqlCommand(
                         """
                         SELECT unit.submission_id, unit.status, submission.source_sha256_digest
                         FROM document_processing_manager.processing_units AS unit
                         INNER JOIN document_processing_manager.document_submissions AS submission
                             ON submission.submission_id = unit.submission_id
                         WHERE unit.unit_id = @unit_id
                         FOR UPDATE OF unit, submission;
                         """,
                         connection,
                         transaction))
        {
            readUnit.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, command.UnitId.Value);
            await using var reader = await readUnit.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The processing unit does not exist.");
            }

            submissionId = reader.GetGuid(0);
            status = reader.GetInt16(1);
            sourceDigest = reader.GetString(2);
        }

        if (status is not (2 or 3))
        {
            throw new InvalidOperationException("Only a terminal processing unit can be permanently purged.");
        }

        string? resultReference = null;
        string? resultDigest = null;
        string? publicationDirectory = null;

        await using (var readResult = new NpgsqlCommand(
                         """
                         SELECT result_reference, result_sha256_digest, publication_directory
                         FROM document_processing_manager.processing_results
                         WHERE processing_unit_id = @unit_id
                         FOR UPDATE;
                         """,
                         connection,
                         transaction))
        {
            readResult.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, command.UnitId.Value);
            await using var reader = await readResult.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                resultReference = reader.GetString(0);
                resultDigest = reader.GetString(1);
                publicationDirectory = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        if (status == 2 && resultReference is null)
        {
            throw new InvalidOperationException("A succeeded processing unit has no registered result to purge.");
        }

        var deleteSubmission = !await ExistsAsync(
            connection,
            transaction,
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM document_processing_manager.processing_units
                WHERE submission_id = @submission_id
                    AND unit_id <> @unit_id
            );
            """,
            submissionId,
            command.UnitId.Value,
            cancellationToken).ConfigureAwait(false);

        var deleteSourceArtifact = deleteSubmission && !await ExistsAsync(
            connection,
            transaction,
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM document_processing_manager.document_submissions
                WHERE source_sha256_digest = @digest
                    AND submission_id <> @submission_id
            );
            """,
            submissionId,
            command.UnitId.Value,
            cancellationToken,
            sourceDigest).ConfigureAwait(false);

        var deleteResultArtifact = resultDigest is not null && !await ResultDigestIsSharedAsync(
            connection,
            transaction,
            resultDigest,
            resultReference!,
            cancellationToken).ConfigureAwait(false);

        var purgeId = Guid.NewGuid();

        await using (var authorize = new NpgsqlCommand(
                         """
                         INSERT INTO document_processing_manager.custody_purge_authorizations
                         (
                             purge_id, processing_unit_id, submission_id,
                             source_sha256_digest, result_reference, result_sha256_digest
                         )
                         VALUES
                         (
                             @purge_id, @unit_id, @submission_id,
                             @source_digest, @result_reference, @result_digest
                         );
                         """,
                         connection,
                         transaction))
        {
            AddPlanParameters(authorize, purgeId, command.UnitId, submissionId, sourceDigest, resultReference, resultDigest);
            await authorize.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var createJob = new NpgsqlCommand(
                         """
                         INSERT INTO document_processing_manager.custody_purge_jobs
                         (
                             purge_id, processing_unit_id, result_sha256_digest,
                             source_sha256_digest, publication_directory
                         )
                         VALUES
                         (
                             @purge_id, @unit_id, @result_digest,
                             @source_digest, @publication_directory
                         );
                         """,
                         connection,
                         transaction))
        {
            createJob.Parameters.AddWithValue("purge_id", NpgsqlDbType.Uuid, purgeId);
            createJob.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, command.UnitId.Value);
            AddNullableText(createJob, "result_digest", deleteResultArtifact ? resultDigest : null);
            AddNullableText(createJob, "source_digest", deleteSourceArtifact ? sourceDigest : null);
            AddNullableText(createJob, "publication_directory", publicationDirectory);
            await createJob.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (resultReference is not null)
        {
            await ExecuteAsync(connection, transaction,
                "DELETE FROM document_processing_manager.result_consumer_deliveries WHERE result_reference = @result_reference;",
                "result_reference", resultReference, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction,
                "DELETE FROM document_processing_manager.result_available_events WHERE result_reference = @result_reference;",
                "result_reference", resultReference, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction,
                "DELETE FROM document_processing_manager.processing_results WHERE result_reference = @result_reference;",
                "result_reference", resultReference, cancellationToken).ConfigureAwait(false);
        }

        if (deleteResultArtifact)
        {
            await ExecuteAsync(connection, transaction,
                "DELETE FROM document_processing_manager.processing_result_artifacts WHERE sha256_digest = @digest;",
                "digest", resultDigest!, cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(connection, transaction,
            "DELETE FROM document_processing_manager.processing_units WHERE unit_id = @unit_id;",
            "unit_id", command.UnitId.Value, cancellationToken).ConfigureAwait(false);

        if (deleteSubmission)
        {
            await ExecuteAsync(connection, transaction,
                "DELETE FROM document_processing_manager.custody_events WHERE submission_id = @submission_id;",
                "submission_id", submissionId, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction,
                "DELETE FROM document_processing_manager.document_submissions WHERE submission_id = @submission_id;",
                "submission_id", submissionId, cancellationToken).ConfigureAwait(false);

            if (deleteSourceArtifact)
            {
                await ExecuteAsync(connection, transaction,
                    "DELETE FROM document_processing_manager.source_artifacts WHERE sha256_digest = @digest;",
                    "digest", sourceDigest, cancellationToken).ConfigureAwait(false);
            }
        }

        await ExecuteAsync(connection, transaction,
            "DELETE FROM document_processing_manager.custody_purge_authorizations WHERE purge_id = @purge_id;",
            "purge_id", purgeId, cancellationToken).ConfigureAwait(false);
        await IncrementQueueVersionAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessingUnitCustodyPurge(
            purgeId,
            command.UnitId,
            deleteResultArtifact ? new Sha256Digest(resultDigest!) : null,
            deleteSourceArtifact ? new Sha256Digest(sourceDigest) : null,
            publicationDirectory);
    }

    public async ValueTask<IReadOnlyList<ProcessingUnitCustodyPurge>> GetPendingPurgesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT purge_id, processing_unit_id, result_sha256_digest,
                source_sha256_digest, publication_directory
            FROM document_processing_manager.custody_purge_jobs
            ORDER BY created_at_utc, purge_id;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ProcessingUnitCustodyPurge>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new ProcessingUnitCustodyPurge(
                reader.GetGuid(0),
                new ProcessingUnitId(reader.GetGuid(1)),
                reader.IsDBNull(2) ? null : new Sha256Digest(reader.GetString(2)),
                reader.IsDBNull(3) ? null : new Sha256Digest(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return result;
    }

    public async ValueTask CompletePurgeAsync(
        Guid purgeId,
        CancellationToken cancellationToken = default)
    {
        if (purgeId == Guid.Empty)
        {
            throw new ArgumentException("Purge identifier cannot be empty.", nameof(purgeId));
        }

        await using var command = _dataSource.CreateCommand(
            "DELETE FROM document_processing_manager.custody_purge_jobs WHERE purge_id = @purge_id;");
        command.Parameters.AddWithValue("purge_id", NpgsqlDbType.Uuid, purgeId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<long> LockQueueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT version FROM document_processing_manager.queue_metadata WHERE singleton = TRUE FOR UPDATE;",
            connection,
            transaction);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async ValueTask IncrementQueueVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE document_processing_manager.queue_metadata SET version = version + 1 WHERE singleton = TRUE;",
            connection,
            transaction);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> ExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        Guid submissionId,
        Guid unitId,
        CancellationToken cancellationToken,
        string? digest = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("submission_id", NpgsqlDbType.Uuid, submissionId);
        if (sql.Contains("@unit_id", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, unitId);
        }
        if (digest is not null)
        {
            command.Parameters.AddWithValue("digest", NpgsqlDbType.Text, digest);
        }
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private static async ValueTask<bool> ResultDigestIsSharedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string digest,
        string resultReference,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM document_processing_manager.processing_results
                WHERE result_sha256_digest = @digest
                    AND result_reference <> @result_reference
            );
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("digest", NpgsqlDbType.Text, digest);
        command.Parameters.AddWithValue("result_reference", NpgsqlDbType.Text, resultReference);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private static void AddPlanParameters(
        NpgsqlCommand command,
        Guid purgeId,
        ProcessingUnitId unitId,
        Guid submissionId,
        string sourceDigest,
        string? resultReference,
        string? resultDigest)
    {
        command.Parameters.AddWithValue("purge_id", NpgsqlDbType.Uuid, purgeId);
        command.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, unitId.Value);
        command.Parameters.AddWithValue("submission_id", NpgsqlDbType.Uuid, submissionId);
        command.Parameters.AddWithValue("source_digest", NpgsqlDbType.Text, sourceDigest);
        AddNullableText(command, "result_reference", resultReference);
        AddNullableText(command, "result_digest", resultDigest);
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.AddWithValue(name, NpgsqlDbType.Text, value is null ? DBNull.Value : value);

    private static async ValueTask ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string parameterName,
        object value,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(parameterName, value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
