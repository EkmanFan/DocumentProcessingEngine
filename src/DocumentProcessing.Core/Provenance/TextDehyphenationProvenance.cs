namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Public deterministic dehyphenation facts retained without exposing the
/// internal normalization object graph.
/// </summary>
public sealed record TextDehyphenationProvenance
{
    public TextDehyphenationProvenance(
        int softHyphenRemovalCount,
        int boundaryJoinCount)
    {
        if (softHyphenRemovalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(softHyphenRemovalCount));
        }

        if (boundaryJoinCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundaryJoinCount));
        }

        if (softHyphenRemovalCount == 0 &&
            boundaryJoinCount == 0)
        {
            throw new ArgumentException(
                "Dehyphenation provenance is retained only when text changed.");
        }

        SoftHyphenRemovalCount =
            softHyphenRemovalCount;

        BoundaryJoinCount =
            boundaryJoinCount;
    }

    public int SoftHyphenRemovalCount { get; }

    public int BoundaryJoinCount { get; }
}
