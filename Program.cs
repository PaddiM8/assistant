using System.Text.Json;
using System.Text.Json.Serialization;
using Assistant.Database;
using Assistant.Llm;
using Assistant.Messaging;
using Assistant.Services;
using Assistant.Workers;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Secrets.json",
    optional: true,
    reloadOnChange: false
);

// Contexts
builder.Services.AddDbContextPool<ApplicationDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("ApplicationDbContext"),
        o => o.UseVector()
    )
);

// External
builder.Services.AddHttpClient();

// Workers
builder.Services.AddHostedService<SchedulingWorker>();
builder.Services.AddHostedService<DiscordWorker>();

// LLM tools
builder.Services.AddTransient<ILlmClient, OpenAiLlmClient>();
builder.Services.AddTransient<IEmbeddingClient, OpenAiEmbeddingClient>();
builder.Services.AddTransient<ToolService>();

// General services
builder.Services.AddTransient<EmbeddingService>();
builder.Services.AddTransient<ReminderService>();
builder.Services.AddTransient<SelfPromptService>();
builder.Services.AddTransient<WeatherService>();
builder.Services.AddTransient<IMessagingService, DiscordMessagingService>();

// Configuration
var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
};
jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

builder.Services.AddSingleton(jsonSerializerOptions);

var host = builder.Build();
host.Run();
