namespace DocumentProcessing.Manager.Blazor.Workshop;

internal static class ManagerResultDownloadFileName
{
    #region Variables and Constants

    private const int
        MaximumStemLength =
            140;

    private const string
        DefaultStem =
            "document";

    private const string
        ResultSuffix =
            ".dpengine-result.json";

    #endregion

    #region Methods

    public static string Create(
        string originalFileName,
        ManagerWorkItemScopeView scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            originalFileName);

        ArgumentNullException.ThrowIfNull(
            scope);

        var normalizedPath =
            originalFileName.Trim()
                .Replace(
                    '\\',
                    '/');

        var leafName =
            normalizedPath[
                (normalizedPath.LastIndexOf(
                    '/') + 1)..];

        var sourceStem =
            Path.GetFileNameWithoutExtension(
                leafName);

        var sanitizedStem =
            SanitizeStem(
                sourceStem);

        var scopeSuffix =
            scope.Kind switch
            {
                ManagerWorkItemScopeKind.WholeDocument =>
                    string.Empty,
                ManagerWorkItemScopeKind.PageRange
                    when scope.StartPhysicalPageNumber is not null &&
                         scope.EndPhysicalPageNumber is not null =>
                    $".pages-{scope.StartPhysicalPageNumber}-{scope.EndPhysicalPageNumber}",
                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(scope),
                        scope.Kind,
                        "Unknown Manager work-item scope kind.")
            };

        return
            $"{sanitizedStem}{scopeSuffix}{ResultSuffix}";
    }

    private static string SanitizeStem(
        string value)
    {
        var sanitized =
            new string(
                    value.Select(
                            character =>
                                char.IsLetterOrDigit(
                                    character) ||
                                character is
                                    '-' or
                                    '_'
                                    ? character
                                    : '-')
                        .ToArray())
                .Trim(
                    '-',
                    '_');

        if (string.IsNullOrWhiteSpace(
                sanitized))
        {
            return DefaultStem;
        }

        return sanitized.Length >
               MaximumStemLength
            ? sanitized[..MaximumStemLength]
                .TrimEnd(
                    '-',
                    '_')
            : sanitized;
    }

    #endregion
}
