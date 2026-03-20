# Migration Guide: IVectorDatabase to MEVA

This guide explains how to migrate from the legacy `IVectorDatabase` / `IVectorCollection` interfaces to the industry-standard **MEVA** (`Microsoft.Extensions.VectorData.Abstractions`) interfaces.

## Why migrate?

- **MEVA** is the standard .NET vector data abstraction, supported across Microsoft Semantic Kernel connectors and the broader .NET ecosystem
- The custom `IVectorDatabase` / `IVectorCollection` interfaces have been removed
- Most backends now have official Semantic Kernel MEVA connectors — no wrapper packages needed

## Package changes

### Removed packages

The following NuGet packages are no longer published. Replace them with SK connectors:

| Old package | Replacement |
|---|---|
| `LangChain.Databases.InMemory` | `Microsoft.SemanticKernel.Connectors.InMemory` |
| `LangChain.Databases.Sqlite` | `Microsoft.SemanticKernel.Connectors.Sqlite` |
| `LangChain.Databases.Chroma` | `Microsoft.SemanticKernel.Connectors.Chroma` |
| `LangChain.Databases.Qdrant` | `Microsoft.SemanticKernel.Connectors.Qdrant` |
| `LangChain.Databases.Pinecone` | `Microsoft.SemanticKernel.Connectors.Pinecone` |
| `LangChain.Databases.Weaviate` | `Microsoft.SemanticKernel.Connectors.Weaviate` |
| `LangChain.Databases.Milvus` | `Microsoft.SemanticKernel.Connectors.Milvus` |
| `LangChain.Databases.DuckDb` | `Microsoft.SemanticKernel.Connectors.DuckDB` |
| `LangChain.Databases.AzureSearch` | `Microsoft.SemanticKernel.Connectors.AzureAISearch` |
| `LangChain.Databases.Redis` (vector) | `Microsoft.SemanticKernel.Connectors.Redis` |
| `LangChain.Databases.SemanticKernel` | Not needed — use SK connectors directly |
| `LangChain.Databases.AzureCognitiveSearch` | `Microsoft.SemanticKernel.Connectors.AzureAISearch` |
| `LangChain.Databases.Elasticsearch` | Removed (was stubs only) |
| `LangChain.Databases.Kendra` | Removed (was stubs only) |

### Updated packages

| Package | Change |
|---|---|
| `LangChain.Databases.Postgres` | Now implements MEVA `VectorStore` / `VectorStoreCollection` |
| `LangChain.Databases.OpenSearch` | Now implements MEVA `VectorStore` / `VectorStoreCollection` |
| `LangChain.Databases.Redis` | Chat history only — `RedisChatMessageHistory` unchanged |
| `LangChain.Databases.Mongo` | Chat history only — `MongoChatMessageHistory` unchanged |
| `LangChain.Databases.Abstractions` | Chat history only — all vector DB types removed |

## Code migration

### Define a record type

MEVA uses strongly-typed records instead of the generic `Vector` class. Define your record with attributes:

```csharp
using Microsoft.Extensions.VectorData;

public class DocumentRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string? Category { get; set; }

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

### Creating a vector store and collection

**Before:**
```csharp
IVectorDatabase db = new PostgresVectorDatabase(connectionString);
IVectorCollection collection = await db.GetOrCreateCollectionAsync("docs", dimensions: 1536);
```

**After:**
```csharp
var store = new PostgresVectorStore(connectionString);
var collection = store.GetCollection<string, DocumentRecord>("docs");
await collection.EnsureCollectionExistsAsync();
```

Or with SK InMemory:
```csharp
var store = new InMemoryVectorStore();
var collection = store.GetCollection<string, DocumentRecord>("docs");
await collection.EnsureCollectionExistsAsync();
```

### Adding records

**Before:**
```csharp
await collection.AddAsync(new[]
{
    new Vector
    {
        Text = "hello world",
        Embedding = embedding,
        Metadata = new Dictionary<string, object> { ["category"] = "greeting" },
    }
});
```

**After:**
```csharp
await collection.UpsertAsync(new DocumentRecord
{
    Text = "hello world",
    Embedding = embedding,
    Category = "greeting",
});
```

### Retrieving records

**Before:**
```csharp
Vector? item = await collection.GetAsync(id);
string text = item?.Text;
```

**After:**
```csharp
DocumentRecord? item = await collection.GetAsync(id);
string text = item?.Text;
```

### Similarity search

**Before:**
```csharp
var response = await collection.SearchAsync(
    new VectorSearchRequest { Embeddings = new[] { queryEmbedding } },
    new VectorSearchSettings { NumberOfResults = 5 });

foreach (var item in response.Items)
{
    Console.WriteLine($"{item.Text} (distance: {item.Distance})");
}
```

**After:**
```csharp
await foreach (var result in collection.SearchAsync(
    new ReadOnlyMemory<float>(queryEmbedding),
    top: 5))
{
    Console.WriteLine($"{result.Record.Text} (score: {result.Score})");
}
```

### Deleting records

**Before:**
```csharp
await collection.DeleteAsync(new[] { id1, id2 });
```

**After:**
```csharp
await collection.DeleteAsync(id1);
await collection.DeleteAsync(id2);
```

### Checking / deleting collections

**Before:**
```csharp
bool exists = await db.IsCollectionExistsAsync("docs");
var names = await db.ListCollectionsAsync();
await db.DeleteCollectionAsync("docs");
```

**After:**
```csharp
bool exists = await store.CollectionExistsAsync("docs");
await foreach (var name in store.ListCollectionNamesAsync()) { ... }
await store.EnsureCollectionDeletedAsync("docs");
```

## Chat message history

Chat message history is **unchanged**. `BaseChatMessageHistory`, `ChatMessageHistory`, `FileChatMessageHistory`, `RedisChatMessageHistory`, and `MongoChatMessageHistory` all work exactly as before.

## Key differences

| Aspect | Old (`IVectorDatabase`) | New (MEVA) |
|---|---|---|
| Record type | Generic `Vector` class | Strongly-typed with attributes |
| Search results | `VectorSearchResponse.Items` | `IAsyncEnumerable<VectorSearchResult<T>>` |
| Scoring | `Distance` / `RelevanceScore` on `Vector` | `Score` on `VectorSearchResult` |
| Metadata | `Dictionary<string, object>` | Typed properties with `[VectorStoreData]` |
| Collection creation | `GetOrCreateCollectionAsync(name, dims)` | `GetCollection<K,V>(name)` + `EnsureCollectionExistsAsync()` |
| Batch add | `AddAsync(IReadOnlyCollection<Vector>)` | `UpsertAsync(IEnumerable<T>)` |
| Listing collections | Returns `IReadOnlyList<string>` | Returns `IAsyncEnumerable<string>` |
