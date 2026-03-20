# LangChain.Databases

Database abstractions and vector store backends for the [LangChain.NET](https://github.com/tryAGI/LangChain) ecosystem.

## Philosophy

This project aligns with the **Microsoft.Extensions** ecosystem. For vector search, we use [Microsoft.Extensions.VectorData.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions) (MEVA) — the industry-standard .NET vector data abstraction. For backends that have official [Semantic Kernel](https://github.com/microsoft/semantic-kernel) connectors, consumers should reference those packages directly rather than using wrappers.

## Packages

### Vector stores (MEVA implementations)

These packages implement `VectorStore` / `VectorStoreCollection<TKey, TRecord>` from `Microsoft.Extensions.VectorData`:

| Package | Backend | NuGet |
|---|---|---|
| `LangChain.Databases.Postgres` | PostgreSQL + pgvector | [![NuGet](https://img.shields.io/nuget/v/LangChain.Databases.Postgres)](https://www.nuget.org/packages/LangChain.Databases.Postgres) |
| `LangChain.Databases.OpenSearch` | OpenSearch k-NN | [![NuGet](https://img.shields.io/nuget/v/LangChain.Databases.OpenSearch)](https://www.nuget.org/packages/LangChain.Databases.OpenSearch) |

### Chat message history

| Package | Backend | NuGet |
|---|---|---|
| `LangChain.Databases.Abstractions` | In-memory, file-based | [![NuGet](https://img.shields.io/nuget/v/LangChain.Databases.Abstractions)](https://www.nuget.org/packages/LangChain.Databases.Abstractions) |
| `LangChain.Databases.Redis` | Redis | [![NuGet](https://img.shields.io/nuget/v/LangChain.Databases.Redis)](https://www.nuget.org/packages/LangChain.Databases.Redis) |
| `LangChain.Databases.Mongo` | MongoDB | [![NuGet](https://img.shields.io/nuget/v/LangChain.Databases.Mongo)](https://www.nuget.org/packages/LangChain.Databases.Mongo) |

### Use SK connectors for other backends

For backends with official Semantic Kernel connectors, use those packages directly:

| Backend | SK Connector Package |
|---|---|
| In-Memory | `Microsoft.SemanticKernel.Connectors.InMemory` |
| SQLite | `Microsoft.SemanticKernel.Connectors.Sqlite` |
| Chroma | `Microsoft.SemanticKernel.Connectors.Chroma` |
| Qdrant | `Microsoft.SemanticKernel.Connectors.Qdrant` |
| Pinecone | `Microsoft.SemanticKernel.Connectors.Pinecone` |
| Weaviate | `Microsoft.SemanticKernel.Connectors.Weaviate` |
| Milvus | `Microsoft.SemanticKernel.Connectors.Milvus` |
| DuckDB | `Microsoft.SemanticKernel.Connectors.DuckDB` |
| Azure AI Search | `Microsoft.SemanticKernel.Connectors.AzureAISearch` |
| Redis (vector) | `Microsoft.SemanticKernel.Connectors.Redis` |
| MongoDB (vector) | `Microsoft.SemanticKernel.Connectors.MongoDB` |

## Quick start

### 1. Define a record type

```csharp
using Microsoft.Extensions.VectorData;

public class DocumentRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

### 2. Create a store and collection

```csharp
// PostgreSQL + pgvector
var store = new PostgresVectorStore(connectionString);

// Or OpenSearch
var store = new OpenSearchVectorStore(new OpenSearchVectorDatabaseOptions
{
    ConnectionUri = new Uri("http://localhost:9200"),
});

// Or SK InMemory
var store = new InMemoryVectorStore();

var collection = store.GetCollection<string, DocumentRecord>("my_collection");
await collection.EnsureCollectionExistsAsync();
```

### 3. Upsert and search

```csharp
await collection.UpsertAsync(new DocumentRecord
{
    Text = "hello world",
    Embedding = embedding,
});

await foreach (var result in collection.SearchAsync(
    new ReadOnlyMemory<float>(queryEmbedding), top: 5))
{
    Console.WriteLine($"{result.Record.Text} (score: {result.Score})");
}
```

## Migration from IVectorDatabase

The legacy `IVectorDatabase` / `IVectorCollection` interfaces have been removed in favor of MEVA. See the [migration guide](docs/guides/migration-to-meva.md) for detailed before/after examples.

## License

This project is licensed under the [MIT License](LICENSE).
