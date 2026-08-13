#!/usr/bin/env python3

import argparse
import json
import math
import re
import statistics
import unicodedata
from pathlib import Path


STRUCTURE_SCHEMA = "document-processing-ocr-mixed-content-structure-v1"
OCR_RESULT_SCHEMA = "document-processing-ocr-engine-result-v1"
GROUND_TRUTH_REPORT_SCHEMA = "document-processing-ocr-ground-truth-evaluation-v1"
REPORT_SCHEMA = "document-processing-ocr-mixed-content-evaluation-v1"


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--structure", required=True)
    parser.add_argument("--ocr-result", required=True)
    parser.add_argument("--ground-truth-report", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json(path, value):
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(
        json.dumps(value, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def normalized_search_text(value):
    value = unicodedata.normalize("NFC", value).casefold()
    value = value.replace("’", "'").replace("‘", "'")
    value = value.replace("“", '"').replace("”", '"')
    value = re.sub(r"\s+", " ", value)
    return value.strip()


def validate_bounds(bounds, label):
    values = [
        bounds.get("left"),
        bounds.get("top"),
        bounds.get("right"),
        bounds.get("bottom"),
    ]

    if not all(
        isinstance(value, (int, float)) and math.isfinite(value)
        for value in values
    ):
        raise RuntimeError(f"Invalid finite bounds for {label}.")

    left, top, right, bottom = values

    if not (
        0.0 <= left < right <= 1.0
        and 0.0 <= top < bottom <= 1.0
    ):
        raise RuntimeError(f"Invalid normalized bounds for {label}.")


def center_inside(region_bounds, zone_bounds):
    center_x = (
        float(region_bounds["left"])
        + float(region_bounds["right"])
    ) / 2.0
    center_y = (
        float(region_bounds["top"])
        + float(region_bounds["bottom"])
    ) / 2.0

    return (
        zone_bounds["left"] <= center_x <= zone_bounds["right"]
        and zone_bounds["top"] <= center_y <= zone_bounds["bottom"]
    )


def classify_region(region, structure_regions):
    matches = [
        item
        for item in structure_regions
        if center_inside(region["bounds"], item["bounds"])
    ]

    if len(matches) > 1:
        raise RuntimeError(
            f'OCR region sequence {region["sequence"]} falls in multiple '
            f'mixed-content zones: {[item["id"] for item in matches]}'
        )

    return matches[0] if matches else None


def sum_zone_metrics(zones):
    reference_chars = sum(
        zone["referenceCharacterCount"]
        for zone in zones
    )
    char_edits = sum(
        zone["characterEdits"]
        for zone in zones
    )
    reference_words = sum(
        zone["referenceWordCount"]
        for zone in zones
    )
    word_edits = sum(
        zone["wordEdits"]
        for zone in zones
    )

    return {
        "zoneCount": len(zones),
        "referenceCharacterCount": reference_chars,
        "characterEdits": char_edits,
        "characterErrorRate": (
            char_edits / reference_chars
            if reference_chars
            else 0.0
        ),
        "referenceWordCount": reference_words,
        "wordEdits": word_edits,
        "wordErrorRate": (
            word_edits / reference_words
            if reference_words
            else 0.0
        ),
    }


def main():
    args = parse_args()

    structure = read_json(args.structure)
    ocr_result = read_json(args.ocr_result)
    gt_report = read_json(args.ground_truth_report)

    if structure.get("schemaVersion") != STRUCTURE_SCHEMA:
        raise RuntimeError("Unsupported mixed-content structure schema.")

    if ocr_result.get("schemaVersion") != OCR_RESULT_SCHEMA:
        raise RuntimeError("Unsupported OCR result schema.")

    if gt_report.get("schemaVersion") != GROUND_TRUTH_REPORT_SCHEMA:
        raise RuntimeError("Unsupported ground-truth report schema.")

    benchmark_id = structure["benchmarkId"]
    source_sha = structure["sourceSha256"]

    for label, value in (
        ("OCR result benchmarkId", ocr_result.get("benchmarkId")),
        ("ground-truth report benchmarkId", gt_report.get("benchmarkId")),
    ):
        if value != benchmark_id:
            raise RuntimeError(f"{label} mismatch.")

    for label, value in (
        ("OCR result source SHA", ocr_result.get("sourceSha256")),
        ("ground-truth report source SHA", gt_report.get("sourceSha256")),
    ):
        if value != source_sha:
            raise RuntimeError(f"{label} mismatch.")

    page_number = structure["pageNumber"]

    pages = [
        page
        for page in ocr_result["pages"]
        if page["pageNumber"] == page_number
    ]

    if len(pages) != 1:
        raise RuntimeError(
            f"Expected exactly one OCR page {page_number}."
        )

    page = pages[0]

    if page["status"] != "Completed":
        raise RuntimeError(
            f"OCR page {page_number} did not complete."
        )

    structure_regions = structure["regions"]

    for item in structure_regions:
        validate_bounds(item["bounds"], item["id"])

    by_id = {
        item["id"]: item
        for item in structure_regions
    }

    if len(by_id) != len(structure_regions):
        raise RuntimeError("Duplicate mixed-content region ID.")

    classified = []

    for region in sorted(
        page["regions"],
        key=lambda item: item["sequence"],
    ):
        bucket = classify_region(region, structure_regions)

        classified.append(
            {
                "sequence": region["sequence"],
                "text": region["text"],
                "confidence": region.get("confidence"),
                "structureRegionId": (
                    bucket["id"] if bucket else None
                ),
                "kind": (
                    bucket["kind"] if bucket else "OutsideAnnotatedZones"
                ),
            }
        )

    zones = gt_report["zones"]
    zones_by_id = {
        zone["id"]: zone
        for zone in zones
    }

    modern_zone_ids = [
        "p233-section-title",
        "p233-left-body",
        "p233-right-opening",
    ]
    caption_zone_id = "p233-caption"

    missing_gt = [
        zone_id
        for zone_id in modern_zone_ids + [caption_zone_id]
        if zone_id not in zones_by_id
    ]

    if missing_gt:
        raise RuntimeError(
            f"Ground-truth report is missing zones: {missing_gt}"
        )

    modern_metrics = sum_zone_metrics(
        [zones_by_id[zone_id] for zone_id in modern_zone_ids]
    )
    caption_metrics = sum_zone_metrics(
        [zones_by_id[caption_zone_id]]
    )

    facsimile_regions = [
        item
        for item in classified
        if item["kind"] == "FacsimileImage"
    ]
    facsimile_text = "\n".join(
        item["text"]
        for item in facsimile_regions
        if str(item["text"]).strip()
    )
    facsimile_confidences = [
        float(item["confidence"])
        for item in facsimile_regions
        if item["confidence"] is not None
    ]

    continuity = structure["narrativeContinuity"]
    left_structure_id = continuity["leftRegionId"]
    right_structure_id = continuity["rightRegionId"]
    left_needle = normalized_search_text(
        continuity["leftNeedle"]
    )
    right_needle = normalized_search_text(
        continuity["rightNeedle"]
    )

    left_candidates = [
        item
        for item in classified
        if (
            item["structureRegionId"] == left_structure_id
            and left_needle in normalized_search_text(item["text"])
        )
    ]
    right_candidates = [
        item
        for item in classified
        if (
            item["structureRegionId"] == right_structure_id
            and right_needle in normalized_search_text(item["text"])
        )
    ]

    left_sequence = (
        left_candidates[-1]["sequence"]
        if left_candidates
        else None
    )
    right_sequence = (
        right_candidates[0]["sequence"]
        if right_candidates
        else None
    )

    continuity_order_valid = (
        left_sequence is not None
        and right_sequence is not None
        and left_sequence < right_sequence
    )

    if continuity_order_valid:
        between = [
            item
            for item in classified
            if left_sequence < item["sequence"] < right_sequence
        ]
    else:
        between = []

    excluded_kinds = set(
        continuity["excludedKinds"]
    )
    excluded_between = [
        item
        for item in between
        if item["kind"] in excluded_kinds
    ]

    if not continuity_order_valid:
        contamination_outcome = "NotEvaluated"
    elif excluded_between:
        contamination_outcome = "Detected"
    else:
        contamination_outcome = "NotDetected"

    report = {
        "schemaVersion": REPORT_SCHEMA,
        "benchmarkId": benchmark_id,
        "sourceSha256": source_sha,
        "pageNumber": page_number,
        "printedPageNumber": structure["printedPageNumber"],
        "engine": ocr_result["engine"],
        "modernPrintedText": modern_metrics,
        "caption": caption_metrics,
        "facsimile": {
            "groundTruthRole": "untrusted-non-narrative-image-region",
            "ocrRegionCount": len(facsimile_regions),
            "ocrCharacterCount": len(facsimile_text),
            "meanConfidence": (
                statistics.fmean(facsimile_confidences)
                if facsimile_confidences
                else None
            ),
            "minimumConfidence": (
                min(facsimile_confidences)
                if facsimile_confidences
                else None
            ),
            "textSample": facsimile_text[:240],
        },
        "narrativeContinuity": {
            "humanReading": continuity["humanReading"],
            "leftNeedleFound": bool(left_candidates),
            "rightNeedleFound": bool(right_candidates),
            "leftSequence": left_sequence,
            "rightSequence": right_sequence,
            "rawSequenceOrderValid": continuity_order_valid,
            "interveningRegionCount": len(between),
            "excludedNonNarrativeRegionCount": len(excluded_between),
            "excludedKinds": sorted(excluded_kinds),
            "facsimileOrCaptionContaminationOutcome": contamination_outcome,
            "intervening": between,
        },
        "rawPage": {
            "ocrRegionCount": len(page["regions"]),
            "classifiedRegionCount": sum(
                item["structureRegionId"] is not None
                for item in classified
            ),
            "outsideAnnotatedZoneRegionCount": sum(
                item["structureRegionId"] is None
                for item in classified
            ),
        },
    }

    write_json(args.output, report)

    def pct(value):
        return f"{value * 100:.3f}%"

    print()
    print("RESULT: OCR-0H EHRMAN MIXED-CONTENT EVALUATED")
    print(
        "Modern printed text CER / WER: "
        f'{pct(modern_metrics["characterErrorRate"])} / '
        f'{pct(modern_metrics["wordErrorRate"])}'
    )
    print(
        "Caption CER / WER: "
        f'{pct(caption_metrics["characterErrorRate"])} / '
        f'{pct(caption_metrics["wordErrorRate"])}'
    )
    print(
        "Facsimile OCR regions / chars: "
        f'{len(facsimile_regions)} / {len(facsimile_text)}'
    )

    if facsimile_confidences:
        print(
            "Facsimile OCR mean / min confidence: "
            f'{statistics.fmean(facsimile_confidences):.4f} / '
            f'{min(facsimile_confidences):.4f}'
        )

    print(
        'Continuity sentinels "Imagine" -> "for example": '
        f"left={left_sequence} right={right_sequence}"
    )
    print(
        "Raw sequence order valid: "
        f"{continuity_order_valid}"
    )
    print(
        "Facsimile/caption regions between sentinels: "
        f"{len(excluded_between)}"
    )
    print(
        "Naive narrative contamination outcome: "
        f"{contamination_outcome}"
    )
    print(
        "Important: facsimile OCR is informational and must not be "
        "treated as narrative truth."
    )
    print(f"Report: {Path(args.output).resolve()}")


if __name__ == "__main__":
    main()
