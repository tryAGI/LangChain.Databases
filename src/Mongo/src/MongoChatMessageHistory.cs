using LangChain.Databases.Mongo.Client;
using LangChain.Memory;
using LangChain.Databases.Mongo.Model;
using System.Text.Json;
using Microsoft.Extensions.AI;
using System.Diagnostics.CodeAnalysis;

namespace LangChain.Databases.Mongo;

public class MongoChatMessageHistory(
    string sessionId,
    IMongoDbClient mongoRepository)
    : BaseChatMessageHistory, IMongoChatMessageHistory
{
    protected IMongoDbClient MongoRepository { get; } = mongoRepository;

    public override async Task Clear()
    {
        await MongoRepository
            .BatchDeactivate<LangChainAiSessionHistory>(i => i.SessionId == sessionId).ConfigureAwait(false);
    }

    [RequiresUnreferencedCode("Uses System.Text.Json serialization.")]
    [RequiresDynamicCode("Uses System.Text.Json serialization.")]
    public override async Task AddMessage(ChatMessage message)
    {
        await MongoRepository.InsertAsync(new LangChainAiSessionHistory
        {
            SessionId = sessionId,
            Message = JsonSerializer.Serialize(message),
        }).ConfigureAwait(false);
    }

    public override IReadOnlyList<ChatMessage> Messages
    {
        get
        {
            return MongoRepository
                .GetSync<LangChainAiSessionHistory, string>(s =>
                        s.SessionId == sessionId &&
                        s.IsActive,
                        m => m.Message)
                .Select(static x => JsonSerializer.Deserialize<ChatMessage>(x.ToString())!)
                .ToList();
        }
    }
}
