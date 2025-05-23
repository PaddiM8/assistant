using Assistant.Workers;

namespace Assistant.Messaging;

public class DiscordMessagingService(IConfiguration configuration) : IMessagingService
{
    private readonly ulong _defaultChannelId = configuration.GetSection("Discord").GetValue<ulong>("DefaultChannelId");

    public async Task SendMessageAsync(string message, bool includeInLlmContext)
    {
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
