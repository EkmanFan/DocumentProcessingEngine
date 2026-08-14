#!/usr/bin/env python3

import argparse
import hashlib
import json
import math
import os
import re
import resource
import sys
import time
import unicodedata
from pathlib import Path

from PIL import Image, ImageDraw


RAW_SCHEMA = "document-processing-ppstructurev3-layout-result-v1"
ASSESSMENT_SCHEMA = "document-processing-layout-assessment-v1"
INPUT_INDEX_SCHEMA = "document-processing-ocr-benchmark-input-index-v1"
STRUCTURE_SCHEMA = "document-processing-ocr-mixed-content-structure-v1"


FIGURE_LABELS = {
    "image",
    "figure",
    "header_image",
    "footer_image",
}

CAPTION_LABELS = {
    "figure_caption",
    "figure_title",
}

HEADING_LABELS = {
    "doc_title",
    "paragraph_title",
}

TEXT_LABELS = {
    "text",
    "abstract",
    "footnote",
    "aside_text",
}

NON_NARRATIVE_LABELS = FIGURE_LABELS | CAPTION_LABELS | {
    "table",
    "table_caption",
    "seal",
    "formula",
    "formula_number",
    "chart",
}


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-index", required=True)
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--structure", required=True)
    parser.add_argument("--raw-output", required=True)
    parser.add_argument("--assessment-output", required=True)
    parser.add_argument("--annotated-output", required=True)
    return parser.parse_args()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json(path, value):
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + f".tmp-{os.getpid()}")
    temporary.write_text(
        json.dumps(value, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    temporary.replace(target)


def sha256_file(path):
    digest = hashlib.sha256()

    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)

    return digest.hexdigest()


def to_jsonable(value):
    if isinstance(value, dict):
        return {
            str(key): to_jsonable(item)
            for key, item in value.items()
        }

    if isinstance(value, (list, tuple)):
        return [to_jsonable(item) for item in value]

    if hasattr(value, "tolist"):
        return to_jsonable(value.tolist())

    if isinstance(value, Path):
        return str(value)

    if isinstance(value, (str, int, float, bool)) or value is None:
        return value

    return str(value)


def find_key(value, key):
    if isinstance(value, dict):
        if key in value:
            return value[key]

        for nested in value.values():
            found = find_key(nested, key)
            if found is not None:
                return found

    if isinstance(value, list):
        for nested in value:
            found = find_key(nested, key)
            if found is not None:
                return found

    return None


def normalize_label(value):
    return str(value or "").strip().casefold().replace("-", "_").replace(" ", "_")


def normalize_text(value):
    value = unicodedata.normalize("NFC", str(value or "")).casefold()
    value = value.replace("’", "'").replace("‘", "'")
    value = value.replace("“", '"').replace("”", '"')
    value = re.sub(r"\s+", " ", value)
    return value.strip()


def validate_normalized_bounds(bounds, label):
    values = [
        bounds.get("left"),
        bounds.get("top"),
        bounds.get("right"),
        bounds.get("bottom"),
    ]

    if not all(
        isinstance(item, (int, float)) and math.isfinite(item)
        for item in values
    ):
        raise RuntimeError(f"Invalid finite bounds for {label}.")

    left, top, right, bottom = values

    if not (
        0.0 <= left < right <= 1.0
        and 0.0 <= top < bottom <= 1.0
    ):
        raise RuntimeError(f"Invalid normalized bounds for {label}.")


def pixel_to_normalized(bbox, width, height):
    if len(bbox) != 4:
        raise RuntimeError(f"Expected 4-value block_bbox, found {bbox!r}.")

    left, top, right, bottom = [float(item) for item in bbox]

    normalized = {
        "left": max(0.0, min(1.0, left / width)),
        "top": max(0.0, min(1.0, top / height)),
        "right": max(0.0, min(1.0, right / width)),
        "bottom": max(0.0, min(1.0, bottom / height)),
    }

    validate_normalized_bounds(normalized, "PP-StructureV3 block")
    return normalized


def area(bounds):
    return (
        max(0.0, bounds["right"] - bounds["left"])
        * max(0.0, bounds["bottom"] - bounds["top"])
    )


def intersection_area(a, b):
    width = max(
        0.0,
        min(a["right"], b["right"])
        - max(a["left"], b["left"]),
    )
    height = max(
        0.0,
        min(a["bottom"], b["bottom"])
        - max(a["top"], b["top"]),
    )
    return width * height


def overlap_metrics(predicted, expected):
    intersection = intersection_area(predicted, expected)
    predicted_area = area(predicted)
    expected_area = area(expected)
    union = predicted_area + expected_area - intersection

    return {
        "iou": intersection / union if union > 0 else 0.0,
        "expectedCoverage": (
            intersection / expected_area
            if expected_area > 0
            else 0.0
        ),
        "predictedCoverage": (
            intersection / predicted_area
            if predicted_area > 0
            else 0.0
        ),
    }


def overlap_score(metrics):
    # The human OCR-0H boxes intentionally cover only the discriminating part
    # of some larger text blocks. Expected-area coverage therefore matters more
    # than exact IoU for text, while IoU remains visible in the report.
    return max(
        metrics["iou"],
        metrics["expectedCoverage"],
    )


def center_inside(bounds, zone):
    center_x = (bounds["left"] + bounds["right"]) / 2.0
    center_y = (bounds["top"] + bounds["bottom"]) / 2.0

    return (
        zone["left"] <= center_x <= zone["right"]
        and zone["top"] <= center_y <= zone["bottom"]
    )


def best_match(blocks, expected_bounds, labels):
    candidates = []

    for block in blocks:
        if labels is not None and block["label"] not in labels:
            continue

        metrics = overlap_metrics(
            block["bounds"],
            expected_bounds,
        )

        candidates.append(
            (
                overlap_score(metrics),
                block,
                metrics,
            )
        )

    if not candidates:
        return None

    _, block, metrics = max(
        candidates,
        key=lambda item: (
            item[0],
            item[2]["expectedCoverage"],
            item[2]["iou"],
        ),
    )

    return {
        "block": block,
        "overlap": metrics,
        "score": overlap_score(metrics),
    }


def expected_region_map(structure):
    result = {
        item["id"]: item
        for item in structure["regions"]
    }

    required = {
        "section-title",
        "left-body",
        "right-opening",
        "facsimile",
        "caption",
    }

    missing = sorted(required - set(result))

    if missing:
        raise RuntimeError(
            f"Mixed-content structure missing regions: {missing}"
        )

    return result


def block_summary(block):
    return {
        "sequence": block["sequence"],
        "providedBlockOrder": block["providedBlockOrder"],
        "label": block["label"],
        "bbox": block["bbox"],
        "bounds": block["bounds"],
        "content": block["content"],
    }


def match_summary(match):
    if match is None:
        return None

    return {
        "score": match["score"],
        "overlap": match["overlap"],
        "block": block_summary(match["block"]),
    }


def x_overlap_ratio(a, b):
    overlap = max(
        0.0,
        min(a["right"], b["right"])
        - max(a["left"], b["left"]),
    )
    denominator = min(
        a["right"] - a["left"],
        b["right"] - b["left"],
    )

    return overlap / denominator if denominator > 0 else 0.0


def vertical_gap(upper, lower):
    return max(0.0, lower["top"] - upper["bottom"])


def annotate(image_path, blocks, expected, output_path):
    with Image.open(image_path) as source:
        image = source.convert("RGB")

    draw = ImageDraw.Draw(image)
    width, height = image.size

    for block in blocks:
        bounds = block["bounds"]
        box = (
            int(bounds["left"] * width),
            int(bounds["top"] * height),
            int(bounds["right"] * width),
            int(bounds["bottom"] * height),
        )
        draw.rectangle(box, width=4)
        draw.text(
            (box[0] + 4, box[1] + 4),
            f'{block["sequence"]}:{block["label"]}',
        )

    for region_id, item in expected.items():
        bounds = item["bounds"]
        box = (
            int(bounds["left"] * width),
            int(bounds["top"] * height),
            int(bounds["right"] * width),
            int(bounds["bottom"] * height),
        )
        draw.rectangle(box, width=8)
        draw.text(
            (box[0] + 4, max(0, box[1] - 18)),
            f"GT:{region_id}",
        )

    Path(output_path).parent.mkdir(parents=True, exist_ok=True)
    image.save(output_path, format="PNG")


def main():
    args = parse_args()

    input_index = read_json(args.input_index)
    structure = read_json(args.structure)

    if input_index.get("schemaVersion") != INPUT_INDEX_SCHEMA:
        raise RuntimeError("Unsupported OCR benchmark input-index schema.")

    if structure.get("schemaVersion") != STRUCTURE_SCHEMA:
        raise RuntimeError("Unsupported OCR-0H structure schema.")

    if input_index["benchmarkId"] != structure["benchmarkId"]:
        raise RuntimeError("Input index and structure benchmarkId differ.")

    if input_index["sourceSha256"] != structure["sourceSha256"]:
        raise RuntimeError("Input index and structure source SHA differ.")

    pages = [
        item
        for item in input_index["pages"]
        if item["pageNumber"] == structure["pageNumber"]
    ]

    if len(pages) != 1:
        raise RuntimeError("Expected exactly one rendered mixed-content page.")

    page = pages[0]
    image_path = Path(args.input_dir) / page["fileName"]

    if not image_path.is_file():
        raise RuntimeError(f"Rendered input image missing: {image_path}")

    if sha256_file(image_path) != page["sha256"]:
        raise RuntimeError("Rendered mixed-content image SHA mismatch.")

    expected = expected_region_map(structure)

    for region_id, item in expected.items():
        validate_normalized_bounds(item["bounds"], region_id)

    from paddleocr import PPStructureV3
    import paddle
    import paddleocr

    startup_started = time.perf_counter()

    pipeline = PPStructureV3(
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        use_seal_recognition=False,
        use_table_recognition=False,
        use_formula_recognition=False,
        use_chart_recognition=False,
        use_region_detection=True,
        format_block_content=False,
        device="cpu",
    )

    startup_ms = (time.perf_counter() - startup_started) * 1000.0

    inference_started = time.perf_counter()

    output = list(
        pipeline.predict(
            input=str(image_path),
            use_doc_orientation_classify=False,
            use_doc_unwarping=False,
            use_textline_orientation=False,
            use_seal_recognition=False,
            use_table_recognition=False,
            use_formula_recognition=False,
            use_chart_recognition=False,
            use_region_detection=True,
            format_block_content=False,
        )
    )

    inference_ms = (time.perf_counter() - inference_started) * 1000.0

    if len(output) != 1:
        raise RuntimeError(
            f"Expected one PP-StructureV3 result, received {len(output)}."
        )

    native = to_jsonable(output[0].json)
    parsing = find_key(native, "parsing_res_list")

    if not isinstance(parsing, list):
        raise RuntimeError(
            "PP-StructureV3 output does not contain parsing_res_list."
        )

    blocks = []

    for sequence, item in enumerate(parsing):
        if not isinstance(item, dict):
            raise RuntimeError("Unexpected non-object parsing block.")

        bbox = item.get("block_bbox")

        if bbox is None:
            raise RuntimeError(
                f"Parsing block {sequence} has no block_bbox."
            )

        block = {
            "sequence": sequence,
            "providedBlockOrder": item.get("block_order"),
            "label": normalize_label(item.get("block_label")),
            "bbox": [float(value) for value in bbox],
            "bounds": pixel_to_normalized(
                bbox,
                page["width"],
                page["height"],
            ),
            "content": str(item.get("block_content") or "").strip(),
        }

        blocks.append(block)

    raw_result = {
        "schemaVersion": RAW_SCHEMA,
        "benchmarkId": structure["benchmarkId"],
        "sourceSha256": structure["sourceSha256"],
        "pageNumber": structure["pageNumber"],
        "printedPageNumber": structure["printedPageNumber"],
        "input": {
            "fileName": page["fileName"],
            "sha256": page["sha256"],
            "width": page["width"],
            "height": page["height"],
        },
        "engine": {
            "id": "pp-structurev3",
            "paddleocrVersion": getattr(
                paddleocr,
                "__version__",
                "unknown",
            ),
            "paddleVersion": getattr(
                paddle,
                "__version__",
                "unknown",
            ),
            "device": "cpu",
            "configuration": {
                "useDocOrientationClassify": False,
                "useDocUnwarping": False,
                "useTextlineOrientation": False,
                "useSealRecognition": False,
                "useTableRecognition": False,
                "useFormulaRecognition": False,
                "useChartRecognition": False,
                "useRegionDetection": True,
                "formatBlockContent": False,
            },
        },
        "performance": {
            "startupMilliseconds": startup_ms,
            "inferenceMilliseconds": inference_ms,
        },
        "blocks": [block_summary(block) for block in blocks],
        "native": native,
    }

    figure_match = best_match(
        blocks,
        expected["facsimile"]["bounds"],
        FIGURE_LABELS,
    )
    caption_match = best_match(
        blocks,
        expected["caption"]["bounds"],
        CAPTION_LABELS,
    )
    title_match = best_match(
        blocks,
        expected["section-title"]["bounds"],
        HEADING_LABELS,
    )
    left_match = best_match(
        blocks,
        expected["left-body"]["bounds"],
        TEXT_LABELS,
    )
    right_match = best_match(
        blocks,
        expected["right-opening"]["bounds"],
        TEXT_LABELS,
    )

    facsimile_text_blocks = [
        block
        for block in blocks
        if (
            block["label"] in (TEXT_LABELS | HEADING_LABELS)
            and center_inside(
                block["bounds"],
                expected["facsimile"]["bounds"],
            )
        )
    ]

    imagine_blocks = [
        block
        for block in blocks
        if "imagine" in normalize_text(block["content"])
    ]
    for_example_blocks = [
        block
        for block in blocks
        if "for example" in normalize_text(block["content"])
    ]

    imagine_sequence = (
        imagine_blocks[-1]["sequence"]
        if imagine_blocks
        else None
    )
    for_example_sequence = (
        for_example_blocks[0]["sequence"]
        if for_example_blocks
        else None
    )

    sentinel_order = (
        imagine_sequence is not None
        and for_example_sequence is not None
        and imagine_sequence < for_example_sequence
    )

    spatial_order = (
        left_match is not None
        and right_match is not None
        and left_match["block"]["sequence"]
        < right_match["block"]["sequence"]
    )

    if sentinel_order:
        between_sentinels = [
            block
            for block in blocks
            if imagine_sequence < block["sequence"] < for_example_sequence
        ]
    else:
        between_sentinels = []

    non_narrative_between = [
        block
        for block in between_sentinels
        if block["label"] in NON_NARRATIVE_LABELS
    ]

    figure_caption_relation = None

    if figure_match is not None and caption_match is not None:
        figure_bounds = figure_match["block"]["bounds"]
        caption_bounds = caption_match["block"]["bounds"]

        figure_caption_relation = {
            "xOverlapRatio": x_overlap_ratio(
                figure_bounds,
                caption_bounds,
            ),
            "verticalGap": vertical_gap(
                figure_bounds,
                caption_bounds,
            ),
            "captionAfterFigureInReadingOrder": (
                figure_match["block"]["sequence"]
                < caption_match["block"]["sequence"]
            ),
        }

    gates = {
        "figureDetectedAsNonNarrative": (
            figure_match is not None
            and figure_match["score"] >= 0.50
        ),
        "noNarrativeTextBlockCenteredInsideFacsimile": (
            len(facsimile_text_blocks) == 0
        ),
        "captionSeparated": (
            caption_match is not None
            and caption_match["score"] >= 0.40
        ),
        "sectionTitleSeparated": (
            title_match is not None
            and title_match["score"] >= 0.40
        ),
        "leftModernTextDetected": (
            left_match is not None
            and left_match["score"] >= 0.40
        ),
        "rightModernTextDetected": (
            right_match is not None
            and right_match["score"] >= 0.40
        ),
        "modernTextReadingOrderUsable": spatial_order,
        "figureCaptionSpatialRelationPlausible": (
            figure_caption_relation is not None
            and figure_caption_relation["xOverlapRatio"] >= 0.40
            and figure_caption_relation["verticalGap"] <= 0.08
        ),
    }

    overall_pass = all(gates.values())

    peak_rss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    peak_bytes = (
        int(peak_rss * 1024)
        if sys.platform.startswith("linux")
        else int(peak_rss)
    )

    raw_result["performance"]["processPeakWorkingSetBytes"] = peak_bytes

    assessment = {
        "schemaVersion": ASSESSMENT_SCHEMA,
        "benchmarkId": structure["benchmarkId"],
        "sourceSha256": structure["sourceSha256"],
        "pageNumber": structure["pageNumber"],
        "engine": raw_result["engine"],
        "decision": {
            "overallPass": overall_pass,
            "interpretation": (
                "PASS means PP-StructureV3 produced enough structural evidence "
                "on this page to justify proceeding to a minimal production "
                "layout adapter/policy increment without benchmarking another "
                "layout engine. It is not a general production quality claim."
            ),
        },
        "gates": gates,
        "matches": {
            "facsimile": match_summary(figure_match),
            "caption": match_summary(caption_match),
            "sectionTitle": match_summary(title_match),
            "leftBody": match_summary(left_match),
            "rightOpening": match_summary(right_match),
        },
        "facsimile": {
            "textLikeParsingBlocksCenteredInside": [
                block_summary(block)
                for block in facsimile_text_blocks
            ],
        },
        "readingOrder": {
            "leftBodySequence": (
                left_match["block"]["sequence"]
                if left_match
                else None
            ),
            "rightOpeningSequence": (
                right_match["block"]["sequence"]
                if right_match
                else None
            ),
            "spatiallyMatchedModernTextOrderValid": spatial_order,
            "imagineSequence": imagine_sequence,
            "forExampleSequence": for_example_sequence,
            "sentinelOrderValid": sentinel_order,
            "nonNarrativeBlocksBetweenSentinels": [
                block_summary(block)
                for block in non_narrative_between
            ],
        },
        "figureCaptionRelation": figure_caption_relation,
        "observedLabels": sorted(
            {block["label"] for block in blocks}
        ),
        "blockCount": len(blocks),
        "performance": raw_result["performance"],
    }

    write_json(args.raw_output, raw_result)
    write_json(args.assessment_output, assessment)

    annotate(
        image_path,
        blocks,
        expected,
        args.annotated_output,
    )

    print()
    print("RESULT: LAYOUT-0A PP-STRUCTUREV3 EVALUATED")
    print(
        "Overall decision: "
        f'{"PASS" if overall_pass else "FAIL"}'
    )
    print(f"Parsing blocks: {len(blocks)}")
    print(
        "Observed labels: "
        + ", ".join(assessment["observedLabels"])
    )

    for name, value in gates.items():
        print(f"  {name}: {value}")

    print()
    print(
        "Facsimile text-like parsing blocks centered inside: "
        f"{len(facsimile_text_blocks)}"
    )

    if figure_match is not None:
        print(
            "Facsimile best block: "
            f'{figure_match["block"]["label"]} '
            f'seq={figure_match["block"]["sequence"]} '
            f'score={figure_match["score"]:.3f} '
            f'IoU={figure_match["overlap"]["iou"]:.3f}'
        )

    if caption_match is not None:
        print(
            "Caption best block: "
            f'{caption_match["block"]["label"]} '
            f'seq={caption_match["block"]["sequence"]} '
            f'score={caption_match["score"]:.3f} '
            f'IoU={caption_match["overlap"]["iou"]:.3f}'
        )

    print(
        "Modern spatial order left -> right: "
        f"{spatial_order}"
    )
    print(
        'Sentinels "Imagine" -> "for example": '
        f"{imagine_sequence} -> {for_example_sequence}; "
        f"valid={sentinel_order}"
    )

    if figure_caption_relation is not None:
        print(
            "Figure/caption x-overlap / vertical-gap: "
            f'{figure_caption_relation["xOverlapRatio"]:.3f} / '
            f'{figure_caption_relation["verticalGap"]:.3f}'
        )

    print(
        "Startup / inference ms: "
        f"{startup_ms:.1f} / {inference_ms:.1f}"
    )
    print(f"Peak process bytes: {peak_bytes}")
    print(f"Raw output: {Path(args.raw_output).resolve()}")
    print(
        "Assessment: "
        f"{Path(args.assessment_output).resolve()}"
    )
    print(
        "Annotated page: "
        f"{Path(args.annotated_output).resolve()}"
    )


if __name__ == "__main__":
    main()
