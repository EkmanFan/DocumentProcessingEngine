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


PAGE_NS = {"p": "http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15"}
MANIFEST_SCHEMA = "document-processing-ocr-diversification-manifest-v1"
REPORT_SCHEMA = "document-processing-ocr-diversification-report-v1"


def parse_args():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)

    select = sub.add_parser("select")
    select.add_argument("--dataset-root", required=True)
    select.add_argument("--upstream-repository", required=True)
    select.add_argument("--upstream-commit", required=True)
    select.add_argument("--output", required=True)

    run = sub.add_parser("run")
    run.add_argument("--dataset-root", required=True)
    run.add_argument("--manifest", required=True)
    run.add_argument("--output", required=True)

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
    temp = target.with_name(target.name + f".tmp-{os.getpid()}")
    temp.write_text(
        json.dumps(value, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    temp.replace(target)


def local_name(tag):
    return tag.rsplit("}", 1)[-1]


def parse_points(value):
    points = []
    for token in value.split():
        x, y = token.split(",", 1)
        points.append((float(x), float(y)))
    return points


def bbox_from_coords(element):
    coords = element.find("p:Coords", PAGE_NS)
    if coords is None:
        return None

    points = parse_points(coords.attrib.get("points", ""))
    if not points:
        return None

    xs = [point[0] for point in points]
    ys = [point[1] for point in points]

    return (min(xs), min(ys), max(xs), max(ys))


def direct_unicode(element):
    text_equiv = element.find("p:TextEquiv", PAGE_NS)
    if text_equiv is None:
        return None

    unicode_element = text_equiv.find("p:Unicode", PAGE_NS)
    if unicode_element is None or unicode_element.text is None:
        return None

    value = unicode_element.text.strip()
    return value if value else None


def page_reference(page):
    text_regions = {
        region.attrib["id"]: region
        for region in page.findall("p:TextRegion", PAGE_NS)
        if "id" in region.attrib
    }

    ordered_ids = []

    indexed = page.findall(
        ".//p:ReadingOrder//p:RegionRefIndexed",
        PAGE_NS,
    )

    for ref in sorted(indexed, key=lambda item: int(item.attrib["index"])):
        region_id = ref.attrib.get("regionRef")
        if region_id in text_regions:
            ordered_ids.append(region_id)

    if not ordered_ids:
        return None

    nonempty_regions = []
    region_text = {}

    for region_id, region in text_regions.items():
        text = direct_unicode(region)

        if text is None:
            line_values = []
            for line in region.findall("p:TextLine", PAGE_NS):
                line_text = direct_unicode(line)
                if line_text:
                    line_values.append(line_text)
            text = "\n".join(line_values).strip()

        if text:
            nonempty_regions.append(region_id)
            region_text[region_id] = text

    # We only benchmark pages where the explicit reading order covers all
    # non-empty TextRegions. This avoids inventing missing layout semantics.
    if set(nonempty_regions) != set(ordered_ids):
        return None

    reference = "\n".join(
        region_text[region_id]
        for region_id in ordered_ids
    ).strip()

    if not reference:
        return None

    return reference, ordered_ids, text_regions


def normalize_text(value):
    value = unicodedata.normalize("NFC", value)
    value = value.replace("\r\n", "\n").replace("\r", "\n")
    value = re.sub(r"[ \t\f\v]+", " ", value)
    value = re.sub(r"\s*\n\s*", " ", value)
    value = re.sub(r" +", " ", value)
    return value.strip()


def word_count(value):
    return len([part for part in value.split(" ") if part])


def parse_year(document_id):
    matches = re.findall(r"(1[5-8]\d{2}|1900)", document_id)
    return int(matches[-1]) if matches else None


def image_path_for_page(dataset_root, xml_path, page):
    declared = page.attrib.get("imageFilename")

    candidates = []

    if declared:
        declared_path = Path(declared)

        candidates.append(xml_path.parent / declared_path.name)
        candidates.append(xml_path.parent.parent / declared_path)

    stem = xml_path.stem

    for suffix in (".jpg", ".jpeg", ".png", ".tif", ".tiff"):
        candidates.append(xml_path.with_suffix(suffix))
        candidates.append(xml_path.parent / f"{stem}{suffix}")

    seen = set()
    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)

        if candidate.is_file():
            return candidate

    return None


def vertical_overlap(a, b):
    overlap = max(0.0, min(a[3], b[3]) - max(a[1], b[1]))
    denom = min(a[3] - a[1], b[3] - b[1])
    return overlap / denom if denom > 0 else 0.0


def multi_column_score(page_width, page_height, ordered_ids, text_regions):
    boxes = []

    for region_id in ordered_ids:
        bbox = bbox_from_coords(text_regions[region_id])
        if bbox is None:
            continue

        left, top, right, bottom = bbox
        width = right - left
        height = bottom - top

        if width < page_width * 0.18:
            continue
        if height < page_height * 0.08:
            continue
        if width > page_width * 0.70:
            continue

        boxes.append(bbox)

    best = 0.0

    for index, left_box in enumerate(boxes):
        for right_box in boxes[index + 1:]:
            left_center = (left_box[0] + left_box[2]) / 2.0
            right_center = (right_box[0] + right_box[2]) / 2.0
            separation = abs(left_center - right_center) / page_width

            if separation < 0.22:
                continue

            overlap = vertical_overlap(left_box, right_box)
            if overlap < 0.30:
                continue

            horizontal_overlap = max(
                0.0,
                min(left_box[2], right_box[2])
                - max(left_box[0], right_box[0]),
            )

            if horizontal_overlap > page_width * 0.05:
                continue

            score = separation + overlap
            best = max(best, score)

    return best


def collect_candidates(dataset_root):
    root = Path(dataset_root)
    candidates = []

    for xml_path in sorted(root.glob("data/*/GT-PAGE/*.xml")):
        document_id = xml_path.parents[1].name

        try:
            tree = ET.parse(xml_path)
        except ET.ParseError:
            continue

        page = tree.getroot().find("p:Page", PAGE_NS)
        if page is None:
            continue

        parsed = page_reference(page)
        if parsed is None:
            continue

        reference, ordered_ids, text_regions = parsed
        normalized = normalize_text(reference)

        if len(normalized) < 600:
            continue

        image_path = image_path_for_page(root, xml_path, page)
        if image_path is None:
            continue

        width = int(page.attrib.get("imageWidth", "0"))
        height = int(page.attrib.get("imageHeight", "0"))

        if width <= 0 or height <= 0:
            continue

        line_count = len(page.findall(".//p:TextLine", PAGE_NS))
        year = parse_year(document_id)
        column_score = multi_column_score(
            width,
            height,
            ordered_ids,
            text_regions,
        )

        candidates.append(
            {
                "documentId": document_id,
                "year": year,
                "xmlPath": xml_path,
                "imagePath": image_path,
                "pageType": page.attrib.get("type"),
                "imageWidth": width,
                "imageHeight": height,
                "reference": normalized,
                "referenceCharacterCount": len(normalized),
                "referenceWordCount": word_count(normalized),
                "textRegionCount": len(text_regions),
                "textLineCount": line_count,
                "multiColumnScore": column_score,
            }
        )

    return candidates


def choose_first(candidates, predicate, used):
    pool = [
        candidate
        for candidate in candidates
        if candidate["xmlPath"] not in used and predicate(candidate)
    ]

    if not pool:
        return None

    return sorted(
        pool,
        key=lambda candidate: (
            candidate["documentId"],
            candidate["xmlPath"].name,
        ),
    )[0]


def choose_specimens(candidates):
    if len(candidates) < 4:
        raise RuntimeError(
            f"Need at least four eligible OCR-D pages; found {len(candidates)}."
        )

    used = set()
    selected = []

    early = choose_first(
        candidates,
        lambda candidate: (
            candidate["year"] is not None
            and candidate["year"] <= 1550
            and candidate["referenceCharacterCount"] >= 1000
        ),
        used,
    )

    if early is None:
        raise RuntimeError("Could not select an early-print specimen.")

    selected.append(("early-print", early))
    used.add(early["xmlPath"])

    eighteenth = choose_first(
        candidates,
        lambda candidate: (
            candidate["year"] is not None
            and 1700 <= candidate["year"] <= 1799
            and candidate["referenceCharacterCount"] >= 1000
        ),
        used,
    )

    if eighteenth is None:
        raise RuntimeError("Could not select an eighteenth-century specimen.")

    selected.append(("eighteenth-century", eighteenth))
    used.add(eighteenth["xmlPath"])

    hilbert = next(
        (
            candidate
            for candidate in candidates
            if candidate["xmlPath"].name
            == "hilbert_zahlkoerper_1897_0379.xml"
        ),
        None,
    )

    if hilbert is not None and hilbert["xmlPath"] not in used:
        nineteenth = hilbert
    else:
        nineteenth = choose_first(
            candidates,
            lambda candidate: (
                candidate["year"] is not None
                and 1800 <= candidate["year"] <= 1900
                and candidate["referenceCharacterCount"] >= 1000
            ),
            used,
        )

    if nineteenth is None:
        raise RuntimeError("Could not select a nineteenth-century specimen.")

    selected.append(("nineteenth-century", nineteenth))
    used.add(nineteenth["xmlPath"])

    multi_pool = [
        candidate
        for candidate in candidates
        if (
            candidate["xmlPath"] not in used
            and candidate["multiColumnScore"] > 0
            and candidate["referenceCharacterCount"] >= 1000
        )
    ]

    if multi_pool:
        multi = sorted(
            multi_pool,
            key=lambda candidate: (
                -candidate["multiColumnScore"],
                candidate["documentId"],
                candidate["xmlPath"].name,
            ),
        )[0]
        category = "multi-column"
    else:
        # The fallback is explicit in the manifest. We do not pretend a page is
        # multi-column when the structural heuristic did not establish that.
        fallback_pool = [
            candidate
            for candidate in candidates
            if candidate["xmlPath"] not in used
        ]

        if not fallback_pool:
            raise RuntimeError("Could not select a fourth distinct specimen.")

        multi = sorted(
            fallback_pool,
            key=lambda candidate: (
                -candidate["textRegionCount"],
                -candidate["referenceCharacterCount"],
                candidate["documentId"],
                candidate["xmlPath"].name,
            ),
        )[0]
        category = "complex-layout-fallback"

    selected.append((category, multi))

    return selected


def command_select(args):
    dataset_root = Path(args.dataset_root).resolve()
    candidates = collect_candidates(dataset_root)
    selected = choose_specimens(candidates)

    specimens = []

    for ordinal, (category, candidate) in enumerate(selected):
        xml_path = candidate["xmlPath"].resolve()
        image_path = candidate["imagePath"].resolve()

        specimens.append(
            {
                "id": f"ocrd-{ordinal + 1:02d}",
                "category": category,
                "documentId": candidate["documentId"],
                "year": candidate["year"],
                "pageType": candidate["pageType"],
                "pageXmlPath": str(xml_path.relative_to(dataset_root)),
                "pageXmlSha256": sha256_file(xml_path),
                "imagePath": str(image_path.relative_to(dataset_root)),
                "imageSha256": sha256_file(image_path),
                "imageWidth": candidate["imageWidth"],
                "imageHeight": candidate["imageHeight"],
                "referenceCharacterCount": candidate[
                    "referenceCharacterCount"
                ],
                "referenceWordCount": candidate["referenceWordCount"],
                "textRegionCount": candidate["textRegionCount"],
                "textLineCount": candidate["textLineCount"],
                "multiColumnScore": round(
                    candidate["multiColumnScore"],
                    6,
                ),
            }
        )

    manifest = {
        "schemaVersion": MANIFEST_SCHEMA,
        "benchmarkId": "ocr-d-gt-structure-text-diversification-v1",
        "purpose": (
            "Diversify PaddleOCR validation beyond the Ehrman corpus using "
            "real historical scan images paired with PAGE-XML ground truth."
        ),
        "upstream": {
            "repository": args.upstream_repository,
            "commit": args.upstream_commit,
            "license": "CC-BY-SA-4.0",
            "dataset": "OCR-D/gt_structure_text",
        },
        "selection": {
            "eligiblePageCount": len(candidates),
            "algorithm": (
                "deterministic-four-specimen-v1:"
                "early-print;eighteenth-century;"
                "nineteenth-century;multi-column-or-explicit-fallback"
            ),
            "minimumNormalizedReferenceCharacters": 600,
            "requiresCompleteExplicitTextRegionReadingOrder": True,
        },
        "normalization": {
            "profile": "unicode-nfc-whitespace-v1",
            "caseSensitive": True,
            "historicalCharactersPreserved": True,
            "dehyphenation": False,
        },
        "specimens": specimens,
    }

    write_json(args.output, manifest)

    print("RESULT: OCR-D DIVERSIFICATION MANIFEST CREATED")
    print(f"Eligible pages: {len(candidates)}")

    for specimen in specimens:
        print(
            f'  {specimen["id"]} {specimen["category"]}: '
            f'{specimen["pageXmlPath"]} '
            f'chars={specimen["referenceCharacterCount"]} '
            f'words={specimen["referenceWordCount"]} '
            f'columns={specimen["multiColumnScore"]:.3f}'
        )

    print(f"Manifest: {Path(args.output).resolve()}")


def levenshtein(a, b):
    if len(a) < len(b):
        a, b = b, a

    previous = list(range(len(b) + 1))

    for row_index, a_value in enumerate(a, start=1):
        current = [row_index]

        for column_index, b_value in enumerate(b, start=1):
            insert_cost = current[column_index - 1] + 1
            delete_cost = previous[column_index] + 1
            replace_cost = previous[column_index - 1] + (
                0 if a_value == b_value else 1
            )
            current.append(
                min(insert_cost, delete_cost, replace_cost)
            )

        previous = current

    return previous[-1]


def page_reference_from_xml(xml_path):
    tree = ET.parse(xml_path)
    page = tree.getroot().find("p:Page", PAGE_NS)

    if page is None:
        raise RuntimeError(f"PAGE element missing: {xml_path}")

    parsed = page_reference(page)

    if parsed is None:
        raise RuntimeError(
            f"Explicit complete reading order missing: {xml_path}"
        )

    return normalize_text(parsed[0])


def unwrap_paddle_json(value):
    if isinstance(value, dict):
        if "rec_texts" in value:
            return value

        for nested in value.values():
            found = unwrap_paddle_json(nested)
            if found is not None:
                return found

    return None


def command_run(args):
    import paddle
    import paddleocr
    from paddleocr import PaddleOCR

    dataset_root = Path(args.dataset_root).resolve()
    manifest = read_json(args.manifest)

    if manifest.get("schemaVersion") != MANIFEST_SCHEMA:
        raise RuntimeError("Unsupported diversification manifest schema.")

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

    results = []
    total_elapsed_ms = 0.0
    total_reference_chars = 0
    total_reference_words = 0
    total_char_edits = 0
    total_word_edits = 0

    for specimen in manifest["specimens"]:
        xml_path = dataset_root / specimen["pageXmlPath"]
        image_path = dataset_root / specimen["imagePath"]

        if sha256_file(xml_path) != specimen["pageXmlSha256"]:
            raise RuntimeError(
                f'PAGE XML SHA mismatch for {specimen["id"]}.'
            )

        if sha256_file(image_path) != specimen["imageSha256"]:
            raise RuntimeError(
                f'Image SHA mismatch for {specimen["id"]}.'
            )

        reference = page_reference_from_xml(xml_path)

        run_started = time.perf_counter()
        predictions = list(ocr.predict(str(image_path)))
        elapsed_ms = (time.perf_counter() - run_started) * 1000.0
        total_elapsed_ms += elapsed_ms

        if len(predictions) != 1:
            raise RuntimeError(
                f'Expected one PaddleOCR result for {specimen["id"]}, '
                f"received {len(predictions)}."
            )

        native = unwrap_paddle_json(predictions[0].json)

        if native is None:
            raise RuntimeError(
                f'PaddleOCR rec_texts missing for {specimen["id"]}.'
            )

        recognized_lines = [
            str(value).strip()
            for value in native.get("rec_texts", [])
            if str(value).strip()
        ]

        recognized = normalize_text("\n".join(recognized_lines))

        reference_chars = len(reference)
        reference_tokens = [
            token for token in reference.split(" ") if token
        ]
        recognized_tokens = [
            token for token in recognized.split(" ") if token
        ]

        char_edits = levenshtein(reference, recognized)
        word_edits = levenshtein(
            reference_tokens,
            recognized_tokens,
        )

        cer = (
            char_edits / reference_chars
            if reference_chars
            else 0.0
        )
        wer = (
            word_edits / len(reference_tokens)
            if reference_tokens
            else 0.0
        )

        total_reference_chars += reference_chars
        total_reference_words += len(reference_tokens)
        total_char_edits += char_edits
        total_word_edits += word_edits

        result = {
            "id": specimen["id"],
            "category": specimen["category"],
            "documentId": specimen["documentId"],
            "year": specimen["year"],
            "imagePath": specimen["imagePath"],
            "referenceCharacterCount": reference_chars,
            "referenceWordCount": len(reference_tokens),
            "recognizedCharacterCount": len(recognized),
            "recognizedWordCount": len(recognized_tokens),
            "characterEdits": char_edits,
            "wordEdits": word_edits,
            "characterErrorRate": cer,
            "wordErrorRate": wer,
            "elapsedMilliseconds": elapsed_ms,
        }

        results.append(result)

        print(
            f'{specimen["id"]} {specimen["category"]}: '
            f'CER={cer * 100:.3f}% '
            f'WER={wer * 100:.3f}% '
            f'elapsedMs={elapsed_ms:.1f}',
            flush=True,
        )

    peak_rss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    peak_bytes = (
        int(peak_rss * 1024)
        if sys.platform.startswith("linux")
        else int(peak_rss)
    )

    aggregate_cer = (
        total_char_edits / total_reference_chars
        if total_reference_chars
        else 0.0
    )
    aggregate_wer = (
        total_word_edits / total_reference_words
        if total_reference_words
        else 0.0
    )

    report = {
        "schemaVersion": REPORT_SCHEMA,
        "benchmarkId": manifest["benchmarkId"],
        "upstream": manifest["upstream"],
        "engine": {
            "id": "paddleocr",
            "version": getattr(paddleocr, "__version__", "unknown"),
            "model": "PP-OCRv6_medium_det+PP-OCRv6_medium_rec",
            "backend": "paddle_static",
            "device": "cpu",
            "paddleVersion": getattr(paddle, "__version__", "unknown"),
        },
        "normalization": manifest["normalization"],
        "specimenCount": len(results),
        "referenceCharacterCount": total_reference_chars,
        "referenceWordCount": total_reference_words,
        "characterEdits": total_char_edits,
        "wordEdits": total_word_edits,
        "characterErrorRate": aggregate_cer,
        "wordErrorRate": aggregate_wer,
        "performance": {
            "startupMilliseconds": startup_ms,
            "ocrElapsedMilliseconds": total_elapsed_ms,
            "processPeakWorkingSetBytes": peak_bytes,
        },
        "specimens": results,
    }

    write_json(args.output, report)

    print()
    print("RESULT: OCR-0F PADDLEOCR DIVERSIFIED CORPUS COMPLETE")
    print(f"Specimens: {len(results)}")
    print(
        f"Aggregate CER / WER: "
        f"{aggregate_cer * 100:.3f}% / "
        f"{aggregate_wer * 100:.3f}%"
    )
    print(
        f"Reference chars / words: "
        f"{total_reference_chars} / "
        f"{total_reference_words}"
    )
    print(f"Startup ms: {startup_ms:.1f}")
    print(f"OCR elapsed ms: {total_elapsed_ms:.1f}")
    print(f"Peak process bytes: {peak_bytes}")
    print(f"Report: {Path(args.output).resolve()}")


def main():
    args = parse_args()

    if args.command == "select":
        command_select(args)
    elif args.command == "run":
        command_run(args)
    else:
        raise RuntimeError(f"Unsupported command: {args.command}")


if __name__ == "__main__":
    main()
