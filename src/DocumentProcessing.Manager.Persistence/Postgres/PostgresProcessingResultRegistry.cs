using System.Data;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Results;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for immutable idempotent processing-result registration.
/// </summary>
public sealed class PostgresProcessingResultRegistry
    : IProcessingResultRegistryWriter,
      IProcessingResultRegistryReader
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL processing-result registry adapter.
    /// </summary>
    public PostgresProcessingResultRegistry(
        NpgsqlDataSource dataSource)
    {
        _dataSource =
            dataSource ??
            throw new ArgumentNullException(
                nameof(dataSource));
    }

    #endregion

    #region Methods Write

    /// <inheritdoc />
    public async ValueTask<ProcessingResultRegistration> RegisterAsync(
        ProcessingResultRecord result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            result);

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

        await EnsureArtifactAsync(
                connection,
                transaction,
                result.Artifact,
                cancellationToken)
            .ConfigureAwait(false);

        var created =
            await TryInsertResultAsync(
                    connection,
                    transaction,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!created)
        {
            var existing =
                await ReadByUnitAsync(
                        connection,
                        transaction,
                        result.UnitId,
                        cancellationToken)
                    .ConfigureAwait(false) ??
                throw new InvalidOperationException(
                    "A conflicting processing result disappeared inside its registration transaction.");

            if (!EquivalentForIdempotency(
                    existing,
                    result))
            {
                throw new ProcessingResultConflictException(
                    result.UnitId);
            }

            await transaction
                .CommitAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new ProcessingResultRegistration(
                existing,
                created:
                    false);
        }

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessingResultRegistration(
            result,
            created:
                true);
    }

    private static async ValueTask EnsureArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProcessingResultArtifact artifact,
        CancellationToken cancellationToken)
    {
        await using var insert =
            new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.processing_result_artifacts
                    (sha256_digest, byte_length)
                VALUES
                    (@sha256_digest, @byte_length)
                ON CONFLICT (sha256_digest) DO NOTHING;
                """,
                connection,
                transaction);

        insert.Parameters.AddWithValue(
            "sha256_digest",
            NpgsqlDbType.Text,
            artifact.Digest.Value);

        insert.Parameters.AddWithValue(
            "byte_length",
            NpgsqlDbType.Bigint,
            artifact.ByteLength);

        await insert
            .ExecuteNonQueryAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await using var verify =
            new NpgsqlCommand(
                """
                SELECT byte_length
                FROM document_processing_manager.processing_result_artifacts
                WHERE sha256_digest = @sha256_digest
                FOR SHARE;
                """,
                connection,
                transaction);

        verify.Parameters.AddWithValue(
            "sha256_digest",
            NpgsqlDbType.Text,
            artifact.Digest.Value);

        var retainedLength =
            await verify
                .ExecuteScalarAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (retainedLength is not long length ||
            length !=
            artifact.ByteLength)
        {
            throw new ProcessingResultIntegrityException(
                artifact.Digest,
                $"Durable result manifest '{artifact.Digest}' has a conflicting byte length.");
        }
    }

    private static async ValueTask<bool> TryInsertResultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProcessingResultRecord result,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.processing_results
                (
                    result_reference,
                    processing_unit_id,
                    submission_id,
                    result_sha256_digest,
                    media_type,
                    schema_version,
                    produced_at_utc
                )
                VALUES
                (
                    @result_reference,
                    @processing_unit_id,
                    @submission_id,
                    @result_sha256_digest,
                    @media_type,
                    @schema_version,
                    @produced_at_utc
                )
                ON CONFLICT (processing_unit_id) DO NOTHING
                RETURNING result_reference;
                """,
                connection,
                transaction);

        AddResultParameters(
            command,
            result);

        return await command
                .ExecuteScalarAsync(
                    cancellationToken)
                .ConfigureAwait(false) is string;
    }

    #endregion

    #region Methods Read

    /// <inheritdoc />
    public async ValueTask<ProcessingResultRecord?> GetByUnitAsync(
        ProcessingUnitId unitId,
        CancellationToken cancellationToken = default)
    {
        ValidateUnitId(
            unitId);

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await ReadByUnitAsync(
                connection,
                transaction:
                    null,
                unitId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<ProcessingResultRecord?> GetByReferenceAsync(
        string resultReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                resultReference))
        {
            throw new ArgumentException(
                "Processing-result reference cannot be empty.",
                nameof(resultReference));
        }

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using var command =
            CreateReadCommand(
                connection,
                transaction:
                    null,
                "result.result_reference = @result_reference");

        command.Parameters.AddWithValue(
            "result_reference",
            NpgsqlDbType.Text,
            resultReference.Trim());

        return await ReadSingleAsync(
                command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<ProcessingResultRecord?> ReadByUnitAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        ProcessingUnitId unitId,
        CancellationToken cancellationToken)
    {
        await using var command =
            CreateReadCommand(
                connection,
                transaction,
                "result.processing_unit_id = @processing_unit_id");

        command.Parameters.AddWithValue(
            "processing_unit_id",
            NpgsqlDbType.Uuid,
            unitId.Value);

        return await ReadSingleAsync(
                command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static NpgsqlCommand CreateReadCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string predicate) =>
        new(
            $"""
            SELECT
                result.result_reference,
                result.processing_unit_id,
                result.submission_id,
                artifact.sha256_digest,
                artifact.byte_length,
                result.media_type,
                result.schema_version,
                result.produced_at_utc
            FROM document_processing_manager.processing_results AS result
            INNER JOIN document_processing_manager.processing_result_artifacts AS artifact
                ON artifact.sha256_digest = result.result_sha256_digest
            WHERE {predicate};
            """,
            connection,
            transaction);

    private static async ValueTask<ProcessingResultRecord?> ReadSingleAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ? ReadResult(
                reader)
            : null;
    }

    private static ProcessingResultRecord ReadResult(
        NpgsqlDataReader reader) =>
        new(
            reader.GetString(
                0),
            new ProcessingUnitId(
                reader.GetGuid(
                    1)),
            new DocumentSubmissionId(
                reader.GetGuid(
                    2)),
            new ProcessingResultArtifact(
                new Sha256Digest(
                    reader.GetString(
                        3)),
                reader.GetInt64(
                    4)),
            reader.GetString(
                5),
            reader.GetString(
                6),
            reader.GetFieldValue<DateTimeOffset>(
                7));

    #endregion

    #region Methods Validation

    private static bool EquivalentForIdempotency(
        ProcessingResultRecord existing,
        ProcessingResultRecord requested) =>
        existing.UnitId ==
            requested.UnitId &&
        existing.SubmissionId ==
            requested.SubmissionId &&
        existing.Artifact ==
            requested.Artifact &&
        string.Equals(
            existing.MediaType,
            requested.MediaType,
            StringComparison.Ordinal) &&
        string.Equals(
            existing.SchemaVersion,
            requested.SchemaVersion,
            StringComparison.Ordinal);

    private static void ValidateUnitId(
        ProcessingUnitId unitId)
    {
        if (unitId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-result unit identifier cannot be empty.",
                nameof(unitId));
        }
    }

    private static void AddResultParameters(
        NpgsqlCommand command,
        ProcessingResultRecord result)
    {
        command.Parameters.AddWithValue(
            "result_reference",
            NpgsqlDbType.Text,
            result.ResultReference);

        command.Parameters.AddWithValue(
            "processing_unit_id",
            NpgsqlDbType.Uuid,
            result.UnitId.Value);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            result.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "result_sha256_digest",
            NpgsqlDbType.Text,
            result.Artifact.Digest.Value);

        command.Parameters.AddWithValue(
            "media_type",
            NpgsqlDbType.Text,
            result.MediaType);

        command.Parameters.AddWithValue(
            "schema_version",
            NpgsqlDbType.Text,
            result.SchemaVersion);

        command.Parameters.AddWithValue(
            "produced_at_utc",
            NpgsqlDbType.TimestampTz,
            result.ProducedAtUtc);
    }

    #endregion
}
