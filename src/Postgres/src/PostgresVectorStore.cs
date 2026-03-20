using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using Npgsql;

namespace LangChain.Databases.Postgres;

/// <summary>
/// Postgres vector store using pgvector extension.
/// Implements MEVA <see cref="VectorStore"/> for use with Microsoft.Extensions.VectorData consumers.
/// <remarks>
/// Requires: CREATE EXTENSION IF NOT EXISTS vector
/// </remarks>
/// </summary>
[RequiresDynamicCode("Requires dynamic code.")]
[RequiresUnreferencedCode("Requires unreferenced code.")]
public class PostgresVectorStore : VectorStore
{
    private readonly PostgresDbClient _client;
    private readonly string? _databaseName;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresVectorStore"/> class.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="schema">Database schema name (default: "public").</param>
    /// <param name="omitExtensionCreation">Skip creating the pgvector extension.</param>
    public PostgresVectorStore(
        string connectionString,
        string schema = "public",
        bool omitExtensionCreation = false)
    {
        _client = new PostgresDbClient(connectionString, schema, omitExtensionCreation);
        _databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
    }

    /// <summary>
    /// Gets the underlying <see cref="PostgresDbClient"/> for advanced operations.
    /// </summary>
    public PostgresDbClient Client => _client;

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException("PostgresVectorStore only supports string keys.");
        }

        return (VectorStoreCollection<TKey, TRecord>)(object)new PostgresVectorStoreCollection<TRecord>(
            _client,
            name,
            definition,
            _databaseName);
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition)
    {
        throw new NotSupportedException("Dynamic collections are not supported by PostgresVectorStore. Use GetCollection<string, TRecord> instead.");
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("list_collections");
        var tables = await _client.ListTablesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var table in tables)
        {
            yield return table;
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("collection_exists");
        activity?.SetTag("db.collection.name", name);
        return await _client.IsTableExistsAsync(name, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity("delete_collection");
        activity?.SetTag("db.collection.name", name);
        await _client.DropTableAsync(name, cancellationToken).ConfigureAwait(false);
    }

    private Activity? StartActivity(string operationName)
    {
        var activity = PostgresTelemetry.ActivitySource.StartActivity(
            $"{PostgresTelemetry.SystemName}.{operationName}");
        if (activity is not null)
        {
            activity.SetTag("db.system.name", PostgresTelemetry.SystemName);
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

        if (serviceType == typeof(VectorStoreMetadata))
        {
            return new VectorStoreMetadata
            {
                VectorStoreSystemName = "postgresql",
                VectorStoreName = _databaseName,
            };
        }

        return null;
    }
}
