using System.Globalization;

namespace DocumentProcessing.Core.DualRun;

/// <summary>
/// Resolves one snapshotted Dual Run profile into document-level execution.
/// </summary>
public static class DocumentDualRunProfileSelector
{
    #region Variables and Constants

    /// <summary>
    /// Basis-point sampling resolution. 10_000 represents 100%.
    /// </summary>
    public const int SamplingResolution =
        10_000;

    #endregion

    #region Methods Selection

    public static DocumentDualRunSelection Select(
        DocumentDualRunProfile profile,
        string? sourceDocumentSha256 = null,
        int sampledBasisPoints = 0)
    {
        if (!Enum.IsDefined(
                typeof(DocumentDualRunProfile),
                profile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(profile));
        }

        if (sampledBasisPoints is < 0 or >
            SamplingResolution)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampledBasisPoints));
        }

        return profile switch
        {
            DocumentDualRunProfile.Disabled =>
                new DocumentDualRunSelection(
                    profile,
                    isSelected:
                        false,
                    executionMode:
                        null,
                    samplingBucket:
                        null),

            DocumentDualRunProfile.PlanningOnly =>
                new DocumentDualRunSelection(
                    profile,
                    isSelected:
                        true,
                    DocumentDualRunExecutionMode.PlanningOnly,
                    samplingBucket:
                        null),

            DocumentDualRunProfile.Full =>
                new DocumentDualRunSelection(
                    profile,
                    isSelected:
                        true,
                    DocumentDualRunExecutionMode.Full,
                    samplingBucket:
                        null),

            DocumentDualRunProfile.Sampled =>
                SelectSampled(
                    sourceDocumentSha256,
                    sampledBasisPoints),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(profile))
        };
    }

    private static DocumentDualRunSelection SelectSampled(
        string? sourceDocumentSha256,
        int sampledBasisPoints)
    {
        var samplingBucket =
            SamplingBucket(
                sourceDocumentSha256);

        var isSelected =
            samplingBucket <
            sampledBasisPoints;

        return new DocumentDualRunSelection(
            DocumentDualRunProfile.Sampled,
            isSelected,
            isSelected
                ? DocumentDualRunExecutionMode.Full
                : null,
            samplingBucket);
    }

    private static int SamplingBucket(
        string? sourceDocumentSha256)
    {
        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Sampled Dual Run requires the source document SHA-256.",
                nameof(sourceDocumentSha256));
        }

        var normalized =
            sourceDocumentSha256.Trim();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Source document SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(sourceDocumentSha256));
        }

        if (!ulong.TryParse(
                normalized.AsSpan(
                    0,
                    16),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var prefix))
        {
            throw new ArgumentException(
                "Source document SHA-256 prefix is not valid hexadecimal.",
                nameof(sourceDocumentSha256));
        }

        return (int)(
            prefix %
            SamplingResolution);
    }

    #endregion
}
