using LangChain.Databases.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Redis;
using StackExchange.Redis;

namespace LangChain.Databases.Redis;

/// <summary>
/// Redis vector database using the new Vector Store API.
/// </summary>
public class RedisVectorDatabase(IDatabase database)
    : SemanticKernelVectorStoreDatabase(new RedisVectorStore(database));
