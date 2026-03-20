using LangChain.Databases.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Pinecone;
using PineconeClient = Pinecone.PineconeClient;

namespace LangChain.Databases.Pinecone;

/// <summary>
/// Pinecone vector database using the new Vector Store API.
/// </summary>
public class PineconeVectorDatabase(PineconeClient client)
    : SemanticKernelVectorStoreDatabase(new PineconeVectorStore(client));
