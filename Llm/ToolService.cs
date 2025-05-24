using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Assistant.Database;
using Assistant.Llm.Schema;
using Assistant.Messaging;
using Assistant.Services;
using Assistant.Services.Planera;
using Assistant.Utils;

namespace Assistant.Llm;

public class ToolService(
    ReminderService reminderService,
    EmbeddingService embeddingService,
    IEmbeddingClient embeddingClient,
    SelfPromptService selfPromptService,
    IMessagingService messagingService,
    WeatherService weatherService,
    PlaneraService planeraService,
    IConfiguration configuration,
    JsonSerializerOptions jsonSerializerOptions
)
{
    private static readonly Dictionary<string, Type> _toolNameToSchema = [];
    private readonly ReminderService _reminderService = reminderService;
    private readonly EmbeddingService _embeddingService = embeddingService;
    private readonly IEmbeddingClient _embeddingClient = embeddingClient;
    private readonly SelfPromptService _selfPromptService = selfPromptService;
    private readonly IMessagingService _messagingService = messagingService;
    private readonly WeatherService _weatherService = weatherService;
    private readonly PlaneraService _planeraService = planeraService;
    private readonly IConfiguration _configuration = configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions = jsonSerializerOptions;

    static ToolService()
    {
        _toolNameToSchema = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass)
            .Where(x => typeof(IToolSchema).IsAssignableFrom(x))
            .ToDictionary(x => SchemaUtils.GetToolName(x), x => x);
    }

    public async Task<ToolResponse> Execute(string name, JsonElement node)
    {
        if (!_toolNameToSchema.TryGetValue(name, out var schemaType))
            return new ToolResponse($"Tool not found: '{name}'.");

        try
        {
            if (node.Deserialize(schemaType, _jsonSerializerOptions) is not IToolSchema deserialised)
                return new ToolResponse($"Failed to deserialise tool call");

            return await Execute(deserialised);
        }
        catch (Exception ex)
        {
            return new ToolResponse($"Failed to deserialise tool call: {ex.Message}");
        }
    }

    public async Task<ToolResponse> Execute(IToolSchema tool)
    {
        try
        {
            return tool switch
            {
                // Reminders
                CreateReminderSchema createReminderSchema => await CreateReminderAsync(createReminderSchema),
                RemoveReminderSchema removeReminderSchema => await RemoveReminderAsync(removeReminderSchema),
                UpdateReminderSchema updateReminderSchema => await UpdateReminderAsync(updateReminderSchema),

                // Memories
                AddUserVectorMemorySchema addUserVectorMemorySchema => await AddUserVectorMemoryAsync(addUserVectorMemorySchema),
                AddAssistantVectorMemorySchema addAssistantVectorMemorySchema => await AddAssistantVectorMemoryAsync(addAssistantVectorMemorySchema),
                SearchVectorMemorySchema searchVectorMemorySchema => await SearchVectorMemoryAsync(searchVectorMemorySchema),
                RemoveVectorMemorySchema removeVectorMemorySchema => await RemoveVectorMemoryAsync(removeVectorMemorySchema),
                UpdateVectorMemorySchema updateVectorMemorySchema => await UpdateVectorMemoryAsync(updateVectorMemorySchema),

                // Autonomy
                ScheduledSelfPromptSchema selfPromptSchema => await SelfPromptAsync(selfPromptSchema),
                MessageUserSchema messageUserSchema => await MessageUserAsync(messageUserSchema),

                // Second layer
                SecondLayerDocumentationSchema secondLayerDocumentationSchema => SecondLayerDocumentation(secondLayerDocumentationSchema),

                // Weather
                GetWeatherSchema getWeatherSchema => await GetWeatherAsync(getWeatherSchema),

                // Shopping list
                AddToShoppingListSchema addToShoppingListSchema => await AddToShoppingListAsync(addToShoppingListSchema),
                RetrieveShoppingListSchema retrieveShoppingListSchema => await RetrieveShoppingListAsync(retrieveShoppingListSchema),
                _ => throw new NotImplementedException(),
            };
        }
        catch (Exception ex)
        {
            return new ToolResponse($"Call failed: '{ex.Message}'.");
        }
    }

    private async Task<ToolResponse> CreateReminderAsync(CreateReminderSchema call)
    {
        var reminder = new ScheduleEntry
        {
            CreatedAtUtc = DateTime.UtcNow,
            TriggerAtUtc = call.TriggerDateTime.UtcDateTime,
            Content = call.Message,
            Kind = ScheduleEntryKind.Reminder,
            RecurrenceUnit = call.Recurrence?.Frequency,
            RecurrenceInterval = call.Recurrence?.Interval,
        };

        var id = await _reminderService.AddAsync(reminder, call.TriggerDateTime);
        var schedulingString = BuildSchedulingString(
            call.TriggerDateTime,
            call.Message,
            call.Recurrence?.Frequency,
            call.Recurrence?.Interval
        );
        var userResponse = $"Created reminder {id}. {schedulingString}";

        return new ToolResponse($"Created reminder with ID {id}.", userResponse);
    }

    private async Task<ToolResponse> RemoveReminderAsync(RemoveReminderSchema call)
    {
        var reminder = await _reminderService.RemoveAsync(call.Id);
        var schedulingString = BuildSchedulingString(
            reminder.TriggerAtUtc,
            reminder.Content,
            reminder.RecurrenceUnit,
            reminder.RecurrenceInterval,
            isUtc: true
        );
        var userResponse =  $"Removed reminder {call.Id}. {schedulingString}.";

        return new ToolResponse($"Removed reminder {call.Id}.", userResponse);
    }

    private async Task<ToolResponse> UpdateReminderAsync(UpdateReminderSchema call)
    {
        await _reminderService.UpdateAsync(call.Id, call.TriggerDateTime, call.Message, call.Recurrence);
        var schedulingString = BuildSchedulingString(
            call.TriggerDateTime,
            call.Message,
            call.Recurrence?.Frequency,
            call.Recurrence?.Interval
        );
        var userResponse =  $"Updated reminder {call.Id}. {schedulingString}";

        return new ToolResponse($"Updated reminder {call.Id}.", userResponse);
    }

    private async Task<ToolResponse> AddUserVectorMemoryAsync(AddUserVectorMemorySchema call)
    {
        var vector = await _embeddingClient.GetEmbeddingAsync(call.Content);
        var nearestNeighbours = await _embeddingService.GetNearestAsync(vector, EmbeddingContextKind.UserMemory, null, limit: 3);
        var entry = await _embeddingService.AddAsync(EmbeddingContextKind.UserMemory, call.Content, null, null, vector);
        Debug.Assert(entry.Embedding != null);

        var builder = new StringBuilder();
        builder.AppendLine($"User memory added with ID {entry.Id}");
        var userResponse = builder.ToString().Trim() + ".";
        if (nearestNeighbours.Count > 0)
        {
            builder.Append(" next to the following entries (top 3 nearest neighbours):");
            builder.AppendLine();
            builder.AppendLine(BuildEmbeddingListString(nearestNeighbours));
            builder.AppendLine();
            builder.AppendLine("Instructions for assistant: Determine if any of these entries are redundant/obsolete/invalid given the context of the new memory. If so, remove them.");
        }

        return new ToolResponse(builder.ToString(), userResponse);
    }

    private async Task<ToolResponse> AddAssistantVectorMemoryAsync(AddAssistantVectorMemorySchema call)
    {
        var vector = await _embeddingClient.GetEmbeddingAsync(call.Content);
        var nearestNeighbours = await _embeddingService.GetNearestAsync(vector, EmbeddingContextKind.AssistantMemory, null, limit: 3);
        var entry = await _embeddingService.AddAsync(EmbeddingContextKind.AssistantMemory, call.Content, null, null, vector);
        Debug.Assert(entry.Embedding != null);

        var builder = new StringBuilder();
        builder.AppendLine($"Assistant memory added with ID {entry.Id}");
        var userResponse = builder.ToString().Trim() + $". Content: '{call.Content}'.";
        if (nearestNeighbours.Count > 0)
        {
            builder.Append(" next to the following entries (top 3 nearest neighbours):");
            builder.AppendLine();
            builder.AppendLine(BuildEmbeddingListString(nearestNeighbours));
            builder.AppendLine();
            builder.AppendLine("Instructions for assistant: Determine if any of these entries are redundant/obsolete/invalid given the context of the new memory. If so, remove them.");
        }

        return new ToolResponse(builder.ToString(), userResponse);
    }

    private async Task<ToolResponse> SearchVectorMemoryAsync(SearchVectorMemorySchema call)
    {
        var nearestList = await _embeddingService.GetNearestAsync(
            call.Content,
            call.Context,
            null,
            limit: 3,
            call.IncludeStale,
            call.AfterDateTime,
            call.BeforeDateTime
        );
        if (nearestList.Count == 0)
            return new ToolResponse("No entry was found.");

        var builder = new StringBuilder();
        builder.AppendLine("Nearest neighbours (top 3):");
        builder.AppendLine(BuildEmbeddingListString(nearestList));
        builder.AppendLine("Instructions for assistant: Determine if any of these entries are redundant/obsolete/invalid given the context of the new memory. If so, remove them.");

        return new ToolResponse(builder.ToString());
    }

    private async Task<ToolResponse> RemoveVectorMemoryAsync(RemoveVectorMemorySchema call)
    {
        var entry = await _embeddingService.RemoveAsync(call.Id);
        var userResponse = $"Removed vector memory {call.Id}. Content: '{entry.Content.Truncate(100, "...")}'";

        return new ToolResponse($"Removed vector memory {call.Id}.", userResponse);
    }

    private async Task<ToolResponse> UpdateVectorMemoryAsync(UpdateVectorMemorySchema call)
    {
        await _embeddingService.UpdateAsync(call.Id, call.Content);
        var userResponse = $"Updated vector memory {call.Id}. Content: '{call.Content.Truncate(100, "...")}'";

        return new ToolResponse($"Updated vector memory {call.Id}.", userResponse);
    }

    private static string BuildSchedulingString(
        DateTimeOffset triggerAt,
        string? message,
        Frequency? recurrenceUnit,
        int? recurrenceInterval,
        bool isUtc = false
    )
    {
        var builder = new StringBuilder();
        builder.Append(recurrenceUnit == null ? "Trigger at: " : "Initial trigger at: ");
        builder.Append(triggerAt.ToString("O"));

        if (isUtc)
            builder.Append(" UTC");

        if (recurrenceUnit.HasValue)
        {
            var unit = recurrenceUnit switch
            {
                Frequency.Daily => "day(s)",
                Frequency.Weekly => "week(s)",
                Frequency.Monthly => "month(s)",
                Frequency.Yearly => "year(s)",
            };

            builder.Append($" and then every {recurrenceInterval} {unit} with");
        }

        builder.Append($" message: '{message ?? "*unchanged*"}'");

        return builder.ToString();
    }

    private static string BuildEmbeddingListString(IEnumerable<EmbeddingEntry> entries)
    {
        var builder = new StringBuilder();
        bool isFirst = true;

        foreach (var entry in entries)
        {
            var maxLength = isFirst ? 750 : 250;
            var truncatedContent = entry.Content.Truncate(maxLength, "... (truncated)");
            builder.AppendLine($"Created at {entry.AddedAtUtc.ToString("O")} with context {entry.Context}) and memory ID {entry.Id}:");
            builder.AppendLine($"Content: {truncatedContent}");

            if (entry.RelatedItemTableName != null)
                builder.AppendLine($"Item: ID={entry.RelatedItemId}, Table={entry.RelatedItemTableName}");

            builder.AppendLine("---");
            isFirst = false;
        }

        return builder.ToString();
    }

    private async Task<ToolResponse> SelfPromptAsync(ScheduledSelfPromptSchema call)
    {
        var id = await _selfPromptService.Schedule(call.TriggerDateTime, call.Prompt, call.Recurrence);
        var schedulingString = BuildSchedulingString(
            call.TriggerDateTime,
            call.Prompt,
            call.Recurrence?.Frequency,
            call.Recurrence?.Interval
        );
        var userResponse = $"Created self-prompt {id}. {schedulingString}.";

        return new ToolResponse($"Created self-prompt with ID {id}.", userResponse);
    }

    private async Task<ToolResponse> MessageUserAsync(MessageUserSchema call)
    {
        await _messagingService.SendMessageAsync(call.Message, includeInLlmContext: true);

        return new ToolResponse($"Sent message to user.");
    }

    private ToolResponse SecondLayerDocumentation(SecondLayerDocumentationSchema call)
    {
        List<Type> schemas = call.ToolGroupName switch
        {
            SecondLayerToolGroup.Weather => [typeof(GetWeatherSchema)],
            SecondLayerToolGroup.ShoppingList => [
                typeof(AddToShoppingListSchema),
                typeof(RetrieveShoppingListSchema)
            ],
        };

        return new ToolResponse(string.Empty, $"Retrieved documentation for {call.ToolGroupName}.")
        {
            Tools = schemas.Select(OpenAiUtils.CreateFunctionTool).ToList(),
        };
    }

    private async Task<ToolResponse> GetWeatherAsync(GetWeatherSchema call)
    {
        var weatherData = await _weatherService.GetWeatherDataAsync(call.Longitude, call.Latitude, call.StartDate, call.EndDate);
        var startDateString = call.StartDate.ToUniversalTime().ToString("yyyy-MM-dd");
        var endDateString = call.EndDate.ToUniversalTime().ToString("yyyy-MM-dd");

        return new ToolResponse(weatherData, $"Called weather API with start date {startDateString} and end date {endDateString}");
    }

    private async Task<ToolResponse> AddToShoppingListAsync(AddToShoppingListSchema call)
    {
        throw new NotImplementedException();
    }

    private async Task<ToolResponse> RetrieveShoppingListAsync(RetrieveShoppingListSchema call)
    {
        var projectSlug = _configuration.GetSection("Planera").GetValue<string>("ShoppingListSlug")!;
        var tickets = await _planeraService.GetTicketsAsync(projectSlug, PlaneraTicketFilter.Open);
        var serialised = JsonSerializer.Serialize(tickets);

        return new ToolResponse(serialised, $"Called Planera API with slug {projectSlug} and filter 'Open'.");
    }
}
