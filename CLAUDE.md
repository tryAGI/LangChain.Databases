# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Database and vector store abstractions for the LangChain .NET ecosystem. Provides a unified `IVectorDatabase` / `IVectorCollection` interface with 17+ backend implementations. Designed as a standalone library with no dependency on LangChain.Core -- can be used independently. Distributed as individual NuGet packages per backend (e.g., `LangChain.Databases.Sqlite`, `LangChain.Databases.Postgres`).

## Build and Test Commands

```bash
# Build the entire solution
dotnet build LangChain.Databases.slnx

# Run integration tests (requires Docker for Testcontainers-based backends)
dotnet test src/IntegrationTests/LangChain.Databases.IntegrationTests.csproj

# Run a specific test
dotnet test src/IntegrationTests/LangChain.Databases.IntegrationTests.csproj --filter "FullyQualifiedName~Sqlite"

# Validate trimming/NativeAOT compatibility (requires: dotnet tool install -g autosdk.cli --prerelease)
autosdk trim src/libs/*//*.csproj
```

Integration tests for server-based backends (Postgres, Mongo, Redis, Elasticsearch, Milvus) use Testcontainers to spin up Docker instances.

## Architecture

### Project Structure

```
src/
├── Abstractions/src/          # LangChain.Databases.Abstractions — core interfaces
├── InMemory/src/              # In-memory vector database (no external deps)
├── Sqlite/src/                # SQLite-backed vector database
├── Postgres/src/              # PostgreSQL with pgvector
├── Chroma/src/                # ChromaDB client
├── Qdrant/src/                # Qdrant vector database
├── Pinecone/src/              # Pinecone cloud vector database
├── Weaviate/src/              # Weaviate vector database
├── Milvus/src/                # Milvus vector database
├── Mongo/src/                 # MongoDB with vector search
├── Redis/src/                 # Redis with vector similarity search
├── DuckDb/src/                # DuckDB-backed vector database
├── Elasticsearch/src/         # Elasticsearch vector search
├── OpenSearch/src/            # OpenSearch vector search
├── AzureCognitiveSearch/src/  # Azure Cognitive Search
├── AzureSearch/src/           # Azure AI Search
├── Kendra/src/                # AWS Kendra
├── SemanticKernel/src/        # Bridge to Microsoft Semantic Kernel vector stores
├── IntegrationTests/          # Tests covering all backends
```

### Core Abstractions (src/Abstractions/src/)

**`IVectorDatabase`** — top-level interface for vector store access:
- `GetOrCreateCollectionAsync(name, dimensions)` — get or create a named collection
- `GetCollectionAsync(name)` — retrieve existing collection
- `DeleteCollectionAsync(name)` — remove a collection
- `IsCollectionExistsAsync(name)` — check existence
- `ListCollectionsAsync()` — list all collections
- `CreateCollectionAsync(name, dimensions)` — create a new collection

**`IVectorCollection`** — operations on a single vector collection:
- `AddAsync(items)` — add vectors with text, embeddings, and metadata
- `SearchAsync(request, settings)` — similarity search by embedding vector
- `SearchByMetadata(filters)` — filter by metadata fields
- `GetAsync(id)` — retrieve a single vector by ID
- `DeleteAsync(ids)` — remove vectors by ID
- `IsEmptyAsync()` — check if empty

**`Vector`** — the core data record:
- `Text` (required) — the document text
- `Id` — unique identifier (auto-generated GUID by default)
- `Embedding` — float array of the vector embedding
- `Metadata` — key-value metadata dictionary
- `Distance` / `RelevanceScore` — search result scoring

**`VectorCollection`** — base class providing default collection name (`"langchain"`) and ID generation.

**`VectorSearchRequest`** / **`VectorSearchSettings`** — search configuration with distance strategy, result count, and search type.

**Message History** (`MessageHistory/`):
- `BaseChatMessageHistory` — abstract message history store
- `ChatMessageHistory` — in-memory message history
- `FileChatMessageHistory` — file-persisted message history

### Dependencies

The abstractions layer depends on:
- `LangChain.Providers.Abstractions` (NuGet) — for embedding model interfaces
- `LangChain.Polyfills` (NuGet) — framework polyfills

Several backends use Microsoft Semantic Kernel connectors under the hood (Sqlite, Chroma, DuckDB, Qdrant, Pinecone, Redis, Weaviate, Azure AI Search, Milvus).

### Adding a New Database Backend

1. Create a new directory `src/<BackendName>/src/`
2. Create `LangChain.Databases.<BackendName>.csproj` targeting `netstandard2.0;net8.0;net9.0`
3. Implement `IVectorDatabase` and `IVectorCollection`
4. Reference `Abstractions` project
5. Add integration tests in `src/IntegrationTests/`

## Key Conventions

- **Target frameworks:** `net4.6.2`, `netstandard2.0`, `net8.0`, `net9.0` (abstractions); some backends drop `net4.6.2`
- **Language:** C# preview, nullable reference types enabled, implicit usings
- **Strong naming:** All assemblies signed with `src/key.snk`
- **Versioning:** MinVer with `v` tag prefix
- **Central package management:** `src/Directory.Packages.props`
- **Testing:** MSTest with Testcontainers for server-based backends
- **Public API tracking:** `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` via Microsoft.CodeAnalysis.PublicApiAnalyzers
- Cross-project dependencies between LangChain ecosystem repos are via NuGet packages, not project references
