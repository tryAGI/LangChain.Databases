using Microsoft.Extensions.AI;

namespace LangChain.Memory;

/// <summary>
/// In memory implementation of chat message history.
///
/// Stores messages in an in memory list.
/// </summary>
public class ChatMessageHistory : BaseChatMessageHistory
{
    private readonly List<ChatMessage> _messages = new List<ChatMessage>();

    /// <summary>
    /// Used to inspect and filter messages on their way to the history store
    /// NOTE: This is not a feature of python langchain
    /// </summary>
    public Predicate<ChatMessage> IsMessageAccepted { get; set; } = (x => true);

    /// <inheritdoc/>
    public override IReadOnlyList<ChatMessage> Messages => _messages;

    /// <inheritdoc/>
    public override Task AddMessage(ChatMessage message)
    {
        if (IsMessageAccepted(message))
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task Clear()
    {
        _messages.Clear();
        return Task.CompletedTask;
    }
}
