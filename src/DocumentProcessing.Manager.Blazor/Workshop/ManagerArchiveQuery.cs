namespace DocumentProcessing.Manager.Blazor.Workshop;

internal enum ManagerArchiveSort
{
    CompletedNewest,
    CompletedOldest,
    TitleAscending,
    TitleDescending
}

internal sealed record ManagerArchiveQuery
{
    #region Variables and Constants

    public const int DefaultLimit =
        50;

    #endregion

    #region Properties

    public string? TitleContains { get; }

    public DateTimeOffset? CompletedFromUtc { get; }

    public DateTimeOffset? CompletedBeforeUtc { get; }

    public ManagerArchiveSort Sort { get; }

    public int Offset { get; }

    public int Limit { get; }

    #endregion

    #region ctor

    public ManagerArchiveQuery(
        string? titleContains = null,
        DateTimeOffset? completedFromUtc = null,
        DateTimeOffset? completedBeforeUtc = null,
        ManagerArchiveSort sort = ManagerArchiveSort.CompletedNewest,
        int offset = 0,
        int limit = DefaultLimit)
    {
        if (!Enum.IsDefined(
                sort))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sort));
        }

        if (completedFromUtc.HasValue &&
            completedBeforeUtc.HasValue &&
            completedFromUtc.Value >=
                completedBeforeUtc.Value)
        {
            throw new ArgumentException(
                "Archive completion upper bound must follow its lower bound.");
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset));
        }

        if (limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

        TitleContains =
            string.IsNullOrWhiteSpace(
                titleContains)
                ? null
                : titleContains.Trim();

        CompletedFromUtc =
            completedFromUtc?.ToUniversalTime();

        CompletedBeforeUtc =
            completedBeforeUtc?.ToUniversalTime();

        Sort =
            sort;

        Offset =
            offset;

        Limit =
            limit;
    }

    #endregion
}

internal static class ManagerArchiveSortExtensions
{
    #region Methods

    public static string ToApiValue(
        this ManagerArchiveSort sort) =>
        sort switch
        {
            ManagerArchiveSort.CompletedNewest =>
                "completedNewest",
            ManagerArchiveSort.CompletedOldest =>
                "completedOldest",
            ManagerArchiveSort.TitleAscending =>
                "titleAscending",
            ManagerArchiveSort.TitleDescending =>
                "titleDescending",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(sort))
        };

    #endregion
}
