namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Sanitized description of an ordinary failure encountered while acquiring
/// low-level raster observations during a coordinated extraction pass.
///
/// This is evidence about acquisition failure only. It is not an execution
/// decision and it does not make native extraction non-authoritative.
/// </summary>
public sealed record RasterObservationAcquisitionFailure
{
    public RasterObservationAcquisitionFailure(
        string exceptionType,
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                exceptionType))
        {
            throw new ArgumentException(
                "Exception type cannot be empty.",
                nameof(exceptionType));
        }

        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "Failure message cannot be empty.",
                nameof(message));
        }

        ExceptionType =
            exceptionType.Trim();

        Message =
            message.Trim();
    }

    public string ExceptionType { get; }

    public string Message { get; }
}
