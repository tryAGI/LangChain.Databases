using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using OpenSearch.Client;
using OpenSearch.Net;

namespace LangChain.Databases.OpenSearch;

/// <summary>
/// MEVA-compatible vector store collection backed by an OpenSearch index with k-NN.
/// </summary>
public class OpenSearchVectorStoreCollection<TRecord> :
    VectorStoreCollection<string, TRecord>
    where TRecord : class
{
    private readonly IOpenSearchClient _client;
    private readonly string _name;
    private readonly VectorStoreCollectionDefinition? _definition;
    private readonly int _dimensions;
    private readonly string? _vectorStoreName;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public OpenSearchVectorStoreCollection(
        IOpenSearchClient client,
        string name,
        VectorStoreCollectionDefinition? definition = null,
        string? vectorStoreName = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _definition = definition;
        _dimensions = ResolveVectorDimensions(definition);
        _vectorStoreName = vectorStoreName;
    }

    private static int ResolveVectorDimensions(VectorStoreCollectionDefinition? definition)
    {
        // 1. Try from definition
        var fromDef = definition?.Properties
            .OfType<VectorStoreVectorProperty>()
            .FirstOrDefault()?.Dimensions;
        if (fromDef is > 0)
        {
            return fromDef.Value;
        }

        // 2. Try from [VectorStoreVector] attribute on TRecord
        foreach (var prop in typeof(TRecord).GetProperties())
        {
            var attr = prop.GetCustomAttributes(typeof(VectorStoreVectorAttribute), false);
            if (attr.Length > 0 && attr[0] is VectorStoreVectorAttribute vectorAttr && vectorAttr.Dimensions > 0)
            {
                return vectorAttr.Dimensions;
            }
        }

        // 3. Default
        return 1536;
    }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("collection_exists");
        try
        {
            var response = await _client.Indices.ExistsAsync(_name, ct: cancellationToken).ConfigureAwait(false);
            return response.Exists;
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("create_collection");
        try
        {
            if (await CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var response = await _client.Indices.CreateAsync(_name, c => c
                .Settings(x => x
                    .Setting("index.knn", true)
                )
                .Map<VectorRecord>(m => m
                    .Properties(p => p
                        .Keyword(k => k.Name(n => n.Id))
                        .Nested<Dictionary<string, object>>(n => n.Name(x => x.Metadata))
                        .Text(t => t.Name(n => n.Text))
                        .KnnVector(d => d.Name(n => n.Vector).Dimension(_dimensions).Similarity("cosine"))
                    )
                ), cancellationToken).ConfigureAwait(false);

            if (!response.IsValid)
            {
                throw new InvalidOperationException(
                    $"Failed to create index '{_name}'. {response.DebugInformation}");
            }
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("delete_collection");
        try
        {
            var exists = await CollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                await _client.Indices.DeleteAsync(
                    new DeleteIndexRequest(_name), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<TRecord?> GetAsync(
        string key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("get");
        try
        {
            var response = await _client.GetAsync<VectorRecord>(
                new GetRequest(_name, key), cancellationToken).ConfigureAwait(false);

            if (!response.Found || response.Source == null)
            {
                return default;
            }

            return MapFromVectorRecord(response.Source);
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("delete");
        try
        {
            await _client.DeleteAsync(
                new DeleteRequest(_name, key), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        keys = keys ?? throw new ArgumentNullException(nameof(keys));

        using var activity = StartActivity("delete_batch");
        try
        {
            var bulkDescriptor = new BulkDescriptor().Refresh(Refresh.WaitFor);
            var count = 0;

            foreach (var key in keys)
            {
                bulkDescriptor.Delete<VectorRecord>(d => d.Index(_name).Id(key));
                count++;
            }

            if (count == 0)
            {
                return;
            }

            var response = await _client.BulkAsync(bulkDescriptor, cancellationToken).ConfigureAwait(false);
            if (response.Errors)
            {
                var itemErrors = response.ItemsWithErrors
                    .Select(item => $"[{item.Id}] {item.Error?.Reason ?? "Unknown error"}")
                    .ToList();

                throw new InvalidOperationException(
                    $"Failed to bulk delete {itemErrors.Count} document(s) from index '{_name}': " +
                    string.Join("; ", itemErrors));
            }
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        record = record ?? throw new ArgumentNullException(nameof(record));

        using var activity = StartActivity("upsert");
        try
        {
            var vectorRecord = MapToVectorRecord(record);

            var response = await _client.IndexAsync(
                vectorRecord,
                i => i.Index(_name).Id(vectorRecord.Id).Refresh(Refresh.WaitFor),
                cancellationToken).ConfigureAwait(false);

            if (!response.IsValid)
            {
                throw new InvalidOperationException(
                    $"Failed to upsert document. {response.DebugInformation}");
            }
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(
        IEnumerable<TRecord> records,
        CancellationToken cancellationToken = default)
    {
        records = records ?? throw new ArgumentNullException(nameof(records));

        using var activity = StartActivity("upsert_batch");
        try
        {
            var bulkDescriptor = new BulkDescriptor().Refresh(Refresh.WaitFor);

            foreach (var record in records)
            {
                var vectorRecord = MapToVectorRecord(record);
                bulkDescriptor.Index<VectorRecord>(
                    idx => idx.Document(vectorRecord).Index(_name));
            }

            var response = await _client.BulkAsync(bulkDescriptor, cancellationToken).ConfigureAwait(false);
            if (response.Errors)
            {
                var itemErrors = response.ItemsWithErrors
                    .Select(item => $"[{item.Id}] {item.Error?.Reason ?? "Unknown error"}")
                    .ToList();

                throw new InvalidOperationException(
                    $"Failed to bulk upsert {itemErrors.Count} document(s) to index '{_name}': " +
                    string.Join("; ", itemErrors));
            }
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Expression-based filtering is not supported by OpenSearchVectorStoreCollection.");
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("search");

        float[] embedding;

        if (searchValue is ReadOnlyMemory<float> floatMemory)
        {
            embedding = floatMemory.ToArray();
        }
        else if (searchValue is float[] floatArray)
        {
            embedding = floatArray;
        }
        else
        {
            throw new NotSupportedException(
                $"Search input type '{typeof(TInput).Name}' is not supported. " +
                "Use ReadOnlyMemory<float> or float[] for pre-computed embeddings.");
        }

        ISearchResponse<VectorRecord> response;
        try
        {
            response = await _client.SearchAsync<VectorRecord>(s => s
                .Index(_name)
                .Query(q => q
                    .Knn(knn => knn
                        .Field(f => f.Vector)
                        .Vector(embedding)
                        .K(top)
                    )
                ), cancellationToken).ConfigureAwait(false);

            if (!response.IsValid)
            {
                throw new InvalidOperationException(
                    $"Failed to search index '{_name}'. {response.DebugInformation}");
            }
        }
        catch (Exception ex)
        {
            OpenSearchTelemetry.SetError(activity, ex);
            throw;
        }

        foreach (var hit in response.Hits)
        {
            if (hit.Source == null || string.IsNullOrWhiteSpace(hit.Source.Text))
            {
                continue;
            }

            var mapped = MapFromVectorRecord(hit.Source);
            if (mapped != null)
            {
                yield return new VectorSearchResult<TRecord>(mapped, hit.Score ?? 0.0);
            }
        }
    }

    private Activity? StartActivity(string operationName)
    {
        var activity = OpenSearchTelemetry.ActivitySource.StartActivity(
            $"{OpenSearchTelemetry.SystemName}.{operationName}");
        if (activity is not null)
        {
            activity.SetTag("db.system.name", OpenSearchTelemetry.SystemName);
            activity.SetTag("db.collection.name", _name);
            if (_vectorStoreName is not null)
            {
                activity.SetTag("db.namespace", _vectorStoreName);
            }
        }

        return activity;
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is not null)
        {
            return null;
        }

        if (serviceType == typeof(VectorStoreCollectionMetadata))
        {
            return new VectorStoreCollectionMetadata
            {
                VectorStoreSystemName = "opensearch",
                VectorStoreName = _vectorStoreName,
                CollectionName = _name,
            };
        }

        return null;
    }

    /// <summary>
    /// Maps a <typeparamref name="TRecord"/> to a <see cref="VectorRecord"/> for storage.
    /// Override this for custom record types. Default implementation works with <see cref="VectorRecord"/>.
    /// </summary>
    protected virtual VectorRecord MapToVectorRecord(TRecord record)
    {
        if (record is VectorRecord vr)
        {
            return vr;
        }

        // Use reflection to map MEVA-attributed properties
        var type = typeof(TRecord);
        var props = type.GetProperties();

        string? id = null;
        string? text = null;
        float[]? vector = null;
        Dictionary<string, object>? metadata = null;

        foreach (var prop in props)
        {
            var attrs = prop.GetCustomAttributes(false);

            foreach (var attr in attrs)
            {
                if (attr is VectorStoreKeyAttribute)
                {
                    id = prop.GetValue(record)?.ToString();
                }
                else if (attr is VectorStoreVectorAttribute)
                {
                    var val = prop.GetValue(record);
                    vector = val switch
                    {
                        float[] arr => arr,
                        ReadOnlyMemory<float> rom => rom.ToArray(),
                        _ => null,
                    };
                }
                else if (attr is VectorStoreDataAttribute)
                {
                    var propName = prop.Name;
                    var val = prop.GetValue(record);

                    if (string.Equals(propName, "Text", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propName, "Content", StringComparison.OrdinalIgnoreCase))
                    {
                        text = val?.ToString();
                    }
                    else if (val != null)
                    {
                        metadata ??= new Dictionary<string, object>();
                        metadata[propName] = val;
                    }
                }
            }
        }

        return new VectorRecord
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Text = text,
            Vector = vector ?? [],
            Metadata = metadata,
        };
    }

    /// <summary>
    /// Maps a <see cref="VectorRecord"/> from storage back to <typeparamref name="TRecord"/>.
    /// </summary>
    protected virtual TRecord? MapFromVectorRecord(VectorRecord record)
    {
        if (typeof(TRecord) == typeof(VectorRecord))
        {
            return (TRecord)(object)record;
        }

        // Use reflection to set MEVA-attributed properties
        var instance = Activator.CreateInstance<TRecord>();
        var type = typeof(TRecord);
        var props = type.GetProperties();

        foreach (var prop in props)
        {
            if (!prop.CanWrite) continue;

            var attrs = prop.GetCustomAttributes(false);
            foreach (var attr in attrs)
            {
                if (attr is VectorStoreKeyAttribute)
                {
                    prop.SetValue(instance, record.Id);
                }
                else if (attr is VectorStoreVectorAttribute)
                {
                    if (prop.PropertyType == typeof(float[]))
                        prop.SetValue(instance, record.Vector);
                    else if (prop.PropertyType == typeof(ReadOnlyMemory<float>))
                        prop.SetValue(instance, new ReadOnlyMemory<float>(record.Vector));
                }
                else if (attr is VectorStoreDataAttribute)
                {
                    if (string.Equals(prop.Name, "Text", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(prop.Name, "Content", StringComparison.OrdinalIgnoreCase))
                    {
                        prop.SetValue(instance, record.Text);
                    }
                    else if (record.Metadata != null && record.Metadata.TryGetValue(prop.Name, out var val))
                    {
                        try
                        {
                            prop.SetValue(instance, Convert.ChangeType(val, prop.PropertyType));
                        }
                        catch
                        {
                            // Skip unmappable properties
                        }
                    }
                }
            }
        }

        return instance;
    }
}
