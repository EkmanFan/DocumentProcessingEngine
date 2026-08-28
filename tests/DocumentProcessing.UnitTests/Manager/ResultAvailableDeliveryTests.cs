using DocumentProcessing.Manager.Custody;
using DocumentProcessing.Manager.Publication;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ResultAvailableDeliveryTests
{
    #region Tests

    [Fact]
    public void Constructor_NormalizesPortableMetadata()
    {
        var availableAt =
            new DateTimeOffset(
                2026,
                8,
                29,
                10,
                0,
                0,
                TimeSpan.FromHours(
                    2));
        var claimToken =
            Guid.NewGuid();

        var delivery =
            new ResultAvailableDelivery(
                " result-1 ",
                DocumentSubmissionId.New(),
                ProcessingUnitId.New(),
                new ProcessingUnitScope.PageRange(
                    10,
                    20,
                    "Part I"),
                " document-processing-result-v3 ",
                " Application/JSON ",
                byteLength:
                    42,
                new Sha256Digest(
                    new string(
                        'b',
                        64)),
                availableAt,
                claimToken,
                availableAt.AddMinutes(
                    5));

        Assert.Equal(
            "result-1",
            delivery.ResultReference);
        Assert.Equal(
            "document-processing-result-v3",
            delivery.SchemaVersion);
        Assert.Equal(
            "application/json",
            delivery.MediaType);
        Assert.Equal(
            availableAt.UtcDateTime,
            delivery.AvailableAtUtc.UtcDateTime);
    }

    [Fact]
    public void Constructor_RejectsInvalidClaimAndPayloadMetadata()
    {
        var now =
            DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new ResultAvailableDelivery(
                    "result-1",
                    DocumentSubmissionId.New(),
                    ProcessingUnitId.New(),
                    new ProcessingUnitScope.WholeDocument(),
                    "v1",
                    "application/json",
                    byteLength:
                        0,
                    new Sha256Digest(
                        new string(
                            'c',
                            64)),
                    now,
                    Guid.NewGuid(),
                    now.AddMinutes(
                        1)));

        Assert.Throws<ArgumentException>(
            () =>
                new ResultAvailableDelivery(
                    "result-1",
                    DocumentSubmissionId.New(),
                    ProcessingUnitId.New(),
                    new ProcessingUnitScope.WholeDocument(),
                    "v1",
                    "application/json",
                    byteLength:
                        1,
                    new Sha256Digest(
                        new string(
                            'c',
                            64)),
                    now,
                    Guid.Empty,
                    now.AddMinutes(
                        1)));
    }

    #endregion
}
