using Assistant.Workers;

namespace Assistant.Messaging;

public class DiscordMessagingService(IConfiguration configuration) : IMessagingService
{
    private readonly ulong _defaultChannelId = configuration.GetSection("Discord").GetValue<ulong>("DefaultChannelId");

    public async Task SendMessageAsync(string message, bool includeInLlmContext)
    {
        await SendMessageAsync(message, MessagePriority.Normal, string.Empty, includeInLlmContext);
    }

    public async Task SendMessageAsync(string message, MessagePriority priority, string userIdentifier, bool includeInLlmContext)
    {
        if (priority == MessagePriority.Ping)
            message = $"<@{userIdentifier}> {message}";

        var discordClient = DiscordWorker.GetClient()
            ?? throw new InvalidOperationException("Cannot send message. Discord worker has not started yet.");
        var channel = await discordClient.GetChannelAsync(_defaultChannelId);
        await discordClient.SendMessageAsync(channel, message);

        if (includeInLlmContext)
        {
            var llmClient = DiscordWorker.GetLlmClient();
            llmClient?.AddAssistantMessageToHistory(message);
        }
    }
}
