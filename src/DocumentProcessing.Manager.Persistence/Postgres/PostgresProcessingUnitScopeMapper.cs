using DocumentProcessing.Manager.Queue;
using Npgsql;

namespace DocumentProcessing.Manager.Persistence.Postgres;

internal static class PostgresProcessingUnitScopeMapper
{
    #region Methods Mapping

    public static DurableScope ToDurableScope(
        ProcessingUnitScope scope) =>
        scope switch
        {
            ProcessingUnitScope.WholeDocument =>
                new DurableScope(
                    0,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
            ProcessingUnitScope.PageRange range =>
                new DurableScope(
                    1,
                    range.StartPhysicalPageNumber,
                    range.EndPhysicalPageNumber,
                    range.Title,
                    null,
                    null,
                    null,
                    null),
            ProcessingUnitScope.ContentUnitRange range =>
                new DurableScope(
                    2,
                    null,
                    null,
                    range.Title,
                    range.StartContentUnitIndex,
                    range.StartContentUnitId,
                    range.EndContentUnitIndex,
                    range.EndContentUnitId),
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(scope),
                    scope,
                    "Unknown processing-unit scope.")
        };

    public static ProcessingUnitScope ReadScope(
        NpgsqlDataReader reader,
        int kindOrdinal,
        int startPageOrdinal,
        int endPageOrdinal,
        int titleOrdinal,
        int startContentUnitIndexOrdinal,
        int startContentUnitIdOrdinal,
        int endContentUnitIndexOrdinal,
        int endContentUnitIdOrdinal) =>
        reader.GetInt16(
            kindOrdinal) switch
        {
            0 =>
                new ProcessingUnitScope.WholeDocument(),
            1 =>
                new ProcessingUnitScope.PageRange(
                    reader.GetInt32(
                        startPageOrdinal),
                    reader.GetInt32(
                        endPageOrdinal),
                    reader.GetString(
                        titleOrdinal)),
            2 =>
                new ProcessingUnitScope.ContentUnitRange(
                    reader.GetInt32(
                        startContentUnitIndexOrdinal),
                    reader.GetString(
                        startContentUnitIdOrdinal),
                    reader.GetInt32(
                        endContentUnitIndexOrdinal),
                    reader.GetString(
                        endContentUnitIdOrdinal),
                    reader.GetString(
                        titleOrdinal)),
            var value =>
                throw new InvalidOperationException(
                    $"Unknown durable processing-unit scope kind '{value}'.")
        };

    #endregion

    #region Internal Types

    internal sealed record DurableScope(
        short Kind,
        int? StartPhysicalPageNumber,
        int? EndPhysicalPageNumber,
        string? Title,
        int? StartContentUnitIndex,
        string? StartContentUnitId,
        int? EndContentUnitIndex,
        string? EndContentUnitId);

    #endregion
}
