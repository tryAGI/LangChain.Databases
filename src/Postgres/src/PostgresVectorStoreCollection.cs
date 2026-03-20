using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.Postgres;

/// <summary>
/// A MEVA-compatible vector store collection backed by PostgreSQL + pgvector.
/// <para>
/// <typeparamref name="TRecord"/> must have properties decorated with
/// <see cref="VectorStoreKeyAttribute"/>, <see cref="VectorStoreDataAttribute"/>,
/// and <see cref="VectorStoreVectorAttribute"/>.
/// </para>
/// </summary>
[RequiresDynamicCode("Requires dynamic code.")]
[RequiresUnreferencedCode("Requires unreferenced code.")]
public class PostgresVectorStoreCollection<TRecord> :
    VectorStoreCollection<string, TRecord>
    where TRecord : class
{
    private readonly PostgresDbClient _client;
    private readonly string _name;
    private readonly VectorStoreCollectionDefinition? _definition;
    private readonly PostgresRecordMapper<TRecord> _mapper;
    private readonly string? _databaseName;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public PostgresVectorStoreCollection(
        PostgresDbClient client,
        string name,
        VectorStoreCollectionDefinition? definition = null,
        string? databaseName = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _definition = definition;
        _mapper = new PostgresRecordMapper<TRecord>(definition);
        _databaseName = databaseName;
    }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("collection_exists");
        try
        {
            return await _client.IsTableExistsAsync(_name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("create_collection");
        try
        {
            if (!await _client.IsTableExistsAsync(_name, cancellationToken).ConfigureAwait(false))
            {
                var dimensions = _mapper.GetVectorDimensions();
                await _client.CreateEmbeddingTableAsync(_name, dimensions, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("delete_collection");
        try
        {
            await _client.DropTableAsync(_name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
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
            var includeVectors = options?.IncludeVectors ?? false;
            var record = await _client.GetRecordByIdAsync(
                _name, key, withEmbeddings: includeVectors, cancellationToken).ConfigureAwait(false);

            return record != null ? _mapper.FromRecord(record) : default;
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("delete");
        try
        {
            await _client.DeleteBatchAsync(_name, [key], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
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
            var keyList = keys as IReadOnlyList<string> ?? keys.ToList();
            if (keyList.Count > 0)
            {
                await _client.DeleteBatchAsync(_name, keyList, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
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
            var (id, content, metadata, embedding) = _mapper.ToRecord(record);

            await _client.UpsertAsync(
                tableName: _name,
                id: id,
                content: content,
                metadata: metadata,
                embedding: embedding,
                timestamp: DateTime.UtcNow,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
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
            var batch = records
                .Select(r => _mapper.ToRecord(r))
                .Select(r => (r.Id, r.Content, (IReadOnlyDictionary<string, object>?)r.Metadata, r.Embedding, (DateTime?)DateTime.UtcNow))
                .ToList();

            await _client.UpsertBatchAsync(_name, batch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
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
        throw new NotSupportedException("Expression-based filtering is not supported. Use SearchAsync with vector search instead.");
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("search");

        IEnumerable<(EmbeddingTableRecord, float)> results;
        ReadOnlyMemory<float> embedding;

        if (searchValue is ReadOnlyMemory<float> floatMemory)
        {
            embedding = floatMemory;
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

        try
        {
            results = await _client.GetWithDistanceAsync(
                tableName: _name,
                embedding: embedding.ToArray(),
                strategy: DistanceStrategy.Cosine,
                limit: top,
                minRelevanceScore: 0,
                withEmbeddings: options?.IncludeVectors ?? false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PostgresTelemetry.SetError(activity, ex);
            throw;
        }

        foreach (var (record, score) in results)
        {
            var mapped = _mapper.FromRecord(record);
            if (mapped != null)
            {
                yield return new VectorSearchResult<TRecord>(mapped, score);
            }
        }
    }

    private Activity? StartActivity(string operationName)
    {
        var activity = PostgresTelemetry.ActivitySource.StartActivity(
            $"{PostgresTelemetry.SystemName}.{operationName}");
        if (activity is not null)
        {
            activity.SetTag("db.system.name", PostgresTelemetry.SystemName);
            activity.SetTag("db.collection.name", _name);
            if (_databaseName is not null)
            {
                activity.SetTag("db.namespace", _databaseName);
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
                VectorStoreSystemName = "postgresql",
                VectorStoreName = _databaseName,
                CollectionName = _name,
            };
        }

        return null;
    }
}
