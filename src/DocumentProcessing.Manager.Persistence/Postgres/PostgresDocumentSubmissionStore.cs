using System.Data;
using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Queue;
using DocumentProcessing.Manager.Submissions;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

/// <summary>
/// PostgreSQL adapter for immutable submissions and atomic processing intake.
/// </summary>
public sealed class PostgresDocumentSubmissionStore
    : IDocumentSubmissionWriter,
      IDocumentSubmissionReader
{
    #region Variables and Constants

    private readonly NpgsqlDataSource
        _dataSource;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the PostgreSQL document-submission adapter.
    /// </summary>
    public PostgresDocumentSubmissionStore(
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
    public async ValueTask<DocumentSubmissionRegistration>
        RegisterAsync(
        DocumentSubmission submission,
        IReadOnlyCollection<ProcessingUnitIntake> processingUnits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            submission);

        ArgumentNullException.ThrowIfNull(
            processingUnits);

        var units =
            processingUnits.ToArray();

        ValidateUnits(
            submission,
            units);

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
                submission.SourceArtifact,
                cancellationToken)
            .ConfigureAwait(false);

        var created =
            await TryInsertSubmissionAsync(
                    connection,
                    transaction,
                    submission,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!created)
        {
            var existing =
                await ReadAsync(
                        connection,
                        transaction,
                        submission.SubmissionId,
                        cancellationToken)
                    .ConfigureAwait(false) ??
                throw new InvalidOperationException(
                    "A conflicting submission disappeared inside its registration transaction.");

            if (!EquivalentForIdempotency(
                    existing,
                    submission))
            {
                throw new DocumentSubmissionConflictException(
                    submission.SubmissionId);
            }

            var existingUnits =
                await ReadProcessingUnitsAsync(
                        connection,
                        transaction,
                        submission.SubmissionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!EquivalentInitialPlan(
                    existingUnits,
                    units.Select(
                            unit =>
                                unit.WorkItem)
                        .ToArray()))
            {
                throw new DocumentSubmissionConflictException(
                    submission.SubmissionId);
            }

            await transaction
                .CommitAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new DocumentSubmissionRegistration(
                existing,
                existingUnits.Select(
                    unit =>
                        unit.UnitId),
                created:
                    false);
        }

        await InsertCustodyEventAsync(
                connection,
                transaction,
                submission,
                cancellationToken)
            .ConfigureAwait(false);

        var queueTail =
            await LockQueueAndReadTailAsync(
                    connection,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);

        for (var index =
             0;
             index <
             units.Length;
             index++)
        {
            await InsertProcessingUnitAsync(
                    connection,
                    transaction,
                    units[index],
                    submissionUnitOrdinal:
                        index +
                        1,
                    queuePosition:
                        checked(
                        queueTail +
                        index +
                        1),
                    cancellationToken:
                        cancellationToken)
                .ConfigureAwait(false);
        }

        await IncrementQueueVersionAsync(
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);

        await transaction
            .CommitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        return new DocumentSubmissionRegistration(
            submission,
            units.Select(
                unit =>
                    unit.WorkItem.UnitId),
            created:
                true);
    }

    #endregion

    #region Methods Read

    /// <inheritdoc />
    public async ValueTask<DocumentSubmission?> GetAsync(
        DocumentSubmissionId submissionId,
        CancellationToken cancellationToken = default)
    {
        if (submissionId.Value ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Document submission identifier cannot be empty.",
                nameof(submissionId));
        }

        await using var connection =
            await _dataSource
                .OpenConnectionAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await ReadAsync(
                connection,
                transaction:
                    null,
                submissionId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<DocumentSubmission?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        DocumentSubmissionId submissionId,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT
                    submission.submission_id,
                    artifact.sha256_digest,
                    artifact.byte_length,
                    submission.original_file_name,
                    submission.declared_media_type,
                    submission.source_origin,
                    submission.submitted_at_utc
                FROM document_processing_manager.document_submissions AS submission
                INNER JOIN document_processing_manager.source_artifacts AS artifact
                    ON artifact.sha256_digest = submission.source_sha256_digest
                WHERE submission.submission_id = @submission_id;
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            submissionId.Value);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await reader
                .ReadAsync(
                    cancellationToken)
                .ConfigureAwait(false)
            ? ReadSubmission(
                reader)
            : null;
    }

    #endregion

    #region Methods SQL Insert

    private static async ValueTask EnsureArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SourceArtifact artifact,
        CancellationToken cancellationToken)
    {
        await using var insert =
            new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.source_artifacts
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
                FROM document_processing_manager.source_artifacts
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
            throw new SourceArtifactIntegrityException(
                artifact.Digest,
                $"Durable source manifest '{artifact.Digest}' has a conflicting byte length.");
        }
    }

    private static async ValueTask<bool> TryInsertSubmissionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DocumentSubmission submission,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.document_submissions
                (
                    submission_id,
                    source_sha256_digest,
                    original_file_name,
                    declared_media_type,
                    source_origin,
                    submitted_at_utc
                )
                VALUES
                (
                    @submission_id,
                    @source_sha256_digest,
                    @original_file_name,
                    @declared_media_type,
                    @source_origin,
                    @submitted_at_utc
                )
                ON CONFLICT (submission_id) DO NOTHING
                RETURNING submission_id;
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            submission.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "source_sha256_digest",
            NpgsqlDbType.Text,
            submission.SourceArtifact.Digest.Value);

        command.Parameters.AddWithValue(
            "original_file_name",
            NpgsqlDbType.Text,
            submission.OriginalFileName);

        AddNullableTextParameter(
            command,
            "declared_media_type",
            submission.DeclaredMediaType);

        AddNullableTextParameter(
            command,
            "source_origin",
            submission.SourceOrigin);

        command.Parameters.AddWithValue(
            "submitted_at_utc",
            NpgsqlDbType.TimestampTz,
            submission.SubmittedAtUtc);

        return await command
                .ExecuteScalarAsync(
                    cancellationToken)
                .ConfigureAwait(false) is Guid;
    }

    private static async ValueTask InsertCustodyEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DocumentSubmission submission,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.custody_events
                (
                    submission_id,
                    event_kind,
                    source_sha256_digest,
                    occurred_at_utc
                )
                VALUES
                (
                    @submission_id,
                    @event_kind,
                    @source_sha256_digest,
                    @occurred_at_utc
                );
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            submission.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "event_kind",
            NpgsqlDbType.Smallint,
            (short)CustodyEventKind.SourceRegistered);

        command.Parameters.AddWithValue(
            "source_sha256_digest",
            NpgsqlDbType.Text,
            submission.SourceArtifact.Digest.Value);

        command.Parameters.AddWithValue(
            "occurred_at_utc",
            NpgsqlDbType.TimestampTz,
            submission.SubmittedAtUtc);

        await command
            .ExecuteNonQueryAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask InsertProcessingUnitAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProcessingUnitIntake intake,
        int submissionUnitOrdinal,
        long queuePosition,
        CancellationToken cancellationToken)
    {
        var processingUnit =
            intake.WorkItem;

        var pageRange =
            processingUnit.Scope as ProcessingUnitScope.PageRange;

        await using var command =
            new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.processing_units
                (
                    unit_id,
                    submission_id,
                    scope_kind,
                    start_physical_page_number,
                    end_physical_page_number,
                    scope_title,
                    attempt_number,
                    submission_unit_ordinal,
                    status,
                    queue_position,
                    released_at_utc
                )
                VALUES
                (
                    @unit_id,
                    @submission_id,
                    @scope_kind,
                    @start_page,
                    @end_page,
                    @scope_title,
                    @attempt_number,
                    @submission_unit_ordinal,
                    0,
                    @queue_position,
                    CASE
                        WHEN @is_ready THEN clock_timestamp()
                        ELSE NULL
                    END
                );
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "unit_id",
            NpgsqlDbType.Uuid,
            processingUnit.UnitId.Value);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            processingUnit.SubmissionId.Value);

        command.Parameters.AddWithValue(
            "scope_kind",
            NpgsqlDbType.Smallint,
            pageRange is null
                ? (short)0
                : (short)1);

        command.Parameters.AddWithValue(
            "start_page",
            NpgsqlDbType.Integer,
            pageRange is null
                ? DBNull.Value
                : pageRange.StartPhysicalPageNumber);

        command.Parameters.AddWithValue(
            "end_page",
            NpgsqlDbType.Integer,
            pageRange is null
                ? DBNull.Value
                : pageRange.EndPhysicalPageNumber);

        command.Parameters.AddWithValue(
            "scope_title",
            NpgsqlDbType.Text,
            pageRange is null
                ? DBNull.Value
                : pageRange.Title);

        command.Parameters.AddWithValue(
            "attempt_number",
            NpgsqlDbType.Integer,
            processingUnit.AttemptNumber);

        command.Parameters.AddWithValue(
            "submission_unit_ordinal",
            NpgsqlDbType.Integer,
            submissionUnitOrdinal);

        command.Parameters.AddWithValue(
            "queue_position",
            NpgsqlDbType.Bigint,
            queuePosition);

        command.Parameters.AddWithValue(
            "is_ready",
            NpgsqlDbType.Boolean,
            intake.DispatchState ==
            ProcessingUnitDispatchState.Ready);

        await command
            .ExecuteNonQueryAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Methods SQL Queue

    private static async ValueTask<long> LockQueueAndReadTailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using (var queueLock =
                     new NpgsqlCommand(
                         """
                         SELECT version
                         FROM document_processing_manager.queue_metadata
                         WHERE singleton = TRUE
                         FOR UPDATE;
                         """,
                         connection,
                         transaction))
        {
            if (await queueLock
                    .ExecuteScalarAsync(
                        cancellationToken)
                    .ConfigureAwait(false) is not long)
            {
                throw new InvalidOperationException(
                    "The PostgreSQL Manager schema has not been initialized.");
            }
        }

        await using var queueTail =
            new NpgsqlCommand(
                """
                SELECT COALESCE(MAX(queue_position), 0)
                FROM document_processing_manager.processing_units
                WHERE status = 0;
                """,
                connection,
                transaction);

        return Convert.ToInt64(
            await queueTail
                .ExecuteScalarAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private static async ValueTask IncrementQueueVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                UPDATE document_processing_manager.queue_metadata
                SET version = version + 1
                WHERE singleton = TRUE;
                """,
                connection,
                transaction);

        if (await command
                .ExecuteNonQueryAsync(
                    cancellationToken)
                .ConfigureAwait(false) !=
            1)
        {
            throw new InvalidOperationException(
                "The PostgreSQL Manager schema has not been initialized.");
        }
    }

    #endregion

    #region Methods SQL Read

    private static async ValueTask<IReadOnlyList<ProcessingWorkItem>>
        ReadProcessingUnitsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DocumentSubmissionId submissionId,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT
                    unit_id,
                    scope_kind,
                    start_physical_page_number,
                    end_physical_page_number,
                    scope_title,
                    attempt_number
                FROM document_processing_manager.processing_units
                WHERE submission_id = @submission_id
                ORDER BY
                    submission_unit_ordinal NULLS LAST,
                    created_at_utc,
                    unit_id;
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "submission_id",
            NpgsqlDbType.Uuid,
            submissionId.Value);

        await using var reader =
            await command
                .ExecuteReaderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        var units =
            new List<ProcessingWorkItem>();

        while (await reader
                   .ReadAsync(
                       cancellationToken)
                   .ConfigureAwait(false))
        {
            ProcessingUnitScope scope =
                reader.GetInt16(
                    1) switch
                {
                    0 =>
                        new ProcessingUnitScope.WholeDocument(),
                    1 =>
                        new ProcessingUnitScope.PageRange(
                            reader.GetInt32(
                                2),
                            reader.GetInt32(
                                3),
                            reader.GetString(
                                4)),
                    var scopeKind =>
                        throw new InvalidOperationException(
                            $"Unknown durable processing scope kind '{scopeKind}'.")
                };

            units.Add(
                new ProcessingWorkItem(
                    new ProcessingUnitId(
                        reader.GetGuid(
                            0)),
                    submissionId,
                    scope,
                    reader.GetInt32(
                        5)));
        }

        return units.Count >
               0
            ? units
            : throw new InvalidOperationException(
                "A durable document submission has no associated processing unit.");
    }

    private static DocumentSubmission ReadSubmission(
        NpgsqlDataReader reader) =>
        new(
            new DocumentSubmissionId(
                reader.GetGuid(
                    0)),
            new SourceArtifact(
                new Sha256Digest(
                    reader.GetString(
                        1)),
                reader.GetInt64(
                    2)),
            reader.GetString(
                3),
            reader.IsDBNull(
                4)
                ? null
                : reader.GetString(
                    4),
            reader.IsDBNull(
                5)
                ? null
                : reader.GetString(
                    5),
            reader.GetFieldValue<DateTimeOffset>(
                6));

    #endregion

    #region Methods Validation

    private static void ValidateUnits(
        DocumentSubmission submission,
        IReadOnlyCollection<ProcessingUnitIntake> units)
    {
        if (units.Count ==
                0 ||
            units.Any(
                unit =>
                    unit is null ||
                    unit.WorkItem.UnitId.Value ==
                    Guid.Empty ||
                    unit.WorkItem.AttemptNumber !=
                    1 ||
                    unit.WorkItem.SubmissionId !=
                    submission.SubmissionId ||
                    !Enum.IsDefined(
                        unit.DispatchState)) ||
            units.Select(
                    unit =>
                        unit.WorkItem.UnitId)
                .Distinct()
                .Count() !=
            units.Count ||
            !HasValidInitialScopeShape(
                units.Select(
                        unit =>
                            unit.WorkItem)
                    .ToArray()))
        {
            throw new ArgumentException(
                "Initial processing units must be distinct attempt-one units for one coherent whole-document or page-range plan.",
                nameof(units));
        }
    }

    private static bool HasValidInitialScopeShape(
        IReadOnlyCollection<ProcessingWorkItem> units) =>
        (units.Count ==
             1 &&
         units.Single().Scope is ProcessingUnitScope.WholeDocument) ||
        units.All(
            unit =>
                unit.Scope is ProcessingUnitScope.PageRange);

    private static bool EquivalentForIdempotency(
        DocumentSubmission existing,
        DocumentSubmission requested) =>
        existing.SubmissionId ==
            requested.SubmissionId &&
        existing.SourceArtifact ==
            requested.SourceArtifact &&
        string.Equals(
            existing.OriginalFileName,
            requested.OriginalFileName,
            StringComparison.Ordinal) &&
        string.Equals(
            existing.DeclaredMediaType,
            requested.DeclaredMediaType,
            StringComparison.Ordinal) &&
        string.Equals(
            existing.SourceOrigin,
            requested.SourceOrigin,
            StringComparison.Ordinal);

    private static bool EquivalentInitialPlan(
        IReadOnlyList<ProcessingWorkItem> existing,
        IReadOnlyList<ProcessingWorkItem> requested) =>
        existing.Count ==
            requested.Count &&
        existing
            .Select(
                unit =>
                    unit.Scope)
            .SequenceEqual(
                requested.Select(
                    unit =>
                        unit.Scope));

    private static void AddNullableTextParameter(
        NpgsqlCommand command,
        string parameterName,
        string? value) =>
        command.Parameters.AddWithValue(
            parameterName,
            NpgsqlDbType.Text,
            value is null
                ? DBNull.Value
                : value);

    #endregion
}
