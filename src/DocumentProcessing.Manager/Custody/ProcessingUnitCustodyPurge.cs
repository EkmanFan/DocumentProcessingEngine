using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Custody;

/// <summary>Durable filesystem cleanup work created by a metadata purge.</summary>
public sealed record ProcessingUnitCustodyPurge
{
    public Guid PurgeId { get; }

    public ProcessingUnitId UnitId { get; }

    public Sha256Digest? ResultArtifactDigest { get; }

    public Sha256Digest? SourceArtifactDigest { get; }

    public string? PublicationDirectory { get; }

    public ProcessingUnitCustodyPurge(
        Guid purgeId,
        ProcessingUnitId unitId,
        Sha256Digest? resultArtifactDigest,
        Sha256Digest? sourceArtifactDigest,
        string? publicationDirectory)
    {
        if (purgeId == Guid.Empty)
        {
            throw new ArgumentException("Purge identifier cannot be empty.", nameof(purgeId));
        }

        if (unitId.Value == Guid.Empty)
        {
            throw new ArgumentException("Processing-unit identifier cannot be empty.", nameof(unitId));
        }

        if (publicationDirectory is not null && string.IsNullOrWhiteSpace(publicationDirectory))
        {
            throw new ArgumentException("Publication directory cannot be blank.", nameof(publicationDirectory));
        }

        PurgeId = purgeId;
        UnitId = unitId;
        ResultArtifactDigest = resultArtifactDigest;
        SourceArtifactDigest = sourceArtifactDigest;
        PublicationDirectory = publicationDirectory;
    }
}
