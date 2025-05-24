using Assistant.Database;
using Assistant.Llm.Schema;

namespace Assistant.Services;

public class SelfPromptService(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<int> Schedule(DateTimeOffset triggerAtLocal, string prompt, string userIdentifier, Recurrence? recurrence)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var selfPrompt = new ScheduleEntry
        {
            CreatedAtUtc = DateTime.UtcNow,
            TriggerAtUtc = triggerAtLocal.UtcDateTime,
            Content = prompt,
            Kind = ScheduleEntryKind.SelfPrompt,
            UserIdentifier = userIdentifier,
            RecurrenceUnit = recurrence?.Frequency,
            RecurrenceInterval = recurrence?.Interval,
        };

        var entry = applicationContext.ScheduleEntries.Add(selfPrompt);
        await applicationContext.SaveChangesAsync();

        return entry.Entity.Id;
    }
}
