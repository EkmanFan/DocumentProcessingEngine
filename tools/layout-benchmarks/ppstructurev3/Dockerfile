FROM paddlepaddle/paddle:3.2.2

RUN python -m pip install --no-cache-dir \
      "paddleocr[doc-parser]==3.7.0"

WORKDIR /workspace

ENTRYPOINT ["python", "/workspace/tools/layout-benchmarks/ppstructurev3/run_ppstructurev3_benchmark.py"]
