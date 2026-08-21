namespace DocumentProcessing.Epub.Extraction;

internal static class EpubArchivePath
{
    public static string NormalizeEntryPath(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new InvalidDataException(
                "EPUB archive path cannot be empty.");
        }

        return Normalize(
            value,
            allowParentTraversal:
                false);
    }

    public static string Resolve(
        string baseResourcePath,
        string relativeReference)
    {
        if (string.IsNullOrWhiteSpace(
                relativeReference))
        {
            throw new InvalidDataException(
                "EPUB resource reference cannot be empty.");
        }

        var reference =
            relativeReference.Trim();

        if (Uri.TryCreate(
                reference,
                UriKind.Absolute,
                out _))
        {
            throw new InvalidDataException(
                "EPUB spine resources must resolve inside the publication archive.");
        }

        var separatorIndex =
            reference.IndexOfAny(
                ['#', '?']);

        if (separatorIndex >=
            0)
        {
            reference =
                reference[..separatorIndex];
        }

        reference =
            Uri.UnescapeDataString(
                reference);

        var baseDirectory =
            baseResourcePath.Contains(
                '/')
                ? baseResourcePath[..(baseResourcePath.LastIndexOf(
                    '/') +
                    1)]
                : string.Empty;

        return Normalize(
            baseDirectory +
            reference,
            allowParentTraversal:
                true);
    }

    private static string Normalize(
        string value,
        bool allowParentTraversal)
    {
        if (value.Contains(
                '\\') ||
            value.StartsWith(
                "/",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "EPUB archive paths must be relative URI paths.");
        }

        var segments =
            new List<string>();

        foreach (var segment in
                 value.Split(
                     '/',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment ==
                ".")
            {
                continue;
            }

            if (segment ==
                "..")
            {
                if (!allowParentTraversal ||
                    segments.Count ==
                    0)
                {
                    throw new InvalidDataException(
                        "EPUB archive path escapes the publication root.");
                }

                segments.RemoveAt(
                    segments.Count -
                    1);

                continue;
            }

            if (segment.Length ==
                0)
            {
                continue;
            }

            segments.Add(
                segment);
        }

        if (segments.Count ==
            0)
        {
            throw new InvalidDataException(
                "EPUB archive path resolves to the publication root.");
        }

        return string.Join(
            '/',
            segments);
    }
}
