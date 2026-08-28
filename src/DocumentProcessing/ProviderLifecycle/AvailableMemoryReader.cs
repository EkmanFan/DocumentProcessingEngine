using System.Globalization;

namespace DocumentProcessing.ProviderLifecycle;

internal interface IAvailableMemoryReader
{
    long ReadAvailableBytes();
}

internal sealed class AvailableMemoryReader
    : IAvailableMemoryReader
{
    #region Variables and Constants

    private const string
        LinuxMemoryInformationPath =
            "/proc/meminfo";

    #endregion

    #region Methods

    public long ReadAvailableBytes()
    {
        if (File.Exists(
                LinuxMemoryInformationPath))
        {
            var linuxAvailable =
                ReadLinuxAvailableBytes();

            if (linuxAvailable is not null)
            {
                return linuxAvailable.Value;
            }
        }

        return GC.GetGCMemoryInfo()
            .TotalAvailableMemoryBytes;
    }

    private static long? ReadLinuxAvailableBytes()
    {
        foreach (var line in File.ReadLines(
                     LinuxMemoryInformationPath))
        {
            if (!line.StartsWith(
                    "MemAvailable:",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var fields =
                line.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

            if (fields.Length >=
                    2 &&
                long.TryParse(
                    fields[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var availableKilobytes) &&
                availableKilobytes >=
                    0)
            {
                return checked(
                    availableKilobytes *
                    1024);
            }

            return null;
        }

        return null;
    }

    #endregion
}
