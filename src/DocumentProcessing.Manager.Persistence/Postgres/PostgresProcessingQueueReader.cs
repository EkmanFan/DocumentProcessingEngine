using System.Data;
using DocumentProcessing.Manager.History;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL read adapter for consistent versioned queue snapshots.
/// </summary>
public sealed class PostgresProcessingQueueReader
    : IProcessingQueueReader,
      IProcessingHistoryReader
{
    #region Variables and Constants

    private const string
        SelectItemsSql =
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
                processing_unit.released_at_utc,
                submission.original_file_name
            FROM document_processing_manager.processing_units AS processing_unit
            INNER JOIN document_processing_manager.document_submissions AS submission
                ON submission.submission_id = processing_unit.submission_id
            """;

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
        return await GetSnapshotAsync(
                completedSinceUtc:
                    null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<ProcessingQueueSnapshot> GetRecentSnapshotAsync(
        DateTimeOffset completedSinceUtc,
        CancellationToken cancellationToken = default)
    {
        if (completedSinceUtc ==
            default)
        {
            throw new ArgumentException(
                "Recent-completion cutoff is required.",
                nameof(completedSinceUtc));
        }

        return await GetSnapshotAsync(
                completedSinceUtc.ToUniversalTime(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<ProcessingArchivePage> SearchArchiveAsync(
        ProcessingArchiveQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            query);

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

        var whereClause =
            BuildArchiveWhereClause(
                query);

        await using var countCommand =
            new NpgsqlCommand(
                $"""
                SELECT count(*)
                FROM document_processing_manager.processing_units AS processing_unit
                INNER JOIN document_processing_manager.document_submissions AS submission
                    ON submission.submission_id = processing_unit.submission_id
                {whereClause};
                """,
                connection,
                transaction);

        AddArchiveParameters(
            countCommand,
            query);

        var totalCount =
            Convert.ToInt64(
                await countCommand
                    .ExecuteScalarAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        await using var itemCommand =
            new NpgsqlCommand(
                $"""
                {SelectItemsSql}
                {whereClause}
                ORDER BY {GetArchiveOrderByClause(query.Sort)}
                OFFSET @offset
                LIMIT @limit;
                """,
                connection,
                transaction);

        AddArchiveParameters(
            itemCommand,
            query);

        itemCommand.Parameters.AddWithValue(
            "offset",
            NpgsqlDbType.Integer,
            query.Offset);

        itemCommand.Parameters.AddWithValue(
            "limit",
            NpgsqlDbType.Integer,
            query.Limit);

        var items =
            await ReadItemsAsync(
                    itemCommand,
                    cancellationToken)
                .ConfigureAwait(false);

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessingArchivePage(
            totalCount,
            query.Offset,
            query.Limit,
            items);
    }

    private async ValueTask<ProcessingQueueSnapshot> GetSnapshotAsync(
        DateTimeOffset? completedSinceUtc,
        CancellationToken cancellationToken)
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
                $"""
                {SelectItemsSql}
                {(completedSinceUtc.HasValue
                    ? "WHERE processing_unit.status < 2 OR (processing_unit.hidden_at_utc IS NULL AND processing_unit.updated_at_utc >= @completed_since_utc)"
                    : "WHERE processing_unit.status < 2 OR processing_unit.hidden_at_utc IS NULL")}
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

        if (completedSinceUtc.HasValue)
        {
            itemCommand.Parameters.AddWithValue(
                "completed_since_utc",
                NpgsqlDbType.TimestampTz,
                completedSinceUtc.Value);
        }

        var items =
            await ReadItemsAsync(
                    itemCommand,
                    cancellationToken)
                .ConfigureAwait(false);

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessingQueueSnapshot(
            version,
            items);
    }

    private static async ValueTask<IReadOnlyList<ProcessingQueueItemSnapshot>> ReadItemsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var items =
            new List<ProcessingQueueItemSnapshot>();

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        while (await reader
                   .ReadAsync(
                       cancellationToken)
                   .ConfigureAwait(false))
        {
            items.Add(
                ReadItem(
                    reader));
        }

        return items;
    }

    private static string BuildArchiveWhereClause(
        ProcessingArchiveQuery query)
    {
        var predicates =
            new List<string>
            {
                "processing_unit.status IN (2, 3)",
                "processing_unit.hidden_at_utc IS NULL",
                "processing_unit.updated_at_utc < @archived_before_utc"
            };

        if (query.TitleContains is not null)
        {
            predicates.Add(
                "strpos(lower(submission.original_file_name), lower(@title_contains)) > 0");
        }

        if (query.CompletedFromUtc.HasValue)
        {
            predicates.Add(
                "processing_unit.updated_at_utc >= @completed_from_utc");
        }

        if (query.CompletedBeforeUtc.HasValue)
        {
            predicates.Add(
                "processing_unit.updated_at_utc < @completed_before_utc");
        }

        return
            $"WHERE {string.Join(" AND ", predicates)}";
    }

    private static void AddArchiveParameters(
        NpgsqlCommand command,
        ProcessingArchiveQuery query)
    {
        command.Parameters.AddWithValue(
            "archived_before_utc",
            NpgsqlDbType.TimestampTz,
            query.ArchivedBeforeUtc);

        if (query.TitleContains is not null)
        {
            command.Parameters.AddWithValue(
                "title_contains",
                NpgsqlDbType.Text,
                query.TitleContains);
        }

        if (query.CompletedFromUtc.HasValue)
        {
            command.Parameters.AddWithValue(
                "completed_from_utc",
                NpgsqlDbType.TimestampTz,
                query.CompletedFromUtc.Value);
        }

        if (query.CompletedBeforeUtc.HasValue)
        {
            command.Parameters.AddWithValue(
                "completed_before_utc",
                NpgsqlDbType.TimestampTz,
                query.CompletedBeforeUtc.Value);
        }
    }

    private static string GetArchiveOrderByClause(
        ProcessingArchiveSort sort) =>
        sort switch
        {
            ProcessingArchiveSort.CompletedNewest =>
                "processing_unit.updated_at_utc DESC, processing_unit.unit_id",
            ProcessingArchiveSort.CompletedOldest =>
                "processing_unit.updated_at_utc, processing_unit.unit_id",
            ProcessingArchiveSort.TitleAscending =>
                "lower(submission.original_file_name), processing_unit.updated_at_utc DESC, processing_unit.unit_id",
            ProcessingArchiveSort.TitleDescending =>
                "lower(submission.original_file_name) DESC, processing_unit.updated_at_utc DESC, processing_unit.unit_id",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(sort),
                    sort,
                    "Unknown archive sort order.")
        };

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
                16),
            (ProcessingUnitStatus)reader.GetInt16(
                7),
            reader.IsDBNull(
                15)
                ? ProcessingUnitDispatchState.Shelved
                : ProcessingUnitDispatchState.Ready,
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
