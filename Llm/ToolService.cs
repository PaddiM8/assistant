using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Assistant.Database;
using Assistant.Llm.Schema;
using Assistant.Messaging;
using Assistant.Services;
using Assistant.Services.Planera;
using Assistant.Utils;
using DSharpPlus.Net;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Assistant.Llm;

public class ToolService
{
    public const string EmbeddingInstructions = "Instructions for assistant: Determine if the new memory makes any entries redundant/obsolete/invalid. If so, remove them, but ONLY if they contradict each other. Most of the time they should be kept.";

    private static readonly Dictionary<string, Type> _toolNameToSchema = [];
    private readonly ReminderService _reminderService;
    private readonly EmbeddingService _embeddingService;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly SelfPromptService _selfPromptService;
    private readonly IMessagingService _messagingService;
    private readonly WeatherService _weatherService;
    private readonly PlaneraService _planeraService;
    private readonly HomeAssistantService _homeAssistantService;
    private readonly IConfiguration _configuration;
    private readonly TimeService _timeService;
    private readonly ILogger<ToolService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    static ToolService()
    {
        _toolNameToSchema = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass)
            .Where(x => typeof(IToolSchema).IsAssignableFrom(x))
            .ToDictionary(x => SchemaUtils.GetToolName(x), x => x);
    }

    public ToolService(
        ReminderService reminderService,
        EmbeddingService embeddingService,
        IEmbeddingClient embeddingClient,
        SelfPromptService selfPromptService,
        IMessagingService messagingService,
        WeatherService weatherService,
        PlaneraService planeraService,
        HomeAssistantService homeAssistantService,
        IConfiguration configuration,
        TimeService timeService,
        ILogger<ToolService> logger
    )
    {
        _reminderService = reminderService;
        _embeddingService = embeddingService;
        _embeddingClient = embeddingClient;
        _selfPromptService = selfPromptService;
        _messagingService = messagingService;
        _weatherService = weatherService;
        _planeraService = planeraService;
        _homeAssistantService = homeAssistantService;
        _configuration = configuration;
        _timeService = timeService;
        _logger = logger;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public async Task<ToolResponse> Execute(string name, JsonElement node, string userIdentifier)
    {
        if (!_toolNameToSchema.TryGetValue(name, out var schemaType))
            return new ToolResponse($"Tool not found: '{name}'.");

        try
        {
            if (node.Deserialize(schemaType, _jsonSerializerOptions) is not IToolSchema deserialised)
                return new ToolResponse($"Failed to deserialise tool call");

            return await Execute(deserialised, userIdentifier);
        }
        catch (Exception ex)
        {
            return new ToolResponse($"Failed to deserialise tool call: {ex.Message}");
        }
    }

    public async Task<ToolResponse> Execute(IToolSchema tool, string userIdentifier)
    {
        try
        {
            return tool switch
            {
                // Reminders
                CreateReminderSchema createReminderSchema => await CreateReminderAsync(createReminderSchema, userIdentifier),
                RemoveReminderSchema removeReminderSchema => await RemoveReminderAsync(removeReminderSchema),
                UpdateReminderSchema updateReminderSchema => await UpdateReminderAsync(updateReminderSchema),

                // Memories
                AddUserVectorMemorySchema addUserVectorMemorySchema => await AddUserVectorMemoryAsync(addUserVectorMemorySchema),
                AddAssistantVectorMemorySchema addAssistantVectorMemorySchema => await AddAssistantVectorMemoryAsync(addAssistantVectorMemorySchema),
                SearchVectorMemorySchema searchVectorMemorySchema => await SearchVectorMemoryAsync(searchVectorMemorySchema),
                RemoveVectorMemorySchema removeVectorMemorySchema => await RemoveVectorMemoryAsync(removeVectorMemorySchema),
                UpdateVectorMemorySchema updateVectorMemorySchema => await UpdateVectorMemoryAsync(updateVectorMemorySchema),

                // Autonomy
                ScheduleSelfPromptSchema scheduleSelfPromptSchema => await ScheduleSelfPromptAsync(scheduleSelfPromptSchema, userIdentifier),
                UpdateSelfPromptSchema updateSelfPromptSchema => await UpdateSelfPromptAsync(updateSelfPromptSchema),
                DeleteSelfPromptSchema deleteSelfPromptSchema => await DeleteSelfPromptAsync(deleteSelfPromptSchema),
                MessageUserSchema messageUserSchema => await MessageUserAsync(messageUserSchema, userIdentifier),

                // Second layer
                SecondLayerDocumentationSchema secondLayerDocumentationSchema => await SecondLayerDocumentationAsync(secondLayerDocumentationSchema),

                // Weather
                GetWeatherSchema getWeatherSchema => await GetWeatherAsync(getWeatherSchema),

                // Shopping list
                AddToShoppingListSchema addToShoppingListSchema => await AddToShoppingListAsync(addToShoppingListSchema),
                RetrieveShoppingListSchema retrieveShoppingListSchema => await RetrieveShoppingListAsync(retrieveShoppingListSchema),
                DeleteFromShoppingListSchema deleteFromShoppingListSchema => await DeleteFromShoppingListAsync(deleteFromShoppingListSchema),

                // Home automation
                ListSmartHomeEntityIdsSchema listSmartHomeEntityIdsSchema => await ListSmartHomeEntityIdsAsync(listSmartHomeEntityIdsSchema),
                ControlSmartLightSchema controlSmartLightSchema => await ControlSmartLightAsync(controlSmartLightSchema),
                ResetLightSchema resetLightSchema => await ResetLightAsync(resetLightSchema),
                _ => throw new NotImplementedException(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Call failed: {Exception}", ex.ToString());
            await SendUserResponseAsync($"Call failed: '{ex.Message}'.");

            return new ToolResponse($"Call failed: '{ex.Message}'.");
        }
    }

    public List<Type> GetSchemasInToolGroup(SecondLayerToolGroup toolGroupName)
    {
        return toolGroupName switch
        {
            SecondLayerToolGroup.Reminders => [
                typeof(CreateReminderSchema),
                typeof(UpdateReminderSchema),
                typeof(RemoveReminderSchema),
            ],
            SecondLayerToolGroup.Weather => [typeof(GetWeatherSchema)],
            SecondLayerToolGroup.ShoppingList => [
                typeof(AddToShoppingListSchema),
                typeof(RetrieveShoppingListSchema),
                typeof(DeleteFromShoppingListSchema),
            ],
            SecondLayerToolGroup.HomeAutomation => [
                typeof(ListSmartHomeEntityIdsSchema),
                typeof(ControlSmartLightSchema),
                typeof(ResetLightSchema),
            ],
        };
    }

    private DateTime MakeTimeRelative(DateTime dateTime)
    {
        var now = _timeService.GetNow();

        return dateTime
            .AddMinutes(now.Minute)
            .AddSeconds(now.Second);
    }

    private async Task SendUserResponseAsync(string response)
    {
        await _messagingService.SendMessageAsync($"```cs\n{response}\n```", includeInLlmContext: false);
    }

    private async Task<ToolResponse> CreateReminderAsync(CreateReminderSchema call, string userIdentifier)
    {

        var triggerAt = call.WasConvertedFromRelativeTime
            ? MakeTimeRelative(call.TriggerDateTime)
            : call.TriggerDateTime;

        var reminder = new ScheduleEntry
        {
            CreatedAtUtc = DateTime.UtcNow,
            TriggerAtUtc = _timeService.ToUtc(triggerAt),
            Content = call.Message,
            Kind = ScheduleEntryKind.Reminder,
            Priority = call.Priority ?? MessagePriority.Normal,
            UserIdentifier = userIdentifier,
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
        await SendUserResponseAsync($"Created reminder {id} and priority {call.Priority}. {schedulingString}");

        return new ToolResponse($"Created reminder with ID {id}.");
    }

    private async Task<ToolResponse> RemoveReminderAsync(RemoveReminderSchema call)
    {
        var reminder = await _reminderService.RemoveAsync(call.Id);
        var schedulingString = BuildSchedulingString(
            _timeService.ToLocal(reminder.TriggerAtUtc),
            reminder.Content,
            reminder.RecurrenceUnit,
            reminder.RecurrenceInterval
        );
        await SendUserResponseAsync($"Removed reminder {call.Id}. {schedulingString}.");

        return new ToolResponse($"Removed reminder {call.Id}.");
    }

    private async Task<ToolResponse> UpdateReminderAsync(UpdateReminderSchema call)
    {
        var triggerAt = call.WasConvertedFromRelativeTime
            ? MakeTimeRelative(call.TriggerDateTime)
            : call.TriggerDateTime;

        await _reminderService.UpdateAsync(call.Id, triggerAt, call.Message, call.Priority, call.Recurrence);
        var schedulingString = BuildSchedulingString(
            triggerAt,
            call.Message,
            call.Recurrence?.Frequency,
            call.Recurrence?.Interval
        );
        await SendUserResponseAsync($"Updated reminder {call.Id} and priority {call.Priority}. {schedulingString}");

        return new ToolResponse($"Updated reminder {call.Id}.");
    }

    private async Task<ToolResponse> AddUserVectorMemoryAsync(AddUserVectorMemorySchema call)
    {
        var vector = await _embeddingClient.GetEmbeddingAsync(call.Content);
        var nearestNeighbours = await _embeddingService.GetNearestAsync(vector, EmbeddingContextKind.UserMemory, null, limit: 3);
        var entry = await _embeddingService.AddAsync(EmbeddingContextKind.UserMemory, call.Content, null, null, vector);
        Debug.Assert(entry.Embedding != null);

        var builder = new StringBuilder();
        builder.AppendLine($"User memory added with ID {entry.Id}");

        await SendUserResponseAsync(builder.ToString() + ": " + call.Content.Truncate(500, "... (truncated)"));

        if (nearestNeighbours.Count > 0)
        {
            builder.Append(" next to the following entries (top 3 nearest neighbours):");
            builder.AppendLine();
            builder.AppendLine(BuildEmbeddingListString(nearestNeighbours));
            builder.AppendLine();
            builder.AppendLine(EmbeddingInstructions);
        }

        return new ToolResponse(builder.ToString());
    }

    private async Task<ToolResponse> AddAssistantVectorMemoryAsync(AddAssistantVectorMemorySchema call)
    {
        var vector = await _embeddingClient.GetEmbeddingAsync(call.Content);
        var nearestNeighbours = await _embeddingService.GetNearestAsync(vector, EmbeddingContextKind.AssistantMemory, null, limit: 3);
        var entry = await _embeddingService.AddAsync(EmbeddingContextKind.AssistantMemory, call.Content, null, null, vector);
        Debug.Assert(entry.Embedding != null);

        var builder = new StringBuilder();
        builder.AppendLine($"Assistant memory added with ID {entry.Id}");

        await SendUserResponseAsync(builder.ToString() + ": " + call.Content.Truncate(500, "... (truncated)"));

        if (nearestNeighbours.Count > 0)
        {
            builder.Append(" next to the following entries (top 3 nearest neighbours):");
            builder.AppendLine();
            builder.AppendLine(BuildEmbeddingListString(nearestNeighbours));
            builder.AppendLine();
            builder.AppendLine(EmbeddingInstructions);
        }

        return new ToolResponse(builder.ToString());
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
        {
            await SendUserResponseAsync($"Vector search for '{call.Content}' returned no results.");

            return new ToolResponse("No entry was found.");
        }

        await SendUserResponseAsync($"Vector search for '{call.Content}':" + Environment.NewLine + BuildEmbeddingListStringForUser(nearestList));

        var builder = new StringBuilder();
        builder.AppendLine("Nearest neighbours (top 3):");
        builder.AppendLine(BuildEmbeddingListString(nearestList));
        builder.AppendLine(EmbeddingInstructions);

        return new ToolResponse(builder.ToString());
    }

    private async Task<ToolResponse> RemoveVectorMemoryAsync(RemoveVectorMemorySchema call)
    {
        var entry = await _embeddingService.RemoveAsync(call.Id);
        await SendUserResponseAsync($"Removed vector memory {call.Id}. Content: {entry.Content.Truncate(100, "...")}");

        return new ToolResponse($"Removed vector memory {call.Id}.");
    }

    private async Task<ToolResponse> UpdateVectorMemoryAsync(UpdateVectorMemorySchema call)
    {
        await _embeddingService.UpdateAsync(call.Id, call.Content);
        await SendUserResponseAsync($"Updated vector memory {call.Id}. Content: {call.Content.Truncate(100, "...")}");

        return new ToolResponse($"Updated vector memory {call.Id}.");
    }

    private static string BuildSchedulingString(
        DateTimeOffset triggerAt,
        string? message,
        Frequency? recurrenceUnit,
        int? recurrenceInterval
    )
    {
        var builder = new StringBuilder();
        builder.Append(recurrenceUnit == null ? "Trigger at: " : "Initial trigger at: ");
        builder.Append(triggerAt.ToString(EmbeddingService.DateFormat));

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
            builder.AppendLine($"Created at {entry.AddedAtUtc.ToString(EmbeddingService.DateFormat)} with context {entry.Context}) and memory ID {entry.Id}:");
            builder.AppendLine($"Content: {truncatedContent}");

            if (entry.RelatedItemTableName != null)
            {
                builder.AppendLine("Note: This entry cannot be updated directly. To update it, update the associated item:");
                builder.AppendLine($"  Item: ID={entry.RelatedItemId}, Table={entry.RelatedItemTableName}");
            }

            builder.AppendLine("---");
            isFirst = false;
        }

        return builder.ToString();
    }

    private static string BuildEmbeddingListStringForUser(IEnumerable<EmbeddingEntry> entries)
    {
        var builder = new StringBuilder();
        bool isFirst = true;

        foreach (var entry in entries)
        {
            var maxLength = isFirst ? 750 : 250;
            var truncatedContent = entry.Content.Truncate(maxLength, "... (truncated)");
            builder.AppendLine($"{entry.AddedAtUtc.ToString(EmbeddingService.DateFormat)} [{entry.Context})] ID={entry.Id}:");

            if (entry.RelatedItemTableName != null)
                builder.AppendLine($"(Item: ID={entry.RelatedItemId}, Table={entry.RelatedItemTableName})");

            builder.AppendLine($" > {truncatedContent}");

            builder.AppendLine();
            isFirst = false;
        }

        return builder.ToString();
    }

    private async Task<ToolResponse> ScheduleSelfPromptAsync(ScheduleSelfPromptSchema call, string userIdentifier)
    {
        var triggerAt = call.WasConvertedFromRelativeTime
            ? MakeTimeRelative(call.TriggerDateTime)
            : call.TriggerDateTime;

        var id = await _selfPromptService.Schedule(triggerAt, call.Prompt, userIdentifier, call.Recurrence);
        var schedulingString = BuildSchedulingString(
            triggerAt,
            call.Prompt,
            call.Recurrence?.Frequency,
            call.Recurrence?.Interval
        );
        await SendUserResponseAsync($"Created self-prompt {id}: {schedulingString}.");

        return new ToolResponse($"Created self-prompt with ID {id}.");
    }

    private async Task<ToolResponse> DeleteSelfPromptAsync(DeleteSelfPromptSchema call)
    {
        var entry = await _selfPromptService.RemoveAsync(call.Id);
        await SendUserResponseAsync($"Removed self-prompt with ID {call.Id}: {entry.Content.Truncate(500, "... (truncated)")}");

        return new ToolResponse($"Removed self-prompt.");
    }

    private async Task<ToolResponse> UpdateSelfPromptAsync(UpdateSelfPromptSchema call)
    {
        var triggerAt = call.WasConvertedFromRelativeTime
            ? MakeTimeRelative(call.TriggerDateTime)
            : call.TriggerDateTime;

        await _selfPromptService.UpdateAsync(call.Id, triggerAt, call.Prompt, call.Recurrence);
        var schedulingString = BuildSchedulingString(
            triggerAt,
            call.Prompt,
            call.Recurrence?.Frequency,
            call.Recurrence?.Interval
        );
        await SendUserResponseAsync($"Update self-prompt with ID {call.Id}: {schedulingString}");

        return new ToolResponse($"Updated self-prompt.");
    }

    private async Task<ToolResponse> MessageUserAsync(MessageUserSchema call, string userIdentifier)
    {
        await _messagingService.SendMessageAsync(call.Message, call.Priority, userIdentifier, includeInLlmContext: true);

        return new ToolResponse($"Sent message to user.");
    }

    private async Task<ToolResponse> SecondLayerDocumentationAsync(SecondLayerDocumentationSchema call)
    {
        var schemas = GetSchemasInToolGroup(call.ToolGroupName);
        await SendUserResponseAsync($"Retrieved documentation for {call.ToolGroupName}.");

        return new ToolResponse(string.Empty)
        {
            Tools = schemas.Select(OpenAiUtils.CreateFunctionTool).ToList(),
        };
    }

    private async Task<ToolResponse> GetWeatherAsync(GetWeatherSchema call)
    {
        var weatherData = await _weatherService.GetWeatherDataAsync(call.LocationName, call.StartDate, call.EndDate);
        var startDateString = call.StartDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");
        var endDateString = call.EndDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");
        await SendUserResponseAsync($"Called weather API with start date {startDateString} and end date {endDateString}");

        return new ToolResponse(weatherData);
    }

    private async Task<ToolResponse> AddToShoppingListAsync(AddToShoppingListSchema call)
    {
        if (call.Content.Length < 2)
            return new ToolResponse("Invalid value for 'Content'. Expected item name.");

        var title = char.ToUpper(call.Content[0]) + call.Content[1..];
        var projectId = _configuration.GetSection("Planera").GetValue<string>("ShoppingListId")!;
        var ticketId = await _planeraService.CreateTicketAsync(title, string.Empty, call.Priority, projectId);
        await SendUserResponseAsync($"Added item to shopping list with title '{title}' and priority {call.Priority} (ID={ticketId}).");

        return new ToolResponse($"Added to shopping list with ID {ticketId}.");
    }

    private async Task<ToolResponse> RetrieveShoppingListAsync(RetrieveShoppingListSchema _)
    {
        var projectSlug = _configuration.GetSection("Planera").GetValue<string>("ShoppingListSlug")!;
        var tickets = await _planeraService.GetTicketsAsync(projectSlug, PlaneraTicketFilter.Open);
        var serialised = JsonSerializer.Serialize(tickets);
        await SendUserResponseAsync($"Queried shopping list with slug {projectSlug} and filter 'Open'.");

        return new ToolResponse(serialised);
    }

    private async Task<ToolResponse> DeleteFromShoppingListAsync(DeleteFromShoppingListSchema call)
    {
        var projectId = _configuration.GetSection("Planera").GetValue<string>("ShoppingListId")!;
        foreach (var id in call.Ids)
            await _planeraService.DeleteTicketAsync(projectId, id);

        await SendUserResponseAsync($"Deleted item(s) from the shopping list (IDs={string.Join(", ", call.Ids)}).");

        return new ToolResponse($"Deleted item(s) with IDs: {string.Join(", ", call.Ids)}.");
    }

    private async Task<ToolResponse> ControlSmartLightAsync(ControlSmartLightSchema call)
    {
        await SendUserResponseAsync($"Called smart home API. ID: {call.EntityId}, IsOn: {call.IsOn}, Brightness change: {call.BrightnessPointChange}, Coldness change: {call.ColdnessChangePointChange}");
        await _homeAssistantService.SetLightStateAsync(call.EntityId, call.IsOn, call.BrightnessPointChange, call.ColdnessChangePointChange);

        return new ToolResponse("Success.");
    }

    private async Task<ToolResponse> ListSmartHomeEntityIdsAsync(ListSmartHomeEntityIdsSchema _)
    {
        await SendUserResponseAsync("Retrieved list of smart home entities.");
        var lights = await _homeAssistantService.GetLightsAsync();

        return new ToolResponse(JsonSerializer.Serialize(lights));
    }

    private async Task<ToolResponse> ResetLightAsync(ResetLightSchema call)
    {
        await SendUserResponseAsync($"Reset light {call.EntityId}.");
        await _homeAssistantService.ResetLightAsync(call.EntityId);

        return new ToolResponse("Success.");
    }
}
