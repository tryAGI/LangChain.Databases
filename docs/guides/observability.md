# Observability with OpenTelemetry

LangChain.Databases includes built-in distributed tracing via `System.Diagnostics.ActivitySource`. All vector store operations emit activities that can be collected by any OpenTelemetry-compatible exporter.

## Activity sources

| Source name | Package |
|---|---|
| `LangChain.Databases.Postgres` | `LangChain.Databases.Postgres` |
| `LangChain.Databases.OpenSearch` | `LangChain.Databases.OpenSearch` |

## Operations traced

Both `VectorStore` and `VectorStoreCollection` operations are instrumented:

| Activity name | Description |
|---|---|
| `{system}.list_collections` | List all collection/index names |
| `{system}.collection_exists` | Check if a collection exists |
| `{system}.create_collection` | Create a collection (table/index) |
| `{system}.delete_collection` | Delete a collection |
| `{system}.get` | Retrieve a record by key |
| `{system}.upsert` | Insert or update a single record |
| `{system}.upsert_batch` | Batch insert or update records |
| `{system}.delete` | Delete a single record |
| `{system}.delete_batch` | Batch delete records |
| `{system}.search` | Vector similarity search |

Where `{system}` is `postgresql` or `opensearch`.

## Tags (attributes)

Each activity includes [OpenTelemetry Database semantic convention](https://opentelemetry.io/docs/specs/semconv/database/) tags:

| Tag | Description | Example |
|---|---|---|
| `db.system.name` | Database system identifier | `postgresql`, `opensearch` |
| `db.namespace` | Database name or host | `mydb`, `localhost` |
| `db.collection.name` | Table or index name | `documents` |

## Setup with .NET OpenTelemetry SDK

Install the OpenTelemetry packages:

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
# Or use OTLP exporter for Jaeger, Zipkin, etc.
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Configure tracing in your application:

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("LangChain.Databases.Postgres")
    .AddSource("LangChain.Databases.OpenSearch")
    .AddConsoleExporter()  // or .AddOtlpExporter()
    .Build();
```

Or with ASP.NET Core dependency injection:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("LangChain.Databases.Postgres")
        .AddSource("LangChain.Databases.OpenSearch")
        .AddOtlpExporter());
```

## Example output

With the console exporter, you'll see traces like:

```
Activity.TraceId:    abc123...
Activity.SpanId:     def456...
Activity.DisplayName: postgresql.search
Activity.Duration:   00:00:00.0234567
    db.system.name: postgresql
    db.namespace: mydb
    db.collection.name: documents
```

## Metadata via GetService

Both stores expose metadata through the MEVA `GetService` pattern:

```csharp
var store = new PostgresVectorStore(connectionString);
var metadata = store.GetService(typeof(VectorStoreMetadata)) as VectorStoreMetadata;
// metadata.VectorStoreSystemName == "postgresql"
// metadata.VectorStoreName == "mydb"

var collection = store.GetCollection<string, MyRecord>("documents");
var collMeta = collection.GetService(typeof(VectorStoreCollectionMetadata)) as VectorStoreCollectionMetadata;
// collMeta.VectorStoreSystemName == "postgresql"
// collMeta.VectorStoreName == "mydb"
// collMeta.CollectionName == "documents"
```
