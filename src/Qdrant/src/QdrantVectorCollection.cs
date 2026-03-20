using LangChain.Databases.SemanticKernel;
using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.Qdrant;

/// <summary>
/// Qdrant vector collection using the new Vector Store API.
/// </summary>
public class QdrantVectorCollection(
    VectorStoreCollection<object, Dictionary<string, object?>> collection,
    string name = VectorCollection.DefaultName,
    string? id = null)
    : SemanticKernelVectorStoreCollection(collection, name, id);
