using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.SemanticKernel;

/// <summary>
/// Wraps a <see cref="VectorStore"/> to implement <see cref="IVectorDatabase"/>.
/// Uses the dynamic collection API with <see cref="VectorStoreCollectionDefinition"/> for runtime-configurable dimensions.
/// </summary>
public class SemanticKernelVectorStoreDatabase(VectorStore vectorStore) : IVectorDatabase, IDisposable
{
    public async Task CreateCollectionAsync(string collectionName, int dimensions, CancellationToken cancellationToken = default)
    {
        var collection = GetDynamicCollection(collectionName, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await vectorStore.EnsureCollectionDeletedAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IVectorCollection> GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var exists = await vectorStore.CollectionExistsAsync(collectionName, cancellationToken).ConfigureAwait(false);
        if (!exists)
            throw new InvalidOperationException("Collection not found");

        // Use dimensions=1 as placeholder since the collection already exists
        return new SemanticKernelVectorStoreCollection(GetDynamicCollection(collectionName, 1), collectionName);
    }

    public async Task<IVectorCollection> GetOrCreateCollectionAsync(string collectionName, int dimensions, CancellationToken cancellationToken = default)
    {
        var collection = GetDynamicCollection(collectionName, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);

        return new SemanticKernelVectorStoreCollection(collection, collectionName);
    }

    public async Task<bool> IsCollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return await vectorStore.CollectionExistsAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        return await vectorStore.ListCollectionNamesAsync(cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, int dimensions)
    {
        return vectorStore.GetDynamicCollection(name, CreateDefinition(dimensions));
    }

    internal static VectorStoreCollectionDefinition CreateDefinition(int dimensions)
    {
        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty("Id", typeof(string)),
                new VectorStoreDataProperty("Text", typeof(string)),
                new VectorStoreDataProperty("Metadata", typeof(string)),
                new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), dimensions)
                {
                    DistanceFunction = DistanceFunction.CosineSimilarity,
                },
            ],
        };
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            vectorStore.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
