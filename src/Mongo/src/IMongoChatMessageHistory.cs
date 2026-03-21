using Microsoft.Extensions.AI;

namespace LangChain.Databases.Mongo;

public interface IMongoChatMessageHistory
{
    IReadOnlyList<ChatMessage> Messages { get; }

    Task AddMessage(ChatMessage message);
    Task Clear();
}
