using System.Text;
using Assistant.Database;
using Assistant.Llm;
using Assistant.Llm.Schema;
using Assistant.Messaging;
using Assistant.Services;
using Assistant.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Assistant.Workers;

public class SchedulingWorker(IServiceProvider serviceProvider, ILogger<SchedulingWorker> logger) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<SchedulingWorker> _logger = logger;
    private Timer? _timer;
    private IServiceScope? _scope;
    private ApplicationDbContext _applicationContext = null!;
    private EmbeddingService _embeddingService = null!;
    private IMessagingService _messagingService = null!;
    private ILlmClient _llmClient = null!;


    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(
            async _ => await DoPeriodicalWorkAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(20) // TODO: Change to 1 minute
        );

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_timer != null)
            await _timer.DisposeAsync();

        _timer = null;
        _scope?.Dispose();
        _scope = null;
    }

    private void PrepareScope()
    {
        if (_scope != null)
            _scope.Dispose();

        _scope = _serviceProvider.CreateScope();
        _applicationContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _embeddingService = _scope.ServiceProvider.GetRequiredService<EmbeddingService>();
        _messagingService = _scope.ServiceProvider.GetRequiredService<IMessagingService>();
        _llmClient = _scope.ServiceProvider.GetRequiredService<ILlmClient>();
    }

    private async Task DoPeriodicalWorkAsync()
    {
        PrepareScope();

        try
        {
            await ExecuteScheduledEntriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("An error occurred during periodical work in the scheduling worker. Exception: {Exception}", ex.ToString());
        }
    }

    private async Task ExecuteScheduledEntriesAsync()
    {
        var now = DateTime.UtcNow;
        var entriesToTrigger = _applicationContext
            .ScheduleEntries
            .Where(x => x.IsActive)
            .Where(x => x.TriggerAtUtc <= now)
            .ToList();

        var tasks = new Dictionary<Task, ScheduleEntry>();
        foreach (var entry in entriesToTrigger)
        {
            var task = ExecuteScheduleEntry(entry);
            tasks.Add(task, entry);
        }

        try
        {
            await Task.WhenAll(tasks.Keys);
        }
        catch (Exception ex)
        {
            _logger.LogError("One or more schedule tasks failed. Exception: {Exception}", ex.ToString());
        }

        var successfulEntries = tasks
            .Where(x => x.Key.IsCompletedSuccessfully)
            .Select(x => x.Value);
        foreach (var successfulEntry in successfulEntries)
        {
            if (successfulEntry.RecurrenceUnit.HasValue)
            {
                successfulEntry.TriggerAtUtc = ResolveRecurrenceTimeSpan(successfulEntry.TriggerAtUtc, successfulEntry);
            }
            else
            {
                successfulEntry.IsActive = false;
            }
        }

        _applicationContext.ScheduleEntries.UpdateRange(successfulEntries);
        await _applicationContext.SaveChangesAsync();
    }

    private static DateTime ResolveRecurrenceTimeSpan(DateTime dateTime, ScheduleEntry entry)
    {

        var interval = entry.RecurrenceInterval!.Value;

        return entry.RecurrenceUnit!.Value switch
        {
            Frequency.Daily => dateTime.AddDays(interval),
            Frequency.Weekly => dateTime.AddDays(interval * 7),
            Frequency.Monthly => dateTime.AddMonths(interval),
            Frequency.Yearly => dateTime.AddYears(interval),
        };
    }

    private async Task ExecuteScheduleEntry(ScheduleEntry entry)
    {
        if (entry.Kind == ScheduleEntryKind.Reminder)
        {
            await ExecuteReminderAsync(entry);
        }
        else if (entry.Kind == ScheduleEntryKind.SelfPrompt)
        {
            await ExecuteSelfPromptAsync(entry);
        }

        var embedding = await _embeddingService.FindByRelatedItemIdAsync<ScheduleEntry>(entry.Id);
        if (embedding != null)
        {
            embedding.IsStale = true;
            await _embeddingService.UpdateAsync(embedding);
        }
    }

    private async Task ExecuteReminderAsync(ScheduleEntry entry)
    {
        _logger.LogInformation("Triggering reminder {Reminder} with message '{Message}'.", entry.Id, entry.Content);

        await _messagingService.SendMessageAsync(entry.Content, includeInLlmContext: true);
    }

    private async Task ExecuteSelfPromptAsync(ScheduleEntry entry)
    {
        _logger.LogInformation("Triggering self-prompt {Prompt} with content '{Content}'.", entry.Id, entry.Content);

        try
        {
            var llmResponse = await _llmClient.SendSelfPromptAsync(entry.Content);
            if (llmResponse.FunctionCallResponses.Count == 0)
            {
                await _messagingService.SendMessageAsync(
                    $"[System] Self-prompt {entry.Id} failed because no function calls were made. Prompt: '{entry.Content}'.",
                    includeInLlmContext: false
                );
            }
            else
            {
                var responseBuilder = new StringBuilder();
                foreach (var callResponse in llmResponse.FunctionCallResponses)
                {
                    if (callResponse.Contains("Sent message to user"))
                        continue;

                    responseBuilder.AppendLine("```");
                    responseBuilder.AppendLine(callResponse);
                    responseBuilder.AppendLine("```");
                }

                var response = responseBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(response))
                    await _messagingService.SendMessageAsync(response, includeInLlmContext: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Sending self-prompt failed. Exception: {Exception}", ex.ToString());
        }
    }
}
