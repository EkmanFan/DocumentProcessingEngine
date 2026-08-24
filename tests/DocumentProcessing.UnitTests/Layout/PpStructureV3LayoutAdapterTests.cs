using System.Text;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Layout.Adapters.PpStructureV3;

namespace DocumentProcessing.UnitTests.Layout;

public sealed class PpStructureV3LayoutAdapterTests
{
    [Fact]
    public async Task AdaptAsync_Ehrman233_ProducesNeutralMixedContentSequence()
    {
        const string json =
            """
            {
              "res": {
                "parsing_res_list": [
                  {
                    "block_bbox": [617, 809, 1389, 981],
                    "block_label": "paragraph_title",
                    "block_content": "THE NEW TESTAMENT EPISTLES AND THE CONTEXTUAL METHOD",
                    "block_order": 1
                  },
                  {
                    "block_bbox": [613, 1044, 1468, 1376],
                    "block_label": "text",
                    "block_content": "As we have noted ... Imagine,",
                    "block_order": 2
                  },
                  {
                    "block_bbox": [620, 1442, 1461, 2840],
                    "block_label": "image",
                    "block_content": "Φry p Kt JEN2xykt etaErcNy jdpspikncxi",
                    "block_order": null
                  },
                  {
                    "block_bbox": [608, 2880, 1427, 3113],
                    "block_label": "figure_title",
                    "block_content": "Figure 11.1 Example of a papyrus letter from antiquity",
                    "block_order": null
                  },
                  {
                    "block_bbox": [1530, 776, 2387, 1344],
                    "block_label": "text",
                    "block_content": "for example, you stumble on a short message",
                    "block_order": 3
                  }
                ]
              }
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(json));

        var result =
            await PpStructureV3LayoutAdapter.AdaptAsync(
                stream,
                physicalPageNumber: 233,
                pixelWidth: 2556,
                pixelHeight: 3305);

        Assert.Equal(
            ProcessingCapability.LayoutAnalysis,
            result.Capability);
        Assert.Equal(
            PpStructureV3LayoutAdapter.BackendId,
            result.BackendId);
        Assert.Equal(233, result.PhysicalPageNumber);
        Assert.Equal(5, result.Observations.Count);

        Assert.Collection(
            result.Observations,
            observation =>
            {
                Assert.Equal(
                    LayoutObservationKind.Heading,
                    observation.Kind);
                Assert.Equal("paragraph_title", observation.RawLabel);
                Assert.Equal(0, observation.ReadingOrder);
            },
            observation =>
            {
                Assert.Equal(
                    LayoutObservationKind.Text,
                    observation.Kind);
                Assert.Equal("text", observation.RawLabel);
                Assert.Equal(1, observation.ReadingOrder);
            },
            observation =>
            {
                Assert.Equal(
                    LayoutObservationKind.Figure,
                    observation.Kind);
                Assert.Equal("image", observation.RawLabel);
                Assert.Equal(2, observation.ReadingOrder);
            },
            observation =>
            {
                Assert.Equal(
                    LayoutObservationKind.Caption,
                    observation.Kind);
                Assert.Equal("figure_title", observation.RawLabel);
                Assert.Equal(3, observation.ReadingOrder);
            },
            observation =>
            {
                Assert.Equal(
                    LayoutObservationKind.Text,
                    observation.Kind);
                Assert.Equal("text", observation.RawLabel);
                Assert.Equal(4, observation.ReadingOrder);
            });

        var figure = result.Observations[2];

        Assert.Equal(
            620d / 2556d,
            figure.Bounds.Left,
            precision: 12);
        Assert.Equal(
            1442d / 3305d,
            figure.Bounds.Top,
            precision: 12);
        Assert.Equal(
            1461d / 2556d,
            figure.Bounds.Right,
            precision: 12);
        Assert.Equal(
            2840d / 3305d,
            figure.Bounds.Bottom,
            precision: 12);
    }

    [Fact]
    public async Task AdaptAsync_DoesNotExposePpStructureBlockContentAsLayoutEvidence()
    {
        const string json =
            """
            {
              "parsing_res_list": [
                {
                  "block_bbox": [620, 1442, 1461, 2840],
                  "block_label": "image",
                  "block_content": "OCR NOISE THAT MUST NOT BECOME DOCUMENT TEXT"
                }
              ]
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(json));

        var result =
            await PpStructureV3LayoutAdapter.AdaptAsync(
                    stream,
                    physicalPageNumber: 233,
                    pixelWidth: 2556,
                    pixelHeight: 3305);

        var observation =
            Assert.Single(result.Observations);

        Assert.Equal(
            LayoutObservationKind.Figure,
            observation.Kind);

        Assert.DoesNotContain(
            typeof(LayoutObservation).GetProperties(),
            property =>
                property.Name.Equals(
                    "Text",
                    StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals(
                    "Content",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdaptAsync_MapsFormulaToNeutralFigure()
    {
        const string json =
            """
            {
              "res": {
                "parsing_res_list": [
                  {
                    "block_bbox": [193, 993, 369, 1102],
                    "block_label": "formula",
                    "block_content": "If p, then q. P therefore q."
                  }
                ]
              }
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(json));

        var result =
            await PpStructureV3LayoutAdapter.AdaptAsync(
                    stream,
                    physicalPageNumber: 16,
                    pixelWidth: 1020,
                    pixelHeight: 1320);

        var observation =
            Assert.Single(result.Observations);

        Assert.Equal(
            LayoutObservationKind.Figure,
            observation.Kind);
        Assert.Equal(
            "formula",
            observation.RawLabel);
    }

    [Fact]
    public async Task AdaptAsync_MapsUnmodeledLabelsToUnknown()
    {
        const string json =
            """
            {
              "res": {
                "parsing_res_list": [
                  {
                    "block_bbox": [100, 50, 600, 100],
                    "block_label": "header",
                    "block_content": "The New Testament"
                  },
                  {
                    "block_bbox": [50, 50, 80, 100],
                    "block_label": "number",
                    "block_content": "202"
                  }
                ]
              }
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(json));

        var result =
            await PpStructureV3LayoutAdapter.AdaptAsync(
                    stream,
                    physicalPageNumber: 233,
                    pixelWidth: 2556,
                    pixelHeight: 3305);

        Assert.All(
            result.Observations,
            observation =>
                Assert.Equal(
                    LayoutObservationKind.Unknown,
                    observation.Kind));
    }

    [Fact]
    public async Task AdaptAsync_RejectsMissingParsingResultAndRestoresSeekableStreamPosition()
    {
        const string json =
            """
            {
              "res": {
                "other": []
              }
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(json));

        stream.Position = 5;

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await PpStructureV3LayoutAdapter
                    .AdaptAsync(
                        stream,
                        physicalPageNumber: 233,
                        pixelWidth: 2556,
                        pixelHeight: 3305)
                    .AsTask());

        Assert.Equal(5, stream.Position);
    }
}
