namespace Assistant.Workers;

using System.Text;
using Assistant.Llm;
using Assistant.Utils;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

public class DiscordWorker(
    ILogger<DiscordWorker> logger,
    IConfiguration configuration,
    ILoggerFactory loggerFacory,
    IServiceProvider serviceProvider
) : IHostedService
{
    private static DiscordClient? _client;
    private static ILlmClient? _llmClient;

    private readonly ILogger<DiscordWorker> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILoggerFactory _loggerFacory = loggerFacory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public static DiscordClient? GetClient()
    {
        return _client;
    }

    public static ILlmClient? GetLlmClient()
    {
        return _llmClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await StartClientAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client?.Dispose();

        return Task.CompletedTask;
    }

    private async Task StartClientAsync(CancellationToken cancellationToken)
    {
        var clientConfig = new DiscordConfiguration
        {
            Token = _configuration.GetSection("Discord").GetValue<string>("Token"),
            MinimumLogLevel = LogLevel.Warning,
            LoggerFactory = _loggerFacory,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
        };

        _client?.Dispose();
        _client = new DiscordClient(clientConfig);
        _client.MessageCreated += HandleMessageCreatedAsync;

        _llmClient = _serviceProvider.GetRequiredService<ILlmClient>();

        await _client.ConnectAsync();
        await Task.Delay(-1, cancellationToken);
    }

    private async Task HandleMessageCreatedAsync(DiscordClient client, MessageCreateEventArgs args)
    {
        if (args.Author.IsBot)
            return;

        var messagesBeforeId = args.Message.ReferencedMessage == null
            ? args.Message.Id
            : args.Message.ReferencedMessage.Id;
        var fallbackHistoryMessages = await args.Message.Channel.GetMessagesBeforeAsync(messagesBeforeId, limit: 4);
        var fallbackHistory = fallbackHistoryMessages
            .Select(x => x.Content)
            .ToList();

        var content = args.Message.Content;
        if (args.Message.ReferencedMessage != null)
        {
            fallbackHistory.Add(args.Message.ReferencedMessage.Content);
            content = BuildMessageContentWithReply(args.Message);
        }

        var llvmResponse = await _llmClient!.SendAsync(content, args.Message.Author.Id.ToString(), fallbackHistory);
        var responseBuilder = new StringBuilder();
        responseBuilder.AppendLine(llvmResponse.Message.Trim());

        await args.Message.RespondAsync(responseBuilder.ToString().Truncate(1900, $"... (message was {responseBuilder.Length} characters)"));
    }

    private static string BuildMessageContentWithReply(DiscordMessage message)
    {
        var referencedContent = message.ReferencedMessage.Content;
        var quotedUser = message.ReferencedMessage.Author.IsBot ? "Assistant" : "User";
        var builder = new StringBuilder();
        builder.AppendLine($"Quote from {quotedUser}:");
        builder.Append("> ");
        builder.AppendLine(referencedContent.Replace("\n", "\n> "));
        builder.AppendLine();
        builder.AppendLine(message.Content);

        return builder.ToString();
    }
}
