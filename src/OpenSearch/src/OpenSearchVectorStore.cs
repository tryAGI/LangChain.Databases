using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using OpenSearch.Client;

namespace LangChain.Databases.OpenSearch;

/// <summary>
/// MEVA-compatible vector store backed by OpenSearch with k-NN plugin.
/// </summary>
public class OpenSearchVectorStore : VectorStore
{
    private readonly IOpenSearchClient _client;
    private readonly string? _vectorStoreName;

    /// <summary>
    /// Initializes a new instance using connection options.
    /// </summary>
    public OpenSearchVectorStore(OpenSearchVectorDatabaseOptions? options = null)
    {
        options ??= new OpenSearchVectorDatabaseOptions();

#pragma warning disable CA2000
        var settings = new ConnectionSettings(options.ConnectionUri)
#pragma warning restore CA2000
            .BasicAuthentication(options.Username, options.Password);

        _client = new OpenSearchClient(settings);
        _vectorStoreName = options.ConnectionUri?.Host;
    }

    /// <summary>
    /// Initializes a new instance using an existing OpenSearch client.
    /// </summary>
    public OpenSearchVectorStore(IOpenSearchClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Gets the underlying OpenSearch client.
    /// </summary>
    public IOpenSearchClient Client => _client;

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("OpenSearchVectorStore only supports string keys.");
        }

        return (VectorStoreCollection<TKey, TRecord>)(object)new OpenSearchVectorStoreCollection<TRecord>(
            _client,
            name,
            definition,
            _vectorStoreName);
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition)
    {
        throw new NotSupportedException("Dynamic collections are not supported by OpenSearchVectorStore.");
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("list_collections");
        var response = await _client.Indices.GetAsync(Indices.AllIndices, ct: cancellationToken).ConfigureAwait(false);

        foreach (var index in response.Indices.Keys)
        {
            var name = index.Name;
            if (!name.StartsWith(".", StringComparison.Ordinal))
            {
                yield return name;
            }
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("collection_exists");
        activity?.SetTag("db.collection.name", name);
        var response = await _client.Indices.ExistsAsync(name, ct: cancellationToken).ConfigureAwait(false);
        return response.Exists;
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("delete_collection");
        activity?.SetTag("db.collection.name", name);
        var exists = await CollectionExistsAsync(name, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            var response = await _client.Indices.DeleteAsync(
                new DeleteIndexRequest(name), cancellationToken).ConfigureAwait(false);
            if (!response.IsValid)
            {
                throw new InvalidOperationException(
                    $"Failed to delete index '{name}'. {response.DebugInformation}");
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

        if (serviceType == typeof(VectorStoreMetadata))
        {
            return new VectorStoreMetadata
            {
                VectorStoreSystemName = "opensearch",
                VectorStoreName = _vectorStoreName,
            };
        }

        return null;
    }
}
