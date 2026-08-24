namespace DocumentProcessing.Ocr.Adapters.PaddleOCR;

/// <summary>
/// Native PaddleOCR result returned by the serving client.
///
/// This provider-owned representation is translated to neutral Core OCR
/// evidence by <see cref="PaddleOcrAdapter"/>.
/// </summary>
public sealed class PaddleOcrNativeResult
{
    #region Properties

    public byte[] PrunedResultJson { get; }

    #endregion


    #region ctor

    public PaddleOcrNativeResult(
        byte[] prunedResultJson)
    {
        ArgumentNullException.ThrowIfNull(
            prunedResultJson);

        if (prunedResultJson.Length == 0)
        {
            throw new ArgumentException(
                "PaddleOCR native result cannot be empty.",
                nameof(prunedResultJson));
        }

        PrunedResultJson =
            prunedResultJson.ToArray();
    }

    #endregion
}
