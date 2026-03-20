using LangChain.Databases.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

namespace LangChain.Databases.Qdrant;

/// <summary>
/// Qdrant vector database using the new Vector Store API.
/// </summary>
public class QdrantVectorDatabase(QdrantClient client)
    : SemanticKernelVectorStoreDatabase(new QdrantVectorStore(client, ownsClient: true));
