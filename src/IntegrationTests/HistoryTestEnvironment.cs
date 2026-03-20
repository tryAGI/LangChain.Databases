using DotNet.Testcontainers.Containers;
using LangChain.Memory;

namespace LangChain.Databases.IntegrationTests;

public sealed class HistoryTestEnvironment : IAsyncDisposable
{
    public required BaseChatMessageHistory History { get; set; }
    public int Port { get; set; }
    public IContainer? Container { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
    }
}
