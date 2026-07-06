using Microsoft.Extensions.AI;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace LangChain.Memory;

/// <summary>
/// Chat message history that stores history in a local file.
/// </summary>
public class FileChatMessageHistory : BaseChatMessageHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private string MessagesFilePath { get; }

    private List<ChatMessage> _messages = new List<ChatMessage>();

    /// <inheritdoc/>
    public override IReadOnlyList<ChatMessage> Messages => _messages;

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
    [RequiresDynamicCode()]
    [RequiresUnreferencedCode()]
    public static async Task<FileChatMessageHistory> CreateAsync(string path, CancellationToken cancellationToken = default)
    {
        FileChatMessageHistory chatHistory = new FileChatMessageHistory(path);
        await chatHistory.LoadMessages().ConfigureAwait(false);

        return chatHistory;
    }

    /// <inheritdoc/>
    [RequiresDynamicCode()]
    [RequiresUnreferencedCode()]
    public override Task AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        SaveMessages();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    [RequiresDynamicCode()]
    [RequiresUnreferencedCode()]
    public override Task Clear()
    {
        _messages.Clear();
        SaveMessages();

        return Task.CompletedTask;
    }

    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
    private void SaveMessages()
    {
        var json = JsonSerializer.Serialize(_messages, JsonOptions);

        File.WriteAllText(MessagesFilePath, json);
    }

    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    [RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    private async Task LoadMessages()
    {
        if (File.Exists(MessagesFilePath))
        {
            var json = await File2.ReadAllTextAsync(MessagesFilePath).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _messages = JsonSerializer.Deserialize<List<ChatMessage>>(json, JsonOptions) ?? new List<ChatMessage>();
            }
        }
    }
}
