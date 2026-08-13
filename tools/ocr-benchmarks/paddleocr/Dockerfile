FROM paddlepaddle/paddle:3.2.2

RUN python -m pip install --no-cache-dir "paddleocr==3.7.0"

WORKDIR /workspace

ENTRYPOINT ["python", "/workspace/tools/ocr-benchmarks/paddleocr/run_paddleocr_benchmark.py"]
