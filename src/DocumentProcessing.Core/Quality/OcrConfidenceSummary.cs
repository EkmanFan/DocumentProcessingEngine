namespace DocumentProcessing.Core.Quality;

/// <summary>
/// Deterministic arithmetic summary of the OCR fragment confidence values
/// retained for one OCR region.
///
/// These values are backend evidence, not calibrated probabilities and not a
/// quality score. V1 deliberately does not compare them to any acceptance
/// threshold.
/// </summary>
public sealed record OcrConfidenceSummary
{
    public OcrConfidenceSummary(
        int observationCount,
        double minimum,
        double arithmeticMean,
        double maximum)
    {
        if (observationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observationCount),
                observationCount,
                "OCR confidence summary requires at least one observation.");
        }

        ValidateConfidence(
            minimum,
            nameof(minimum));

        ValidateConfidence(
            arithmeticMean,
            nameof(arithmeticMean));

        ValidateConfidence(
            maximum,
            nameof(maximum));

        if (minimum > arithmeticMean ||
            arithmeticMean > maximum)
        {
            throw new ArgumentException(
                "OCR confidence summary must satisfy minimum <= arithmetic mean <= maximum.");
        }

        ObservationCount = observationCount;
        Minimum = minimum;
        ArithmeticMean = arithmeticMean;
        Maximum = maximum;
    }

    public int ObservationCount { get; }

    public double Minimum { get; }

    public double ArithmeticMean { get; }

    public double Maximum { get; }

    private static void ValidateConfidence(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(
                value) ||
            value < 0d ||
            value > 1d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "OCR confidence must be finite and between zero and one.");
        }
    }
}
