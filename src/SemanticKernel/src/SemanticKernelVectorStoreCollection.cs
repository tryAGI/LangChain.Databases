using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.SemanticKernel;

/// <summary>
/// Wraps a dynamic <see cref="VectorStoreCollection{TKey, TRecord}"/> to implement <see cref="IVectorCollection"/>.
/// Maps between LangChain <see cref="Vector"/> and <see cref="Dictionary{TKey, TValue}"/> records.
/// </summary>
public class SemanticKernelVectorStoreCollection(
    VectorStoreCollection<object, Dictionary<string, object?>> collection,
    string name = VectorCollection.DefaultName,
    string? id = null)
    : VectorCollection(name, id), IVectorCollection
{
    public async Task<IReadOnlyCollection<string>> AddAsync(IReadOnlyCollection<Vector> items, CancellationToken cancellationToken = default)
    {
        items = items ?? throw new ArgumentNullException(nameof(items));

        var records = items.Select(item => new Dictionary<string, object?>
        {
            ["Id"] = item.Id,
            ["Text"] = item.Text,
            ["Metadata"] = SerializeMetadata(item.Metadata),
            ["Embedding"] = item.Embedding != null ? new ReadOnlyMemory<float>(item.Embedding) : null,
        }).ToList();

        await collection.UpsertAsync(records, cancellationToken).ConfigureAwait(false);
        return items.Select(x => x.Id).ToList();
    }

    public async Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        await collection.DeleteAsync(ids.Cast<object>(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<Vector?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await collection.GetAsync(
            (object)id,
            new RecordRetrievalOptions { IncludeVectors = true },
            cancellationToken).ConfigureAwait(false);

        return record != null ? MapToVector(record) : null;
    }

    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        return !await collection.CollectionExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VectorSearchResponse> SearchAsync(
        VectorSearchRequest request,
        VectorSearchSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        request = request ?? throw new ArgumentNullException(nameof(request));
        settings ??= new VectorSearchSettings();

        var searchOptions = new VectorSearchOptions<Dictionary<string, object?>>
        {
            IncludeVectors = false,
        };

        if (settings.ScoreThreshold.HasValue)
        {
            searchOptions.ScoreThreshold = settings.ScoreThreshold.Value;
        }

        var results = new List<Vector>();
        await foreach (var result in collection.SearchAsync(
            new ReadOnlyMemory<float>(request.Embeddings.First()),
            top: settings.NumberOfResults,
            options: searchOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var vector = MapToVector(result.Record);
            vector.RelevanceScore = (float)(result.Score ?? 0);
            results.Add(vector);
        }

        return new VectorSearchResponse
        {
            Items = results,
        };
    }

    Task<IReadOnlyList<Vector>> IVectorCollection.SearchByMetadata(
        Dictionary<string, object> filters,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static Vector MapToVector(Dictionary<string, object?> record)
    {
        var text = record.TryGetValue("Text", out var textObj) ? textObj?.ToString() ?? string.Empty : string.Empty;
        var id = record.TryGetValue("Id", out var idObj) ? idObj?.ToString() ?? string.Empty : string.Empty;
        var metadataStr = record.TryGetValue("Metadata", out var metaObj) ? metaObj?.ToString() : null;

        return new Vector
        {
            Id = id,
            Text = text,
            Metadata = DeserializeMetadata(metadataStr),
        };
    }

    private static string? SerializeMetadata(IDictionary<string, object>? metadata)
    {
        if (metadata == null) return null;

        return string.Join("#", metadata.Select(kv => kv.Key + "&" + kv.Value));
    }

    private static Dictionary<string, object>? DeserializeMetadata(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata)) return null;

        return metadata
            .Split('#')
            .Where(part => !string.IsNullOrEmpty(part) && part.Contains('&'))
            .Select(part => part.Split('&'))
            .Where(split => split.Length == 2)
            .ToDictionary(split => split[0], split => (object)split[1]);
    }
}
