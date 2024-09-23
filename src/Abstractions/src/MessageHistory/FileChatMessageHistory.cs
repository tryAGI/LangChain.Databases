using LangChain.Providers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangChain.Memory;

/// <summary>
/// Chat message history that stores history in a local file.
/// </summary>
public class FileChatMessageHistory : BaseChatMessageHistory
{
    private string MessagesFilePath { get; }

    private List<Message> _messages = new List<Message>();

    /// <inheritdoc/>
    public override IReadOnlyList<Message> Messages => _messages;

    /// <summary>
    /// Initializes new history instance with provided file path
    /// </summary>
    /// <param name="messagesFilePath">path of the local file to store the messages</param>
    /// <exception cref="ArgumentNullException"></exception>
    private FileChatMessageHistory(string messagesFilePath)
    {
        MessagesFilePath = messagesFilePath ?? throw new ArgumentNullException(nameof(messagesFilePath));
    }

    /// <summary>
    /// Create new history instance with provided file path
    /// </summary>
    /// <param name="path">path of the local file to store the messages</param>
    /// <param name="cancellationToken"></param>
    public static async Task<FileChatMessageHistory> CreateAsync(string path, CancellationToken cancellationToken = default)
    {
        FileChatMessageHistory chatHistory = new FileChatMessageHistory(path);
        await chatHistory.LoadMessages().ConfigureAwait(false);

        return chatHistory;
    }

    /// <inheritdoc/>
    public override Task AddMessage(Message message)
    {
        _messages.Add(message);
        SaveMessages();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task Clear()
    {
        _messages.Clear();
        SaveMessages();

        return Task.CompletedTask;
    }

    private void SaveMessages()
    {
        var json = JsonSerializer.Serialize(_messages, SourceGenerationContext.Default.ListMessage);

        File.WriteAllText(MessagesFilePath, json);
    }

    private async Task LoadMessages()
    {
        if (File.Exists(MessagesFilePath))
        {
            var json = await File2.ReadAllTextAsync(MessagesFilePath).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _messages = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ListMessage) ?? new List<Message>();
            }
        }
    }
}

[JsonSerializable(typeof(List<Message>))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext;