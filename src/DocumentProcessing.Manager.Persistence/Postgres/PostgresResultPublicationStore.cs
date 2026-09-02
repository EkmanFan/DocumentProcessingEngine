using System.Data;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Queue;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for durable at-least-once result publication.
/// </summary>
public sealed class PostgresResultPublicationStore
    : IResultPublicationStore
{
    #region Variables and Constants

    private readonly NpgsqlDataSource _dataSource;

    #endregion

    #region ctor

    /// <summary>Creates the PostgreSQL result-publication adapter.</summary>
    public PostgresResultPublicationStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask<ResultAvailableDelivery?> ClaimNextAsync(
        string consumerId,
        DateTimeOffset observedAtUtc,
        DateTimeOffset claimExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        consumerId = ValidateConsumerId(consumerId);
        _ = PostgresLeaseDuration.Calculate(
            observedAtUtc,
            claimExpiresAtUtc,
            nameof(claimExpiresAtUtc));

        var claimToken = Guid.NewGuid();

        await using var connection = await _dataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        await using (var materialize = new NpgsqlCommand(
                         """
                         INSERT INTO document_processing_manager.result_consumer_deliveries
                             (consumer_id, result_reference)
                         SELECT @consumer_id, event.result_reference
                         FROM document_processing_manager.result_available_events AS event
                         ON CONFLICT (consumer_id, result_reference) DO NOTHING;
                         """,
                         connection,
                         transaction))
        {
            materialize.Parameters.AddWithValue("consumer_id", NpgsqlDbType.Text, consumerId);
            await materialize.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        string? resultReference;
        await using (var claim = new NpgsqlCommand(
                         """
                         WITH candidate AS
                         (
                             SELECT delivery.result_reference
                             FROM document_processing_manager.result_consumer_deliveries AS delivery
                             INNER JOIN document_processing_manager.result_available_events AS event
                                 ON event.result_reference = delivery.result_reference
                             WHERE delivery.consumer_id = @consumer_id
                                 AND delivery.acknowledged_at_utc IS NULL
                                 AND
                                 (
                                     delivery.claim_token IS NULL
                                     OR delivery.claim_expires_at_utc <= @observed_at_utc
                                 )
                             ORDER BY event.available_at_utc, delivery.result_reference
                             FOR UPDATE OF delivery SKIP LOCKED
                             LIMIT 1
                         )
                         UPDATE document_processing_manager.result_consumer_deliveries AS delivery
                         SET claim_token = @claim_token,
                             claim_expires_at_utc = @claim_expires_at_utc
                         FROM candidate
                         WHERE delivery.consumer_id = @consumer_id
                             AND delivery.result_reference = candidate.result_reference
                         RETURNING delivery.result_reference;
                         """,
                         connection,
                         transaction))
        {
            claim.Parameters.AddWithValue("consumer_id", NpgsqlDbType.Text, consumerId);
            claim.Parameters.AddWithValue("observed_at_utc", NpgsqlDbType.TimestampTz, observedAtUtc.ToUniversalTime());
            claim.Parameters.AddWithValue("claim_token", NpgsqlDbType.Uuid, claimToken);
            claim.Parameters.AddWithValue("claim_expires_at_utc", NpgsqlDbType.TimestampTz, claimExpiresAtUtc.ToUniversalTime());
            resultReference = await claim.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }

        if (resultReference is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var delivery = await ReadDeliveryAsync(
                connection,
                transaction,
                resultReference,
                claimToken,
                claimExpiresAtUtc,
                cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return delivery;
    }

    /// <inheritdoc />
    public async ValueTask<bool> AcknowledgeAsync(
        string consumerId,
        string resultReference,
        Guid claimToken,
        DateTimeOffset acknowledgedAtUtc,
        CancellationToken cancellationToken = default)
    {
        consumerId = ValidateConsumerId(consumerId);
        if (string.IsNullOrWhiteSpace(resultReference))
        {
            throw new ArgumentException("Result reference cannot be empty.", nameof(resultReference));
        }

        if (claimToken == Guid.Empty)
        {
            throw new ArgumentException("Claim token cannot be empty.", nameof(claimToken));
        }

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE document_processing_manager.result_consumer_deliveries
            SET acknowledged_at_utc = COALESCE(
                    acknowledged_at_utc,
                    @acknowledged_at_utc)
            WHERE consumer_id = @consumer_id
                AND result_reference = @result_reference
                AND claim_token = @claim_token
                AND
                (
                    acknowledged_at_utc IS NOT NULL
                    OR claim_expires_at_utc > @acknowledged_at_utc
                );
            """);
        command.Parameters.AddWithValue("consumer_id", NpgsqlDbType.Text, consumerId);
        command.Parameters.AddWithValue("result_reference", NpgsqlDbType.Text, resultReference.Trim());
        command.Parameters.AddWithValue("claim_token", NpgsqlDbType.Uuid, claimToken);
        command.Parameters.AddWithValue("acknowledged_at_utc", NpgsqlDbType.TimestampTz, acknowledgedAtUtc.ToUniversalTime());

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <inheritdoc />
    public async ValueTask<bool> OwnsClaimAsync(
        string consumerId,
        string resultReference,
        Guid claimToken,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        consumerId = ValidateConsumerId(consumerId);
        if (string.IsNullOrWhiteSpace(resultReference))
        {
            throw new ArgumentException("Result reference cannot be empty.", nameof(resultReference));
        }

        if (claimToken == Guid.Empty)
        {
            throw new ArgumentException("Claim token cannot be empty.", nameof(claimToken));
        }

        await using var command = _dataSource.CreateCommand(
            """
            SELECT EXISTS
            (
                SELECT 1
                FROM document_processing_manager.result_consumer_deliveries
                WHERE consumer_id = @consumer_id
                    AND result_reference = @result_reference
                    AND claim_token = @claim_token
                    AND claim_expires_at_utc > @observed_at_utc
                    AND acknowledged_at_utc IS NULL
            );
            """);
        command.Parameters.AddWithValue("consumer_id", NpgsqlDbType.Text, consumerId);
        command.Parameters.AddWithValue("result_reference", NpgsqlDbType.Text, resultReference.Trim());
        command.Parameters.AddWithValue("claim_token", NpgsqlDbType.Uuid, claimToken);
        command.Parameters.AddWithValue("observed_at_utc", NpgsqlDbType.TimestampTz, observedAtUtc.ToUniversalTime());

        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private static async ValueTask<ResultAvailableDelivery> ReadDeliveryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string resultReference,
        Guid claimToken,
        DateTimeOffset claimExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                result.result_reference,
                result.submission_id,
                result.processing_unit_id,
                unit.scope_kind,
                unit.start_physical_page_number,
                unit.end_physical_page_number,
                unit.scope_title,
                result.schema_version,
                result.media_type,
                artifact.byte_length,
                result.result_sha256_digest,
                event.available_at_utc,
                unit.start_content_unit_index,
                unit.start_content_unit_id,
                unit.end_content_unit_index,
                unit.end_content_unit_id
            FROM document_processing_manager.processing_results AS result
            INNER JOIN document_processing_manager.processing_result_artifacts AS artifact
                ON artifact.sha256_digest = result.result_sha256_digest
            INNER JOIN document_processing_manager.processing_units AS unit
                ON unit.unit_id = result.processing_unit_id
            INNER JOIN document_processing_manager.result_available_events AS event
                ON event.result_reference = result.result_reference
            WHERE result.result_reference = @result_reference;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("result_reference", NpgsqlDbType.Text, resultReference);

        string retainedResultReference;
        Guid submissionId;
        Guid processingUnitId;
        ProcessingUnitScope scope;
        string schemaVersion;
        string mediaType;
        long byteLength;
        string digest;
        DateTimeOffset availableAtUtc;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("A claimed result publication disappeared inside its transaction.");
            }

            retainedResultReference = reader.GetString(0);
            submissionId = reader.GetGuid(1);
            processingUnitId = reader.GetGuid(2);
            scope = PostgresProcessingUnitScopeMapper.ReadScope(
                reader,
                kindOrdinal: 3,
                startPageOrdinal: 4,
                endPageOrdinal: 5,
                titleOrdinal: 6,
                startContentUnitIndexOrdinal: 12,
                startContentUnitIdOrdinal: 13,
                endContentUnitIndexOrdinal: 14,
                endContentUnitIdOrdinal: 15);
            schemaVersion = reader.GetString(7);
            mediaType = reader.GetString(8);
            byteLength = reader.GetInt64(9);
            digest = reader.GetString(10);
            availableAtUtc = reader.GetFieldValue<DateTimeOffset>(11);
        }

        var manifest =
            await PostgresSubmissionPublicationManifestStore.ReadLatestAsync(
                    connection,
                    transaction,
                    submissionId,
                    cancellationToken)
                .ConfigureAwait(false);

        return new ResultAvailableDelivery(
            retainedResultReference,
            new DocumentSubmissionId(submissionId),
            new ProcessingUnitId(processingUnitId),
            scope,
            schemaVersion,
            mediaType,
            byteLength,
            new Sha256Digest(digest),
            availableAtUtc,
            claimToken,
            claimExpiresAtUtc,
            manifest);
    }

    private static string ValidateConsumerId(string consumerId)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            throw new ArgumentException("Consumer identifier cannot be empty.", nameof(consumerId));
        }

        var normalized = consumerId.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentException("Consumer identifier cannot exceed 128 characters.", nameof(consumerId));
        }

        return normalized;
    }

    #endregion
}
