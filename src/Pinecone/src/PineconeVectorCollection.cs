using LangChain.Databases.SemanticKernel;
using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.Pinecone;

/// <summary>
/// Pinecone vector collection using the new Vector Store API.
/// </summary>
public class PineconeVectorCollection(
    VectorStoreCollection<object, Dictionary<string, object?>> collection,
    string name = VectorCollection.DefaultName,
    string? id = null)
    : SemanticKernelVectorStoreCollection(collection, name, id);
