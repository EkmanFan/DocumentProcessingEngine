using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Queue;
using Npgsql;
using NpgsqlTypes;

namespace DocumentProcessing.Manager.Persistence.Postgres;

internal static class PostgresSubmissionPublicationManifestStore
{
    public static async ValueTask AppendAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid submissionId,
        IReadOnlyList<ProcessingWorkItem> units,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(units);

        if (units.Count == 0)
        {
            throw new ArgumentException(
                "A submission manifest requires at least one processing unit.",
                nameof(units));
        }

        await using (var submissionLock = new NpgsqlCommand(
                         "SELECT pg_advisory_xact_lock(hashtextextended(@submission_id::text, 0));",
                         connection,
                         transaction))
        {
            submissionLock.Parameters.AddWithValue(
                "submission_id",
                NpgsqlDbType.Uuid,
                submissionId);
            await submissionLock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        int revision;

        await using (var insertManifest = new NpgsqlCommand(
                         """
                         INSERT INTO document_processing_manager.submission_publication_manifests
                         (
                             submission_id, revision, source_sha256_digest,
                             original_file_name, finalized_at_utc
                         )
                         SELECT submission.submission_id,
                             COALESCE(
                                 (
                                     SELECT MAX(manifest.revision)
                                     FROM document_processing_manager.submission_publication_manifests AS manifest
                                     WHERE manifest.submission_id = submission.submission_id
                                 ),
                                 0) + 1,
                             submission.source_sha256_digest,
                             submission.original_file_name,
                             clock_timestamp()
                         FROM document_processing_manager.document_submissions AS submission
                         WHERE submission.submission_id = @submission_id
                         RETURNING revision;
                         """,
                         connection,
                         transaction))
        {
            insertManifest.Parameters.AddWithValue(
                "submission_id",
                NpgsqlDbType.Uuid,
                submissionId);

            revision = Convert.ToInt32(
                await insertManifest.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }

        for (var index = 0; index < units.Count; index++)
        {
            var unit = units[index];
            var scope = PostgresProcessingUnitScopeMapper.ToDurableScope(unit.Scope);

            await using var insertUnit = new NpgsqlCommand(
                """
                INSERT INTO document_processing_manager.submission_publication_manifest_units
                (
                    submission_id, revision, processing_unit_id, unit_ordinal,
                    scope_kind, start_physical_page_number,
                    end_physical_page_number, scope_title,
                    start_content_unit_index, start_content_unit_id,
                    end_content_unit_index, end_content_unit_id
                )
                VALUES
                (
                    @submission_id, @revision, @unit_id, @ordinal,
                    @scope_kind, @start_page, @end_page, @scope_title,
                    @start_content_index, @start_content_id,
                    @end_content_index, @end_content_id
                );
                """,
                connection,
                transaction);

            insertUnit.Parameters.AddWithValue("submission_id", NpgsqlDbType.Uuid, submissionId);
            insertUnit.Parameters.AddWithValue("revision", NpgsqlDbType.Integer, revision);
            insertUnit.Parameters.AddWithValue("unit_id", NpgsqlDbType.Uuid, unit.UnitId.Value);
            insertUnit.Parameters.AddWithValue("ordinal", NpgsqlDbType.Integer, index + 1);
            insertUnit.Parameters.AddWithValue("scope_kind", NpgsqlDbType.Smallint, scope.Kind);
            AddNullable(insertUnit, "start_page", NpgsqlDbType.Integer, scope.StartPhysicalPageNumber);
            AddNullable(insertUnit, "end_page", NpgsqlDbType.Integer, scope.EndPhysicalPageNumber);
            AddNullable(insertUnit, "scope_title", NpgsqlDbType.Text, scope.Title);
            AddNullable(insertUnit, "start_content_index", NpgsqlDbType.Integer, scope.StartContentUnitIndex);
            AddNullable(insertUnit, "start_content_id", NpgsqlDbType.Text, scope.StartContentUnitId);
            AddNullable(insertUnit, "end_content_index", NpgsqlDbType.Integer, scope.EndContentUnitIndex);
            AddNullable(insertUnit, "end_content_id", NpgsqlDbType.Text, scope.EndContentUnitId);

            await insertUnit.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async ValueTask<SubmissionPublicationManifest> ReadLatestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        int revision;
        string sourceDigest;
        string originalFileName;
        DateTimeOffset finalizedAtUtc;

        await using (var readManifest = new NpgsqlCommand(
                         """
                         SELECT revision, source_sha256_digest,
                             original_file_name, finalized_at_utc
                         FROM document_processing_manager.submission_publication_manifests
                         WHERE submission_id = @submission_id
                         ORDER BY revision DESC
                         LIMIT 1;
                         """,
                         connection,
                         transaction))
        {
            readManifest.Parameters.AddWithValue("submission_id", NpgsqlDbType.Uuid, submissionId);
            await using var reader = await readManifest.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "A claimable result has no finalized submission manifest.");
            }

            revision = reader.GetInt32(0);
            sourceDigest = reader.GetString(1);
            originalFileName = reader.GetString(2);
            finalizedAtUtc = reader.GetFieldValue<DateTimeOffset>(3);
        }

        var units = new List<ExpectedProcessingUnit>();

        await using (var readUnits = new NpgsqlCommand(
                         """
                         SELECT processing_unit_id, unit_ordinal, scope_kind,
                             start_physical_page_number, end_physical_page_number,
                             scope_title, start_content_unit_index,
                             start_content_unit_id, end_content_unit_index,
                             end_content_unit_id
                         FROM document_processing_manager.submission_publication_manifest_units
                         WHERE submission_id = @submission_id
                             AND revision = @revision
                         ORDER BY unit_ordinal;
                         """,
                         connection,
                         transaction))
        {
            readUnits.Parameters.AddWithValue("submission_id", NpgsqlDbType.Uuid, submissionId);
            readUnits.Parameters.AddWithValue("revision", NpgsqlDbType.Integer, revision);
            await using var reader = await readUnits.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                units.Add(
                    new ExpectedProcessingUnit(
                        new ProcessingUnitId(reader.GetGuid(0)),
                        reader.GetInt32(1),
                        PostgresProcessingUnitScopeMapper.ReadScope(
                            reader,
                            kindOrdinal: 2,
                            startPageOrdinal: 3,
                            endPageOrdinal: 4,
                            titleOrdinal: 5,
                            startContentUnitIndexOrdinal: 6,
                            startContentUnitIdOrdinal: 7,
                            endContentUnitIndexOrdinal: 8,
                            endContentUnitIdOrdinal: 9)));
            }
        }

        return new SubmissionPublicationManifest(
            new DocumentSubmissionId(submissionId),
            revision,
            new Sha256Digest(sourceDigest),
            originalFileName,
            finalizedAtUtc,
            units);
    }

    private static void AddNullable(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        object? value)
    {
        command.Parameters.AddWithValue(
            name,
            type,
            value ?? DBNull.Value);
    }
}
