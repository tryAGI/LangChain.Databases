using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.IntegrationTests;

/// <summary>
/// A simple MEVA record type for integration tests.
/// </summary>
public class TestVectorRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string? Color { get; set; }

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
