#!/usr/bin/env python3

import argparse
import hashlib
import json
import os
import platform
import resource
import sys
import time
import traceback
from pathlib import Path

import paddle
import paddleocr
from paddleocr import PaddleOCR


SCHEMA = "document-processing-ocr-engine-result-v1"
EXPECTED_MANIFEST_SCHEMA = "document-processing-ocr-benchmark-manifest-v1"
EXPECTED_INDEX_SCHEMA = "document-processing-ocr-benchmark-input-index-v1"


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--input-index", required=True)
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json_atomic(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + f".tmp-{os.getpid()}")
    temporary.write_text(
        json.dumps(value, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def verify_inputs(manifest, index, input_dir):
    if manifest.get("schemaVersion") != EXPECTED_MANIFEST_SCHEMA:
        raise RuntimeError("Unsupported OCR benchmark manifest schema.")

    if index.get("schemaVersion") != EXPECTED_INDEX_SCHEMA:
        raise RuntimeError("Unsupported OCR benchmark input-index schema.")

    if index.get("benchmarkId") != manifest.get("benchmarkId"):
        raise RuntimeError("Input index benchmarkId differs from manifest.")

    if index.get("sourceSha256") != manifest["source"]["sha256"]:
        raise RuntimeError("Input index source SHA differs from manifest.")

    expected_pages = sorted(page["pageNumber"] for page in manifest["pages"])
    indexed_pages = sorted(page["pageNumber"] for page in index["pages"])

    if expected_pages != indexed_pages:
        raise RuntimeError("Input index page set differs from manifest.")

    input_dir = Path(input_dir)

    for page in index["pages"]:
        image_path = input_dir / page["fileName"]

        if not image_path.is_file():
            raise RuntimeError(f"Input image missing: {image_path}")

        digest = hashlib.sha256(image_path.read_bytes()).hexdigest()

        if digest != page["sha256"]:
            raise RuntimeError(
                f"Input SHA mismatch for page {page['pageNumber']}."
            )


def unwrap_result_json(value):
    if isinstance(value, dict):
        if "rec_texts" in value and "rec_boxes" in value:
            return value

        if "res" in value:
            found = unwrap_result_json(value["res"])
            if found is not None:
                return found

        for nested in value.values():
            found = unwrap_result_json(nested)
            if found is not None:
                return found

    return None


def normalize_region(sequence, text, score, box, width, height):
    if box is None or len(box) != 4:
        raise RuntimeError("Unexpected PaddleOCR rec_boxes shape.")

    left, top, right, bottom = [float(value) for value in box]

    left = min(max(left / width, 0.0), 1.0)
    right = min(max(right / width, 0.0), 1.0)
    top = min(max(top / height, 0.0), 1.0)
    bottom = min(max(bottom / height, 0.0), 1.0)

    return {
        "sequence": sequence,
        "text": str(text),
        "confidence": float(score) if score is not None else None,
        "bounds": {
            "left": left,
            "top": top,
            "right": right,
            "bottom": bottom,
        },
    }


def main():
    args = parse_args()

    manifest = read_json(args.manifest)
    index = read_json(args.input_index)

    verify_inputs(
        manifest,
        index,
        args.input_dir,
    )

    startup_started = time.perf_counter()

    ocr = PaddleOCR(
        text_detection_model_name="PP-OCRv6_medium_det",
        text_recognition_model_name="PP-OCRv6_medium_rec",
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        device="cpu",
    )

    startup_ms = (time.perf_counter() - startup_started) * 1000.0

    pages = []
    input_dir = Path(args.input_dir)
    first_failure_trace_printed = False

    for page in sorted(index["pages"], key=lambda item: item["pageNumber"]):
        page_number = page["pageNumber"]
        image_path = input_dir / page["fileName"]
        diagnostics = []

        started = time.perf_counter()

        try:
            prediction = list(ocr.predict(str(image_path)))
            elapsed_ms = (time.perf_counter() - started) * 1000.0

            if len(prediction) != 1:
                raise RuntimeError(
                    f"Expected one PaddleOCR result, received {len(prediction)}."
                )

            native = unwrap_result_json(prediction[0].json)

            if native is None:
                raise RuntimeError(
                    "Could not locate rec_texts/rec_scores/rec_boxes in PaddleOCR result."
                )

            texts = list(native.get("rec_texts", []))
            scores = list(native.get("rec_scores", []))
            boxes = list(native.get("rec_boxes", []))

            if not (len(texts) == len(scores) == len(boxes)):
                raise RuntimeError(
                    "PaddleOCR rec_texts/rec_scores/rec_boxes lengths differ."
                )

            regions = [
                normalize_region(
                    sequence,
                    text,
                    score,
                    box,
                    page["width"],
                    page["height"],
                )
                for sequence, (text, score, box) in enumerate(
                    zip(texts, scores, boxes)
                )
                if str(text).strip()
            ]

            status = "Completed"

        except Exception as exc:
            elapsed_ms = (time.perf_counter() - started) * 1000.0
            regions = []
            status = "Failed"
            diagnostic = f"{type(exc).__name__}: {exc}"
            diagnostics.append(diagnostic)

            print(
                f"p{page_number}: OCR ERROR: {diagnostic}",
                file=sys.stderr,
                flush=True,
            )

            if not first_failure_trace_printed:
                traceback.print_exc()
                first_failure_trace_printed = True

        pages.append(
            {
                "pageNumber": page_number,
                "inputSha256": page["sha256"],
                "status": status,
                "elapsedMilliseconds": elapsed_ms,
                "imageWidth": page["width"],
                "imageHeight": page["height"],
                "regions": regions,
                "diagnostics": diagnostics,
            }
        )

        print(
            f"p{page_number}: {status}, "
            f"regions={len(regions)}, elapsedMs={elapsed_ms:.1f}",
            flush=True,
        )

    peak_rss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss

    if sys.platform.startswith("linux"):
        peak_working_set_bytes = int(peak_rss * 1024)
    else:
        peak_working_set_bytes = int(peak_rss)

    result = {
        "schemaVersion": SCHEMA,
        "benchmarkId": manifest["benchmarkId"],
        "sourceSha256": manifest["source"]["sha256"],
        "engine": {
            "id": "paddleocr",
            "version": getattr(paddleocr, "__version__", "unknown"),
            "model": "PP-OCRv6_medium_det+PP-OCRv6_medium_rec",
            "backend": "paddle_static",
            "device": "cpu",
            "metadata": {
                "paddleVersion": getattr(paddle, "__version__", "unknown"),
                "pythonVersion": platform.python_version(),
                "platform": platform.platform(),
                "documentOrientation": "disabled",
                "documentUnwarping": "disabled",
                "textLineOrientation": "disabled",
            },
        },
        "performance": {
            "startupMilliseconds": startup_ms,
            "processPeakWorkingSetBytes": peak_working_set_bytes,
            "acceleratorPeakMemoryBytes": None,
        },
        "pages": pages,
    }

    write_json_atomic(
        args.output,
        result,
    )

    completed = sum(page["status"] == "Completed" for page in pages)
    failed = len(pages) - completed
    text_pages = sum(bool(page["regions"]) for page in pages)

    print()
    print("RESULT: PADDLEOCR PP-OCRV6 CPU BENCHMARK COMPLETE")
    print(f"PaddleOCR: {result['engine']['version']}")
    print(f"PaddlePaddle: {result['engine']['metadata']['paddleVersion']}")
    print(f"Startup ms: {startup_ms:.1f}")
    print(f"Completed / failed / text pages: {completed} / {failed} / {text_pages}")
    print(f"Output: {Path(args.output).resolve()}")


if __name__ == "__main__":
    main()
