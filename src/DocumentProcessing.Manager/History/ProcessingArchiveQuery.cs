namespace DocumentProcessing.Manager.History;

/// <summary>Describes one bounded search over archived terminal units.</summary>
public sealed record ProcessingArchiveQuery
{
    #region Variables and Constants

    /// <summary>Gets the default archive page size.</summary>
    public const int DefaultLimit =
        50;

    /// <summary>Gets the largest supported archive page size.</summary>
    public const int MaximumLimit =
        200;

    #endregion

    #region Properties

    /// <summary>Gets the exclusive recent/archive boundary.</summary>
    public DateTimeOffset ArchivedBeforeUtc { get; }

    /// <summary>Gets the optional case-insensitive title fragment.</summary>
    public string? TitleContains { get; }

    /// <summary>Gets the optional inclusive completion lower bound.</summary>
    public DateTimeOffset? CompletedFromUtc { get; }

    /// <summary>Gets the optional exclusive completion upper bound.</summary>
    public DateTimeOffset? CompletedBeforeUtc { get; }

    /// <summary>Gets the deterministic result ordering.</summary>
    public ProcessingArchiveSort Sort { get; }

    /// <summary>Gets the zero-based result offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the bounded result count.</summary>
    public int Limit { get; }

    #endregion

    #region ctor

    /// <summary>Creates one archive search.</summary>
    public ProcessingArchiveQuery(
        DateTimeOffset archivedBeforeUtc,
        string? titleContains = null,
        DateTimeOffset? completedFromUtc = null,
        DateTimeOffset? completedBeforeUtc = null,
        ProcessingArchiveSort sort = ProcessingArchiveSort.CompletedNewest,
        int offset = 0,
        int limit = DefaultLimit)
    {
        if (archivedBeforeUtc ==
            default)
        {
            throw new ArgumentException(
                "Archive boundary is required.",
                nameof(archivedBeforeUtc));
        }

        if (completedFromUtc.HasValue &&
            completedBeforeUtc.HasValue &&
            completedFromUtc.Value >=
                completedBeforeUtc.Value)
        {
            throw new ArgumentException(
                "Archive completion upper bound must follow its lower bound.");
        }

        if (!Enum.IsDefined(
                sort))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sort));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset));
        }

        if (limit is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

        ArchivedBeforeUtc =
            archivedBeforeUtc.ToUniversalTime();

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
