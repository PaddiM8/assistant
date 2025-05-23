namespace Assistant.Workers;

using System.Text;
using Assistant.Llm;
using Assistant.Utils;
using DSharpPlus;
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

        var fallbackHistoryMessages = await args.Message.Channel.GetMessagesBeforeAsync(args.Message.Id, limit: 4);
        var fallbackHistory = fallbackHistoryMessages.Select(x => x.Content);

        var llvmResponse = await _llmClient!.SendAsync(args.Message.Content, fallbackHistory);
        var responseBuilder = new StringBuilder();
        responseBuilder.AppendLine(llvmResponse.Message.Trim());
        if (llvmResponse.FunctionCallResponses.Count > 0)
        {
            foreach (var callResponse in llvmResponse.FunctionCallResponses)
            {
                responseBuilder.AppendLine("```");
                responseBuilder.AppendLine(callResponse);
                responseBuilder.AppendLine("```");
            }
        }

        await args.Message.RespondAsync(responseBuilder.ToString().Truncate(1900, $"... (message was {responseBuilder.Length} characters)"));
    }
}
