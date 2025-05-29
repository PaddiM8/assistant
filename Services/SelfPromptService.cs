using Assistant.Database;
using Assistant.Llm.Schema;

namespace Assistant.Services;

public class SelfPromptService(
    IServiceProvider serviceProvider,
    EmbeddingService embeddingService,
    TimeService timeService
)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly EmbeddingService _embeddingService = embeddingService;
    private readonly TimeService _timeService = timeService;

    public async Task<int> Schedule(DateTimeOffset triggerAt, string prompt, string userIdentifier, Recurrence? recurrence)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Self prompt entry
        var selfPrompt = new ScheduleEntry
        {
            CreatedAtUtc = DateTime.UtcNow,
            TriggerAtUtc = triggerAt.UtcDateTime,
            Content = prompt,
            Kind = ScheduleEntryKind.SelfPrompt,
            UserIdentifier = userIdentifier,
            RecurrenceUnit = recurrence?.Frequency,
            RecurrenceInterval = recurrence?.Interval,
        };
        var entry = applicationContext.ScheduleEntries.Add(selfPrompt);
        await applicationContext.SaveChangesAsync();

        // Embedding entry
        var embeddingContent = BuildEmbeddingContent(
            prompt,
            triggerAt,
            recurrence?.Frequency,
            recurrence?.Interval
        );
        await _embeddingService.AddAsync(
            EmbeddingContextKind.AssistantAction,
            embeddingContent,
            typeof(ScheduleEntry),
            entry.Entity.Id
        );

        return entry.Entity.Id;
    }

    public async Task<ScheduleEntry> RemoveAsync(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Self prompt entry
        var entry = await applicationContext.ScheduleEntries.FindAsync(id)
            ?? throw new ArgumentException($"Entry not found: {id}");

        applicationContext.ScheduleEntries.Remove(entry);
        await applicationContext.SaveChangesAsync();

        // Embedding entry
        var embedding = await _embeddingService.FindByRelatedItemIdAsync<ScheduleEntry>(id);
        if (embedding != null)
            await _embeddingService.RemoveAsync(embedding);

        return entry;
    }

    public async Task UpdateAsync(int id, DateTimeOffset triggerAt, string? prompt, Recurrence? recurrence)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Self prompt entry
        var entry = await applicationContext.ScheduleEntries.FindAsync(id)
            ?? throw new ArgumentException($"Entry not found: {id}");

        entry.TriggerAtUtc = triggerAt.UtcDateTime;

        if (prompt != null)
            entry.Content = prompt;

        if (recurrence != null)
        {
            entry.RecurrenceInterval = recurrence.Interval;
            entry.RecurrenceUnit = recurrence.Frequency;
        }

        applicationContext.ScheduleEntries.Update(entry);
        await applicationContext.SaveChangesAsync();

        // Embedding entry
        var embedding = await _embeddingService.FindByRelatedItemIdAsync<ScheduleEntry>(id);
        if (embedding != null)
        {
            embedding.Content = BuildEmbeddingContent(
                prompt ?? entry.Content,
                triggerAt,
                recurrence?.Frequency,
                recurrence?.Interval
            );
            await _embeddingService.UpdateAsync(embedding);
        }
    }

    private static string BuildEmbeddingContent(string prompt, DateTimeOffset triggerAt, Frequency? recurrenceUnit, int? recurrenceInterval)
    {
        var triggerAtString = triggerAt.LocalDateTime.ToString(EmbeddingService.DateFormat);
        if (!recurrenceUnit.HasValue)
            return $"Self-prompt scheduled for {triggerAtString} with prompt: '{prompt}'.";

        var unit = recurrenceUnit switch
        {
            Frequency.Daily => "day(s)",
            Frequency.Weekly => "week(s)",
            Frequency.Monthly => "month(s)",
            Frequency.Yearly => "year(s)",
        };

        return $"Self-prompt scheduled for initial trigger at {triggerAtString} and then every {recurrenceInterval} {unit} with prompt: '{prompt}'";
    }
}
