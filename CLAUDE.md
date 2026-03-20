# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Database abstractions and vector store backends for the LangChain .NET ecosystem. Vector store backends implement **MEVA** (`Microsoft.Extensions.VectorData.Abstractions`) interfaces (`VectorStore` / `VectorStoreCollection<TKey, TRecord>`). The Abstractions package provides chat message history only. For vector search with backends that have official Semantic Kernel connectors (InMemory, SQLite, Chroma, Qdrant, Pinecone, Weaviate, Milvus, Redis, Azure AI Search, DuckDB), consumers should reference the SK connector packages directly.

## Build and Test Commands

```bash
# Build the entire solution
dotnet build LangChain.Databases.slnx

# Run integration tests (requires Docker for Testcontainers-based backends)
dotnet test src/IntegrationTests/LangChain.Databases.IntegrationTests.csproj

# Run a specific test
dotnet test src/IntegrationTests/LangChain.Databases.IntegrationTests.csproj --filter "FullyQualifiedName~Postgres"
```

Integration tests for server-based backends (Postgres, Mongo, Redis) use Testcontainers to spin up Docker instances. InMemory tests use `Microsoft.SemanticKernel.Connectors.InMemory` directly.

## Architecture

### Project Structure

```
src/
├── Abstractions/src/    # LangChain.Databases.Abstractions — message history only
├── Postgres/src/        # PostgreSQL + pgvector — MEVA VectorStore implementation
├── OpenSearch/src/      # OpenSearch k-NN — MEVA VectorStore implementation
├── Mongo/src/           # MongoDB — chat message history only
├── Redis/src/           # Redis — chat message history only
├── IntegrationTests/    # Tests covering all remaining backends
```

### Abstractions (src/Abstractions/src/)

Contains **only** chat message history types (vector DB interfaces have been removed in favor of MEVA):

- `BaseChatMessageHistory` — abstract message history store
- `ChatMessageHistory` — in-memory message history
- `FileChatMessageHistory` — file-persisted message history

Dependencies: `LangChain.Providers.Abstractions` (for `Message` type), `LangChain.Polyfills`

### MEVA Vector Store Backends

Postgres and OpenSearch implement MEVA interfaces directly:

**Postgres** (`LangChain.Databases.Postgres`):
- `PostgresVectorStore : VectorStore` — collection management via pgvector
- `PostgresVectorStoreCollection<TRecord> : VectorStoreCollection<string, TRecord>` — CRUD + similarity search
- `PostgresDbClient` — low-level Npgsql client with pgvector support
- `PostgresRecordMapper<TRecord>` — reflection-based mapping for MEVA-attributed records
- Supports cosine, euclidean, and inner product distance strategies
- Batch upsert via transactions

**OpenSearch** (`LangChain.Databases.OpenSearch`):
- `OpenSearchVectorStore : VectorStore` — index management
- `OpenSearchVectorStoreCollection<TRecord> : VectorStoreCollection<string, TRecord>` — CRUD + k-NN search
- Uses `OpenSearch.Client` with k-NN plugin
- Reflection-based record mapping for MEVA attributes

### Chat History Backends

**Redis** (`LangChain.Databases.Redis`):
- `RedisChatMessageHistory : BaseChatMessageHistory` — Redis list-backed message store with TTL support

**Mongo** (`LangChain.Databases.Mongo`):
- `MongoChatMessageHistory : BaseChatMessageHistory` — MongoDB-backed message store
- Includes `MongoDbClient`, `MongoContext` infrastructure

### MEVA Record Pattern

Vector store backends expect records decorated with MEVA attributes:

```csharp
public class MyRecord
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData]
    public string Text { get; set; }

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

Usage:
```csharp
var store = new PostgresVectorStore(connectionString);
var collection = store.GetCollection<string, MyRecord>("my_collection");
await collection.EnsureCollectionExistsAsync();
await collection.UpsertAsync(record);
await foreach (var result in collection.SearchAsync(embedding, top: 5)) { ... }
```

### Removed Backends

The following wrapper projects have been removed — use the corresponding SK connector packages directly:
- InMemory → `Microsoft.SemanticKernel.Connectors.InMemory`
- SQLite → `Microsoft.SemanticKernel.Connectors.Sqlite`
- Chroma → `Microsoft.SemanticKernel.Connectors.Chroma`
- Qdrant → `Microsoft.SemanticKernel.Connectors.Qdrant`
- Pinecone → `Microsoft.SemanticKernel.Connectors.Pinecone`
- Weaviate → `Microsoft.SemanticKernel.Connectors.Weaviate`
- Milvus → `Microsoft.SemanticKernel.Connectors.Milvus`
- DuckDB → `Microsoft.SemanticKernel.Connectors.DuckDB`
- Azure AI Search → `Microsoft.SemanticKernel.Connectors.AzureAISearch`
- Redis (vector) → `Microsoft.SemanticKernel.Connectors.Redis`
- Elasticsearch, AzureCognitiveSearch, Kendra, SemanticKernel bridge — removed entirely

## Key Conventions

- **Target frameworks:** `net4.6.2`, `netstandard2.0`, `net8.0`, `net9.0` (abstractions); `net8.0`, `net9.0` (Postgres, Redis); `net4.6.2`, `netstandard2.0`, `net8.0`, `net9.0` (OpenSearch, Mongo)
- **Language:** C# preview, nullable reference types enabled, implicit usings
- **Strong naming:** All assemblies signed with `src/key.snk`
- **Versioning:** MinVer with `v` tag prefix
- **Central package management:** `src/Directory.Packages.props`
- **Testing:** NUnit with Testcontainers for server-based backends
- **Public API tracking:** `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` via Microsoft.CodeAnalysis.PublicApiAnalyzers
- Cross-project dependencies between LangChain ecosystem repos are via NuGet packages, not project references
