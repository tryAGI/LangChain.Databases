using LangChain.Databases.Mongo.Client;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LangChain.Databases.Mongo;

public class MongoVectorCollection(
    IMongoContext mongoContext,
    string indexName,
    string name = VectorCollection.DefaultName,
    string? id = null)
: VectorCollection(name, id), IVectorCollection
{
    private readonly IMongoCollection<Vector> _mongoCollection = mongoContext.GetCollection<Vector>(name);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> AddAsync(IReadOnlyCollection<Vector> items, CancellationToken cancellationToken = default)
    {
        await _mongoCollection.InsertManyAsync(items, cancellationToken: cancellationToken).ConfigureAwait(false);
        return items.Select(i => i.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Vector>.Filter.In(i => i.Id, ids);
        var result = await _mongoCollection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.IsAcknowledged;
    }

    /// <inheritdoc />
    public async Task<Vector?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Vector>.Filter.Eq(i => i.Id, id);
        var result = await _mongoCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.FirstOrDefault(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        return await _mongoCollection.EstimatedDocumentCountAsync(cancellationToken: cancellationToken).ConfigureAwait(false) == 0;
    }

    /// <inheritdoc />
    public async Task<VectorSearchResponse> SearchAsync(VectorSearchRequest request, VectorSearchSettings? settings = null, CancellationToken cancellationToken = default)
    {
        request = request ?? throw new ArgumentNullException(nameof(request));
        settings ??= new VectorSearchSettings();

        var options = new VectorSearchOptions<Vector>()
        {
            IndexName = indexName,
            NumberOfCandidates = settings.NumberOfResults * 10
        };
        var projectionDefinition = Builders<Vector>.Projection
                         .Exclude(a => a.Distance)
                         .Meta("score", "vectorSearchScore");

        var results = await _mongoCollection.Aggregate()
                   .VectorSearch(nameof(Vector.Embedding), request.Embeddings.First(), settings.NumberOfResults, options)
                   .Project(projectionDefinition)
                   .ToListAsync(cancellationToken)
                   .ConfigureAwait(false);


        return new VectorSearchResponse
        {
            Items = results.Select(result =>
            {
                var output = BsonSerializer.Deserialize<Vector>(result);
                output.Distance = (float)result["score"].ToDouble();
                return output;
            })
            .ToArray(),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Vector>> SearchByMetadata(Dictionary<string, object> filters, CancellationToken cancellationToken = default)
    {
        filters = filters ?? throw new ArgumentNullException(nameof(filters));

        var builder = Builders<Vector>.Filter;
        var filterDefinitions = new List<FilterDefinition<Vector>>();

        foreach (var kvp in filters)
        {
            if (kvp.Value == null)
            {
                throw new ArgumentException($"Metadata value for key '{kvp.Key}' cannot be null", nameof(filters));
            }

            // Assuming your Vector class has a Metadata field of type Dictionary<string, object>
            var filter = builder.Eq($"Metadata.{kvp.Key}", kvp.Value);
            filterDefinitions.Add(filter);
        }

        var combinedFilter = builder.And(filterDefinitions);

        var results = await _mongoCollection
            .Find(combinedFilter)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return results;
    }
}