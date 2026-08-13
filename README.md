# Document Processing Engine

Document Processing Engine is a .NET-first library for transforming source
documents into structured, normalized, traceable, and quality-assessed
document representations.

The primary product is an in-process .NET library. An optional ASP.NET Core
server facade may be added later, but it is not the architectural center of the
project.

## V1 scope

V1 supports PDF processing only. The architecture nevertheless keeps the
document model and extraction contract generic so that future formats can be
added without changing the processing core.

The V1 architectural boundary explicitly excludes:

- RAG;
- embeddings;
- vector databases;
- retrieval chunking;
- ApologiaStudio-specific concepts;
- LLM or VLM processing;
- persistent document storage.

## Initial solution structure

```text
src/
  DocumentProcessing.Core/
  DocumentProcessing.Engine/
  DocumentProcessing.Pdf/

tests/
  DocumentProcessing.UnitTests/
  DocumentProcessing.IntegrationTests/
```

Dependency direction:

```text
Application / composition root
        |
        +--> DocumentProcessing.Engine --> DocumentProcessing.Core
        |
        +--> DocumentProcessing.Pdf -----> DocumentProcessing.Core
```
