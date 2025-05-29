using Assistant.Database;
using Assistant.Llm.Schema;
using Assistant.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Assistant.Services;

public class ReminderService(
    EmbeddingService embeddingService,
    IServiceProvider serviceProvider,
    TimeService timeService)
{
    private readonly EmbeddingService _embeddingService = embeddingService;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TimeService _timeService = timeService;

    public async Task<int> AddAsync(ScheduleEntry reminder, DateTimeOffset triggerAt)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Reminder entry
        var entry = applicationContext.ScheduleEntries.Add(reminder);
        await applicationContext.SaveChangesAsync();

        // Embedding entry
        await _embeddingService.AddAsync(
            EmbeddingContextKind.AssistantAction,
            BuildEmbeddingContent(reminder.Content, triggerAt, reminder.RecurrenceUnit, reminder.RecurrenceInterval),
            typeof(ScheduleEntry),
            entry.Entity.Id
        );
        await applicationContext.SaveChangesAsync();

        return entry.Entity.Id;
    }

    public async Task<ScheduleEntry> RemoveAsync(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Reminder entry
        var reminder = await applicationContext.ScheduleEntries.FindAsync(id)
            ?? throw new ArgumentException($"Reminder with ID {id} was not found.");
        applicationContext.ScheduleEntries.Remove(reminder);

        // Embedding entry
        var embedding = await _embeddingService.FindByRelatedItemIdAsync<ScheduleEntry>(id);
        if (embedding != null)
            await _embeddingService.RemoveAsync(embedding);

        await applicationContext.SaveChangesAsync();

        return reminder;
    }

    public async Task UpdateAsync(int id, DateTimeOffset triggerAt, string? message, MessagePriority? priority, Recurrence? recurrence)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Reminder entry
        var reminder = await applicationContext.ScheduleEntries.FindAsync(id)
            ?? throw new ArgumentException($"Reminder with ID {id} was not found.");

        reminder.TriggerAtUtc = triggerAt.UtcDateTime;

        if (message != null)
            reminder.Content = message;

        if (priority.HasValue)
            reminder.Priority = priority.Value;

        if (recurrence != null)
        {
            reminder.RecurrenceUnit = recurrence.Frequency;
            reminder.RecurrenceInterval = recurrence.Interval;
        }

        applicationContext.ScheduleEntries.Update(reminder);

        // Embedding entry
        var embedding = await _embeddingService.FindByRelatedItemIdAsync<ScheduleEntry>(id);
        if (embedding != null)
        {
            embedding.Content = BuildEmbeddingContent(
                message ?? reminder.Content,
                triggerAt,
                recurrence?.Frequency,
                recurrence?.Interval
            );
            await _embeddingService.UpdateAsync(embedding);
        }

        await applicationContext.SaveChangesAsync();
    }

    private static string BuildEmbeddingContent(string message, DateTimeOffset triggerAt, Frequency? recurrenceUnit, int? recurrenceInterval)
    {
        var triggerAtString = triggerAt.LocalDateTime.ToString(EmbeddingService.DateFormat);
        if (!recurrenceUnit.HasValue)
            return $"Reminder scheduled for {triggerAtString} with prompt: '{message}'.";

        var unit = recurrenceUnit switch
        {
            Frequency.Daily => "day(s)",
            Frequency.Weekly => "week(s)",
            Frequency.Monthly => "month(s)",
            Frequency.Yearly => "year(s)",
        };

        return $"Reminder scheduled for initial trigger at {triggerAtString} and then every {recurrenceInterval} {unit} with prompt: '{message}'";
    }
}
