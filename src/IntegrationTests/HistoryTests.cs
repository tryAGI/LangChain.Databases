using Microsoft.Extensions.AI;

namespace LangChain.Databases.IntegrationTests;

public partial class HistoryTests
{
    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.File)]
    [TestCase(SupportedDatabase.Mongo)]
    [TestCase(SupportedDatabase.Redis)]
    [Obsolete]
    public async Task FillAndClear_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var history = environment.History;

        history.Messages.Should().BeEmpty();

        var humanMessage = new ChatMessage(ChatRole.User, "Hi, AI");
        await history.AddMessage(humanMessage);
        var aiMessage = new ChatMessage(ChatRole.Assistant, "Hi, human");
        await history.AddMessage(aiMessage);

        var actual = history.Messages;

        actual.Should().NotBeEmpty();
        actual.Should().HaveCount(2);
        actual[0].Role.Should().Be(humanMessage.Role);
        actual[0].Text.Should().Be(humanMessage.Text);
        actual[1].Role.Should().Be(aiMessage.Role);
        actual[1].Text.Should().Be(aiMessage.Text);

        await history.Clear();

        history.Messages.Should().BeEmpty();
    }
}
