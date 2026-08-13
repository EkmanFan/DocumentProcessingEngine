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
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image


PAGE_NS = {
    "p": "http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15"
}

MANIFEST_SCHEMA = "document-processing-ocr-diversification-manifest-v1"
REPORT_SCHEMA = "document-processing-ocr-oracle-layout-report-v1"


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-root", required=True)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--specimen-id", default="ocrd-04")
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


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


def normalize_text(value):
    value = unicodedata.normalize("NFC", value)
    value = value.replace("\r\n", "\n").replace("\r", "\n")
    value = re.sub(r"[ \t\f\v]+", " ", value)
    value = re.sub(r"\s*\n\s*", " ", value)
    value = re.sub(r" +", " ", value)
    return value.strip()


def direct_unicode(element):
    text_equiv = element.find("p:TextEquiv", PAGE_NS)

    if text_equiv is None:
        return None

    unicode_element = text_equiv.find("p:Unicode", PAGE_NS)

    if unicode_element is None or unicode_element.text is None:
        return None

    value = unicode_element.text.strip()
    return value if value else None


def region_reference(region):
    direct = direct_unicode(region)

    if direct:
        return direct

    lines = []

    for line in region.findall("p:TextLine", PAGE_NS):
        text = direct_unicode(line)
        if text:
            lines.append(text)

    return "\n".join(lines).strip()


def parse_points(value):
    points = []

    for token in value.split():
        x, y = token.split(",", 1)
        points.append((float(x), float(y)))

    return points


def region_bbox(region):
    coords = region.find("p:Coords", PAGE_NS)

    if coords is None:
        raise RuntimeError(
            f'Region {region.attrib.get("id")} has no Coords.'
        )

    points = parse_points(coords.attrib.get("points", ""))

    if not points:
        raise RuntimeError(
            f'Region {region.attrib.get("id")} has empty coordinates.'
        )

    xs = [point[0] for point in points]
    ys = [point[1] for point in points]

    return (
        min(xs),
        min(ys),
        max(xs),
        max(ys),
    )


def ordered_text_regions(page):
    by_id = {
        region.attrib["id"]: region
        for region in page.findall("p:TextRegion", PAGE_NS)
        if "id" in region.attrib
    }

    refs = page.findall(
        ".//p:ReadingOrder//p:RegionRefIndexed",
        PAGE_NS,
    )

    if not refs:
        raise RuntimeError("PAGE XML has no explicit region reading order.")

    ordered = []

    for ref in sorted(refs, key=lambda item: int(item.attrib["index"])):
        region_id = ref.attrib.get("regionRef")

        if region_id not in by_id:
            raise RuntimeError(
                f"Reading order references non-TextRegion {region_id}."
            )

        text = region_reference(by_id[region_id])

        if not text:
            raise RuntimeError(
                f"Ordered TextRegion {region_id} has no ground-truth text."
            )

        ordered.append(
            {
                "index": int(ref.attrib["index"]),
                "id": region_id,
                "type": by_id[region_id].attrib.get("type"),
                "element": by_id[region_id],
                "reference": normalize_text(text),
            }
        )

    nonempty_ids = {
        region_id
        for region_id, region in by_id.items()
        if region_reference(region)
    }

    ordered_ids = {item["id"] for item in ordered}

    if nonempty_ids != ordered_ids:
        raise RuntimeError(
            "Explicit reading order does not cover every non-empty TextRegion."
        )

    return ordered


def levenshtein(a, b):
    if len(a) < len(b):
        a, b = b, a

    previous = list(range(len(b) + 1))

    for row_index, a_value in enumerate(a, start=1):
        current = [row_index]

        for column_index, b_value in enumerate(b, start=1):
            insertion = current[column_index - 1] + 1
            deletion = previous[column_index] + 1
            replacement = previous[column_index - 1] + (
                0 if a_value == b_value else 1
            )

            current.append(
                min(insertion, deletion, replacement)
            )

        previous = current

    return previous[-1]


def metrics(reference, recognized):
    reference = normalize_text(reference)
    recognized = normalize_text(recognized)

    reference_words = [
        token for token in reference.split(" ") if token
    ]
    recognized_words = [
        token for token in recognized.split(" ") if token
    ]

    char_edits = levenshtein(reference, recognized)
    word_edits = levenshtein(
        reference_words,
        recognized_words,
    )

    return {
        "referenceCharacterCount": len(reference),
        "recognizedCharacterCount": len(recognized),
        "referenceWordCount": len(reference_words),
        "recognizedWordCount": len(recognized_words),
        "characterEdits": char_edits,
        "wordEdits": word_edits,
        "characterErrorRate": (
            char_edits / len(reference)
            if reference
            else 0.0
        ),
        "wordErrorRate": (
            word_edits / len(reference_words)
            if reference_words
            else 0.0
        ),
    }


def unwrap_paddle_json(value):
    if isinstance(value, dict):
        if "rec_texts" in value:
            return value

        for nested in value.values():
            found = unwrap_paddle_json(nested)
            if found is not None:
                return found

    return None


def paddle_text(ocr, source):
    predictions = list(ocr.predict(source))

    if len(predictions) != 1:
        raise RuntimeError(
            f"Expected one PaddleOCR result, received {len(predictions)}."
        )

    native = unwrap_paddle_json(predictions[0].json)

    if native is None:
        raise RuntimeError("PaddleOCR result has no rec_texts.")

    lines = [
        str(value).strip()
        for value in native.get("rec_texts", [])
        if str(value).strip()
    ]

    return normalize_text("\n".join(lines))


def save_crop(image, bbox, target):
    width, height = image.size

    left = max(
        0,
        min(width - 1, math.floor(bbox[0])),
    )
    top = max(
        0,
        min(height - 1, math.floor(bbox[1])),
    )
    right = max(
        left + 1,
        min(width, math.ceil(bbox[2])),
    )
    bottom = max(
        top + 1,
        min(height, math.ceil(bbox[3])),
    )

    crop = image.crop((left, top, right, bottom))
    crop.save(target, format="PNG")

    return {
        "left": left,
        "top": top,
        "right": right,
        "bottom": bottom,
        "width": right - left,
        "height": bottom - top,
        "sha256": sha256_file(target),
    }


def main():
    args = parse_args()

    dataset_root = Path(args.dataset_root).resolve()
    manifest = read_json(args.manifest)

    if manifest.get("schemaVersion") != MANIFEST_SCHEMA:
        raise RuntimeError("Unsupported OCR-0F manifest schema.")

    specimen = next(
        (
            item
            for item in manifest["specimens"]
            if item["id"] == args.specimen_id
        ),
        None,
    )

    if specimen is None:
        raise RuntimeError(
            f"Specimen not found in manifest: {args.specimen_id}"
        )

    xml_path = dataset_root / specimen["pageXmlPath"]
    image_path = dataset_root / specimen["imagePath"]

    if sha256_file(xml_path) != specimen["pageXmlSha256"]:
        raise RuntimeError("PAGE XML SHA-256 mismatch.")

    if sha256_file(image_path) != specimen["imageSha256"]:
        raise RuntimeError("Source image SHA-256 mismatch.")

    tree = ET.parse(xml_path)
    page = tree.getroot().find("p:Page", PAGE_NS)

    if page is None:
        raise RuntimeError("PAGE element is missing.")

    regions = ordered_text_regions(page)

    if len(regions) != specimen["textRegionCount"]:
        raise RuntimeError(
            "Ordered TextRegion count differs from pinned manifest."
        )

    page_reference = normalize_text(
        "\n".join(item["reference"] for item in regions)
    )

    output_path = Path(args.output)
    crop_dir = output_path.parent / "oracle-region-crops"
    crop_dir.mkdir(parents=True, exist_ok=True)

    import paddle
    import paddleocr
    from paddleocr import PaddleOCR

    started = time.perf_counter()

    ocr = PaddleOCR(
        text_detection_model_name="PP-OCRv6_medium_det",
        text_recognition_model_name="PP-OCRv6_medium_rec",
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        device="cpu",
    )

    startup_ms = (time.perf_counter() - started) * 1000.0

    full_started = time.perf_counter()
    full_text = paddle_text(ocr, str(image_path))
    full_elapsed_ms = (time.perf_counter() - full_started) * 1000.0
    full_metrics = metrics(page_reference, full_text)

    with Image.open(image_path) as source:
        image = source.convert("RGB")

        if image.size != (
            specimen["imageWidth"],
            specimen["imageHeight"],
        ):
            raise RuntimeError("Source image dimensions differ from manifest.")

        recognized_regions = []
        region_reports = []
        oracle_elapsed_ms = 0.0

        for ordinal, region in enumerate(regions, start=1):
            crop_path = crop_dir / (
                f"{ordinal:02d}-{region['id']}.png"
            )

            crop = save_crop(
                image,
                region_bbox(region["element"]),
                crop_path,
            )

            region_started = time.perf_counter()
            recognized = paddle_text(
                ocr,
                str(crop_path),
            )
            elapsed_ms = (
                time.perf_counter() - region_started
            ) * 1000.0
            oracle_elapsed_ms += elapsed_ms

            recognized_regions.append(recognized)

            region_metric = metrics(
                region["reference"],
                recognized,
            )

            region_reports.append(
                {
                    "readingOrderIndex": region["index"],
                    "regionId": region["id"],
                    "regionType": region["type"],
                    "crop": crop,
                    "elapsedMilliseconds": elapsed_ms,
                    **region_metric,
                }
            )

            print(
                f'{ordinal:02d}/{len(regions)} '
                f'{region["id"]} '
                f'type={region["type"] or "unknown"} '
                f'CER={region_metric["characterErrorRate"] * 100:.3f}% '
                f'WER={region_metric["wordErrorRate"] * 100:.3f}% '
                f'chars={region_metric["recognizedCharacterCount"]}',
                flush=True,
            )

    oracle_text = normalize_text(
        "\n".join(recognized_regions)
    )
    oracle_metrics = metrics(
        page_reference,
        oracle_text,
    )

    peak_rss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    peak_bytes = (
        int(peak_rss * 1024)
        if sys.platform.startswith("linux")
        else int(peak_rss)
    )

    empty_regions = sum(
        1
        for item in region_reports
        if item["recognizedCharacterCount"] == 0
    )

    report = {
        "schemaVersion": REPORT_SCHEMA,
        "benchmarkId": (
            "ocr-d-gt-structure-text-ocrd04-oracle-layout-v1"
        ),
        "sourceBenchmarkId": manifest["benchmarkId"],
        "specimen": {
            "id": specimen["id"],
            "category": specimen["category"],
            "documentId": specimen["documentId"],
            "year": specimen["year"],
            "pageType": specimen["pageType"],
            "pageXmlPath": specimen["pageXmlPath"],
            "pageXmlSha256": specimen["pageXmlSha256"],
            "imagePath": specimen["imagePath"],
            "imageSha256": specimen["imageSha256"],
            "textRegionCount": specimen["textRegionCount"],
        },
        "engine": {
            "id": "paddleocr",
            "version": getattr(
                paddleocr,
                "__version__",
                "unknown",
            ),
            "model": "PP-OCRv6_medium_det+PP-OCRv6_medium_rec",
            "backend": "paddle_static",
            "device": "cpu",
            "paddleVersion": getattr(
                paddle,
                "__version__",
                "unknown",
            ),
        },
        "normalization": manifest["normalization"],
        "oracle": {
            "kind": "PAGE-XML TextRegion bounding boxes plus explicit region reading order",
            "cropPaddingPixels": 0,
            "cropRescale": "none",
            "regionCount": len(regions),
            "emptyRecognizedRegionCount": empty_regions,
        },
        "referenceCharacterCount": len(page_reference),
        "fullPage": {
            **full_metrics,
            "elapsedMilliseconds": full_elapsed_ms,
        },
        "oracleRegionLayout": {
            **oracle_metrics,
            "elapsedMilliseconds": oracle_elapsed_ms,
        },
        "performance": {
            "startupMilliseconds": startup_ms,
            "processPeakWorkingSetBytes": peak_bytes,
        },
        "regions": region_reports,
    }

    write_json(output_path, report)

    print()
    print("RESULT: OCR-0G ORACLE-LAYOUT COMPARISON")
    print(
        "Full page:      "
        f'CER={full_metrics["characterErrorRate"] * 100:.3f}% '
        f'WER={full_metrics["wordErrorRate"] * 100:.3f}%'
    )
    print(
        "Oracle regions: "
        f'CER={oracle_metrics["characterErrorRate"] * 100:.3f}% '
        f'WER={oracle_metrics["wordErrorRate"] * 100:.3f}%'
    )
    print(
        "Improvement:    "
        f'CER={(full_metrics["characterErrorRate"] - oracle_metrics["characterErrorRate"]) * 100:+.3f} pp '
        f'WER={(full_metrics["wordErrorRate"] - oracle_metrics["wordErrorRate"]) * 100:+.3f} pp'
    )
    print(
        f"Empty recognized regions: "
        f"{empty_regions}/{len(regions)}"
    )
    print(f"Report: {output_path.resolve()}")


if __name__ == "__main__":
    main()
