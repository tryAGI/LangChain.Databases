using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.IntegrationTests;

public sealed class DatabaseTestEnvironment : IAsyncDisposable
{
    public required VectorStore VectorStore { get; set; }
    public int Port { get; set; }
    public string CollectionName { get; set; } = "test" + Guid.NewGuid().ToString("N");
    public int Dimensions { get; set; } = 1536;
    public IContainer? Container { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
        if (VectorStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
