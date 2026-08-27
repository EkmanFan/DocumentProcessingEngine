using System.Data;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using Npgsql;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL read adapter for consistent versioned queue snapshots.
/// </summary>
public sealed class PostgresProcessingQueueReader
    : IProcessingQueueReader
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL processing-queue reader.
    /// </summary>
    public PostgresProcessingQueueReader(
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
    public async ValueTask<ProcessingQueueSnapshot> GetSnapshotAsync(
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
                    IsolationLevel.RepeatableRead,
                    cancellationToken)
                .ConfigureAwait(false);

        await using var versionCommand =
            new NpgsqlCommand(
                """
                SELECT version
                FROM document_processing_manager.queue_metadata
                WHERE singleton = TRUE;
                """,
                connection,
                transaction);

        var version =
            Convert.ToInt64(
                await versionCommand
                    .ExecuteScalarAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        await using var itemCommand =
            new NpgsqlCommand(
                """
                SELECT
                    processing_unit.unit_id,
                    processing_unit.submission_id,
                    processing_unit.scope_kind,
                    processing_unit.start_physical_page_number,
                    processing_unit.end_physical_page_number,
                    processing_unit.scope_title,
                    processing_unit.attempt_number,
                    processing_unit.status,
                    processing_unit.queue_position,
                    processing_unit.result_reference,
                    processing_unit.failure_code,
                    processing_unit.failure_message,
                    processing_unit.interruption_reason,
                    processing_unit.created_at_utc,
                    processing_unit.updated_at_utc,
                    submission.original_file_name
                FROM document_processing_manager.processing_units AS processing_unit
                INNER JOIN document_processing_manager.document_submissions AS submission
                    ON submission.submission_id = processing_unit.submission_id
                ORDER BY
                    CASE processing_unit.status
                        WHEN 0 THEN 0
                        WHEN 1 THEN 1
                        ELSE 2
                    END,
                    processing_unit.queue_position NULLS LAST,
                    processing_unit.created_at_utc,
                    processing_unit.unit_id;
                """,
                connection,
                transaction);

        var items =
            new List<ProcessingQueueItemSnapshot>();

        await using (var reader =
                     await itemCommand
                         .ExecuteReaderAsync(
                             cancellationToken)
                         .ConfigureAwait(false))
        {
            while (await reader
                       .ReadAsync(
                           cancellationToken)
                       .ConfigureAwait(false))
            {
                items.Add(
                    ReadItem(
                        reader));
            }
        }

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessingQueueSnapshot(
            version,
            items);
    }

    private static ProcessingQueueItemSnapshot ReadItem(
        NpgsqlDataReader reader)
    {
        var scope =
            ReadScope(
                reader);

        var failure =
            reader.IsDBNull(
                10)
                ? null
                : new ProcessingFailure(
                    reader.GetString(
                        10),
                    reader.GetString(
                        11));

        return new ProcessingQueueItemSnapshot(
            new ProcessingWorkItem(
                new ProcessingUnitId(
                    reader.GetGuid(
                        0)),
                new DocumentSubmissionId(
                    reader.GetGuid(
                        1)),
                scope,
                reader.GetInt32(
                    6)),
            reader.GetString(
                15),
            (ProcessingUnitStatus)reader.GetInt16(
                7),
            reader.IsDBNull(
                8)
                ? null
                : reader.GetInt64(
                    8),
            reader.IsDBNull(
                9)
                ? null
                : reader.GetString(
                    9),
            failure,
            reader.IsDBNull(
                12)
                ? null
                : (ProcessingInterruptionReason)reader.GetInt16(
                    12),
            reader.GetFieldValue<DateTimeOffset>(
                13),
            reader.GetFieldValue<DateTimeOffset>(
                14));
    }

    private static ProcessingUnitScope ReadScope(
        NpgsqlDataReader reader) =>
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
            var value =>
                throw new InvalidOperationException(
                    $"Unknown durable processing-unit scope kind '{value}'.")
        };

    #endregion
}
