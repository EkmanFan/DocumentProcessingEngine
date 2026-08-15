using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Reconciliation;

public sealed class NativeLayoutTextPairerTests
{
    [Fact]
    public void Pair_AggregatesMultipleSourceBlocksForOneTargetInNativeReadingOrder()
    {
        var firstBlock =
            Block(
                sourceSequence: 7,
                readingOrder: 7,
                Word(
                    100,
                    "eox",
                    0.10,
                    0.10,
                    0.14,
                    0.20),
                Word(
                    101,
                    "1",
                    0.145,
                    0.10,
                    0.16,
                    0.20),
                Word(
                    102,
                    ".1",
                    0.165,
                    0.10,
                    0.19,
                    0.20));

        var secondBlock =
            Block(
                sourceSequence: 8,
                readingOrder: 8,
                Word(
                    103,
                    "The",
                    0.21,
                    0.10,
                    0.25,
                    0.20),
                Word(
                    104,
                    "Canon",
                    0.255,
                    0.10,
                    0.31,
                    0.20),
                Word(
                    105,
                    "of",
                    0.315,
                    0.10,
                    0.335,
                    0.20),
                Word(
                    106,
                    "Scripture",
                    0.34,
                    0.10,
                    0.40,
                    0.20));

        var target =
            Target(
                physicalPageNumber: 36,
                observationSequence: 8,
                readingOrder: 8,
                LayoutObservationKind.Heading,
                0.09,
                0.09,
                0.41,
                0.21);

        var pairing =
            Assert.Single(
                NativeLayoutTextPairer.Pair(
                    new[]
                    {
                        secondBlock,
                        firstBlock
                    },
                    new[]
                    {
                        target
                    }));

        Assert.Equal(
            NativeLayoutTextPairingStatus.Comparable,
            pairing.Status);

        var evidence =
            Assert.IsType<ComparableNativeTextEvidence>(
                pairing.ComparableNativeEvidence);

        Assert.Equal(
            2,
            evidence.ExtentCount);

        Assert.Equal(
            new[]
            {
                7,
                8
            },
            evidence.SourceBlocks
                .Select(
                    block =>
                        block.SourceSequence)
                .ToArray());

        Assert.Equal(
            7,
            evidence.WordCount);

        Assert.Equal(
            "eox 1 .1 The Canon of Scripture",
            evidence.Text);

        Assert.Empty(
            pairing.AmbiguousWords);
    }

    [Fact]
    public void Pair_AllowsOneNativeBlockToBePartitionedAcrossMultipleTargets()
    {
        var first =
            Word(
                0,
                "alpha",
                0.10,
                0.10,
                0.18,
                0.20);

        var second =
            Word(
                1,
                "beta",
                0.19,
                0.10,
                0.27,
                0.20);

        var third =
            Word(
                2,
                "gamma",
                0.60,
                0.10,
                0.70,
                0.20);

        var fourth =
            Word(
                3,
                "delta",
                0.71,
                0.10,
                0.80,
                0.20);

        var block =
            Block(
                sourceSequence: 3,
                readingOrder: 3,
                first,
                second,
                third,
                fourth);

        var leftTarget =
            Target(
                physicalPageNumber: 1,
                observationSequence: 10,
                readingOrder: 0,
                LayoutObservationKind.Text,
                0.08,
                0.08,
                0.30,
                0.22);

        var rightTarget =
            Target(
                physicalPageNumber: 1,
                observationSequence: 11,
                readingOrder: 1,
                LayoutObservationKind.Text,
                0.58,
                0.08,
                0.82,
                0.22);

        var pairings =
            NativeLayoutTextPairer.Pair(
                new[]
                {
                    block
                },
                new[]
                {
                    rightTarget,
                    leftTarget
                });

        Assert.Equal(
            2,
            pairings.Count);

        Assert.All(
            pairings,
            pairing =>
                Assert.Equal(
                    NativeLayoutTextPairingStatus.Comparable,
                    pairing.Status));

        Assert.Equal(
            "alpha beta",
            pairings[0]
                .ComparableNativeEvidence!
                .Text);

        Assert.Equal(
            "gamma delta",
            pairings[1]
                .ComparableNativeEvidence!
                .Text);

        Assert.Same(
            block,
            pairings[0]
                .ComparableNativeEvidence!
                .SourceBlocks
                .Single());

        Assert.Same(
            block,
            pairings[1]
                .ComparableNativeEvidence!
                .SourceBlocks
                .Single());
    }

    [Fact]
    public void Pair_FailsClosedWhenOneProjectedWordBelongsToMultipleTargets()
    {
        var sharedWord =
            Word(
                0,
                "shared",
                0.45,
                0.10,
                0.55,
                0.20);

        var block =
            Block(
                sourceSequence: 0,
                readingOrder: 0,
                sharedWord);

        var firstTarget =
            Target(
                physicalPageNumber: 1,
                observationSequence: 0,
                readingOrder: 0,
                LayoutObservationKind.Text,
                0.40,
                0.08,
                0.52,
                0.22);

        var secondTarget =
            Target(
                physicalPageNumber: 1,
                observationSequence: 1,
                readingOrder: 1,
                LayoutObservationKind.Text,
                0.50,
                0.08,
                0.60,
                0.22);

        var pairings =
            NativeLayoutTextPairer.Pair(
                new[]
                {
                    block
                },
                new[]
                {
                    firstTarget,
                    secondTarget
                });

        Assert.Equal(
            2,
            pairings.Count);

        Assert.All(
            pairings,
            pairing =>
            {
                Assert.Equal(
                    NativeLayoutTextPairingStatus
                        .AmbiguousWordOwnership,
                    pairing.Status);

                Assert.Null(
                    pairing.ComparableNativeEvidence);

                Assert.Same(
                    sharedWord,
                    Assert.Single(
                        pairing.AmbiguousWords));
            });
    }

    [Fact]
    public void Pair_ReturnsNoNativeEvidenceWhenTargetHasNoProjection()
    {
        var word =
            Word(
                0,
                "native",
                0.10,
                0.10,
                0.20,
                0.20);

        var block =
            Block(
                sourceSequence: 0,
                readingOrder: 0,
                word);

        var target =
            Target(
                physicalPageNumber: 1,
                observationSequence: 0,
                readingOrder: 0,
                LayoutObservationKind.Text,
                0.70,
                0.70,
                0.80,
                0.80);

        var pairing =
            Assert.Single(
                NativeLayoutTextPairer.Pair(
                    new[]
                    {
                        block
                    },
                    new[]
                    {
                        target
                    }));

        Assert.Equal(
            NativeLayoutTextPairingStatus.NoNativeEvidence,
            pairing.Status);

        Assert.Null(
            pairing.ComparableNativeEvidence);

        Assert.Empty(
            pairing.AmbiguousWords);
    }

    [Fact]
    public void Pair_RejectsLayoutTargetsNotAuthorizedForTextRecognition()
    {
        var word =
            Word(
                0,
                "not-figure-text",
                0.10,
                0.10,
                0.20,
                0.20);

        var block =
            Block(
                sourceSequence: 0,
                readingOrder: 0,
                word);

        var figure =
            Target(
                physicalPageNumber: 1,
                observationSequence: 0,
                readingOrder: 0,
                LayoutObservationKind.Figure,
                0.05,
                0.05,
                0.25,
                0.25);

        Assert.Throws<InvalidOperationException>(
            () =>
                NativeLayoutTextPairer.Pair(
                    new[]
                    {
                        block
                    },
                    new[]
                    {
                        figure
                    }));
    }

    private static DocumentWord Word(
        int sourceSequence,
        string text,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            sourceSequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom));

    private static DocumentTextBlock Block(
        int sourceSequence,
        int? readingOrder,
        params DocumentWord[] words)
    {
        if (words.Length == 0)
        {
            throw new ArgumentException(
                "Test block requires at least one word.",
                nameof(words));
        }

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder,
            string.Join(
                " ",
                words.Select(
                    word =>
                        word.Text)),
            new NormalizedRectangle(
                words.Min(
                    word =>
                        word.Bounds.Left),
                words.Min(
                    word =>
                        word.Bounds.Top),
                words.Max(
                    word =>
                        word.Bounds.Right),
                words.Max(
                    word =>
                        word.Bounds.Bottom)),
            words);
    }

    private static LayoutObservation Target(
        int physicalPageNumber,
        int observationSequence,
        int? readingOrder,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber,
            observationSequence,
            readingOrder,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            rawLabel: kind.ToString());
}
