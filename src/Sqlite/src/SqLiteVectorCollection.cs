using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LangChain.Databases.Sqlite;

/// <summary>
/// 
/// </summary>
public sealed class SqLiteVectorCollection : VectorCollection, IVectorCollection
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="name"></param>
    /// <param name="id"></param>
    public SqLiteVectorCollection(
        SqliteConnection connection,
        string name = DefaultName,
        string? id = null) : base(name, id)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    private static string SerializeDocument(Vector document)
    {
        return JsonSerializer.Serialize(document, SourceGenerationContext.Default.Vector);
    }

    private static string SerializeVector(float[] vector)
    {
        return JsonSerializer.Serialize(vector, SourceGenerationContext.Default.SingleArray);
    }

    private async Task InsertDocument(string id, float[] vector, Vector document)
    {
        using (var insertCommand = _connection.CreateCommand())
        {
            string query = $"INSERT INTO {Name} (id, vector, document) VALUES (@id, @vector, @document)";
            insertCommand.CommandText = query;
            insertCommand.Parameters.AddWithValue("@id", id);
            insertCommand.Parameters.AddWithValue("@vector", SerializeVector(vector));
            insertCommand.Parameters.AddWithValue("@document", SerializeDocument(document));
            await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private async Task DeleteDocument(string id)
    {
        using (var deleteCommand = _connection.CreateCommand())
        {
            string query = $"DELETE FROM {Name} WHERE id=@id";
            deleteCommand.CommandText = query;
            deleteCommand.Parameters.AddWithValue("@id", id);
            await deleteCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private async Task<List<(Vector, float)>> SearchByVector(float[] vector, int k)
    {
        using (var searchCommand = _connection.CreateCommand())
        {
            string query = $"SELECT id, vector, document, distance(vector, @vector) d FROM {Name} ORDER BY d LIMIT @k";
            searchCommand.CommandText = query;
            searchCommand.Parameters.AddWithValue("@vector", SerializeVector(vector));
            searchCommand.Parameters.AddWithValue("@k", k);
            var res = new List<(Vector, float)>();

            using (var reader = await searchCommand.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var id = reader.GetString(0);
                    var vec = await reader.GetFieldValueAsync<string>(1).ConfigureAwait(false);
                    var doc = await reader.GetFieldValueAsync<string>(2).ConfigureAwait(false);
                    var docDeserialized = JsonSerializer.Deserialize(doc, SourceGenerationContext.Default.Vector) ?? new Vector
                    {
                        Text = string.Empty,
                    };
                    var distance = reader.GetFloat(3);
                    res.Add((docDeserialized, distance));
                }

                return res;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> AddAsync(
        IReadOnlyCollection<Vector> items,
        CancellationToken cancellationToken = default)
    {
        items = items ?? throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            if (item.Embedding is null)
            {
                throw new ArgumentException("Embedding is required", nameof(items));
            }

            await InsertDocument(item.Id, item.Embedding, new Vector
            {
                Text = item.Text,
                Metadata = item.Metadata,
            }).ConfigureAwait(false);
        }

        return items.Select(i => i.Id).ToArray();
    }

    /// <inheritdoc />
    public async Task<Vector?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using (var command = _connection.CreateCommand())
        {
            var query = $"SELECT vector, document FROM {Name} WHERE id=@id";
            command.CommandText = query;
            command.Parameters.AddWithValue("@id", id);

            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                var vec = await reader.GetFieldValueAsync<string>(0, cancellationToken).ConfigureAwait(false);
                var doc = await reader.GetFieldValueAsync<string>(1, cancellationToken).ConfigureAwait(false);
                var docDeserialized = JsonSerializer.Deserialize(doc, SourceGenerationContext.Default.Vector) ?? new Vector
                {
                    Text = string.Empty,
                };

                return new Vector
                {
                    Id = id,
                    Text = docDeserialized.Text,
                    Metadata = docDeserialized.Metadata,
                    Embedding = JsonSerializer.Deserialize(vec, SourceGenerationContext.Default.SingleArray),
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        using (var command = _connection.CreateCommand())
        {
            var query = $"SELECT COUNT(*) FROM {Name}";
            command.CommandText = query;
            var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return count == null || Convert.ToInt32(count, CultureInfo.InvariantCulture) == 0;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ids = ids ?? throw new ArgumentNullException(nameof(ids));

        foreach (var id in ids)
            await DeleteDocument(id).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<VectorSearchResponse> SearchAsync(
        VectorSearchRequest request,
        VectorSearchSettings? settings = default,
        CancellationToken cancellationToken = default)
    {
        request = request ?? throw new ArgumentNullException(nameof(request));
        settings ??= new VectorSearchSettings();

        var documents = await SearchByVector(
            request.Embeddings.First(),
            settings.NumberOfResults).ConfigureAwait(false);

        return new VectorSearchResponse
        {
            Items = documents.Select(d => new Vector
            {
                Text = d.Item1.Text,
                Metadata = d.Item1.Metadata,
                Distance = d.Item2,
            }).ToArray(),
        };
    }

    /// <inheritdoc />
    public async Task<List<Vector>> SearchByMetadata(
    Dictionary<string, object> filters,
    CancellationToken cancellationToken = default)
    {
        filters = filters ?? throw new ArgumentNullException(nameof(filters));

        using (var command = _connection.CreateCommand())
        {
            var query = $"SELECT id, vector, document FROM {Name}";

            var whereClauses = new List<string>();
            int paramIndex = 0;

            foreach (var filter in filters)
            {
                var paramName = "@param" + paramIndex++;
                whereClauses.Add($"json_extract(document, '$.Metadata.{filter.Key}') = {paramName}");
                command.Parameters.AddWithValue(paramName, filter.Value);
            }
            query += " WHERE " + string.Join(" AND ", whereClauses);

            command.CommandText = query;
            var res = new List<Vector>();

            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = await reader.GetFieldValueAsync<string>(0, cancellationToken).ConfigureAwait(false);
                    var vec = await reader.GetFieldValueAsync<string>(1, cancellationToken).ConfigureAwait(false);
                    var doc = await reader.GetFieldValueAsync<string>(2, cancellationToken).ConfigureAwait(false);
                    var docDeserialized = JsonSerializer.Deserialize(doc, SourceGenerationContext.Default.Vector) ?? new Vector
                    {
                        Text = string.Empty,
                    };

                    var vector = new Vector
                    {
                        Id = id,
                        Text = docDeserialized.Text,
                        Metadata = docDeserialized.Metadata,
                        Embedding = JsonSerializer.Deserialize(vec, SourceGenerationContext.Default.SingleArray),
                    };

                    res.Add(vector);
                }

                return res;
            }
        }
    }
}