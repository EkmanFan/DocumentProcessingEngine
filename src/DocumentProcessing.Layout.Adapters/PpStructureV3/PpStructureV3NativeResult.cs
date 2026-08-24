namespace DocumentProcessing.Layout.Adapters.PpStructureV3;

/// <summary>
/// Native PP-StructureV3 page result returned by the serving client.
/// The layout adapter translates this provider representation to neutral Core
/// layout evidence.
/// </summary>
public sealed class PpStructureV3NativeResult
{
    #region Properties

    public byte[] PrunedResultJson { get; }

    #endregion


    #region ctor

    public PpStructureV3NativeResult(
        byte[] prunedResultJson)
    {
        ArgumentNullException.ThrowIfNull(
            prunedResultJson);

        if (prunedResultJson.Length == 0)
        {
            throw new ArgumentException(
                "PP-StructureV3 native result cannot be empty.",
                nameof(prunedResultJson));
        }

        PrunedResultJson =
            prunedResultJson.ToArray();
    }

    #endregion
}
