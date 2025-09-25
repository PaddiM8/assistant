using System.Text.Json;
using System.Text.Json.Serialization;
using Assistant.Database;
using Assistant.Llm;
using Assistant.Messaging;
using Assistant.Services;
using Assistant.Services.Planera;
using Assistant.Workers;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "ASSISTANT_");

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
builder.Services.AddTransient<AssistantLlmClient>();
builder.Services.AddTransient<LanguageTutorLlmClient>();
builder.Services.AddTransient<IEmbeddingClient, OpenAiEmbeddingClient>();
builder.Services.AddTransient<ToolService>();

// General services
builder.Services.AddTransient<TimeService>();
builder.Services.AddTransient<EmbeddingService>();
builder.Services.AddTransient<ReminderService>();
builder.Services.AddTransient<SelfPromptService>();
builder.Services.AddTransient<WeatherService>();
builder.Services.AddTransient<PlaneraService>();
builder.Services.AddTransient<HomeAssistantService>();
builder.Services.AddTransient<IMessagingService, DiscordMessagingService>();

var host = builder.Build();

// Apply migrations
using (var scope = host.Services.CreateScope())
{
    var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    applicationContext.Database.Migrate();
}

host.Run();
