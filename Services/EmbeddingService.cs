using Assistant.Database;
using Assistant.Llm;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Assistant.Services;

public class EmbeddingService(
    IEmbeddingClient embeddingClient,
    IServiceProvider serviceProvider,
    TimeService timeService
)
{
    public const string DateFormat = "yyyy-MM-ddTHH:mm:ss";

    private readonly IEmbeddingClient _embeddingClient = embeddingClient;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TimeService _timeService = timeService;

    public async Task<EmbeddingEntry> AddAsync(EmbeddingContextKind context, string input, Type? relatedItemType, int? relatedItemId, Vector? vector = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var relatedItemTableName = relatedItemType == null
            ? null
            : applicationContext.Model.FindEntityType(relatedItemType)?.GetTableName();

        var embedding = new EmbeddingEntry
        {
            AddedAtUtc = DateTime.UtcNow,
            Context = context,
            Content = input,
            Embedding = vector ?? await _embeddingClient.GetEmbeddingAsync(input),
            RelatedItemTableName = relatedItemTableName,
            RelatedItemId = relatedItemId,
        };

        var entry = await applicationContext.Embeddings.AddAsync(embedding);
        await applicationContext.SaveChangesAsync();

        return entry.Entity;
    }

    public async Task<List<EmbeddingEntry>> GetNearestAsync(
        string input,
        EmbeddingContextKind? context,
        Type? relatedItemType,
        int limit = 3,
        bool includeStale = false,
        DateTimeOffset? afterDateTimeLocal = null,
        DateTimeOffset? beforeDateTimeLocal = null
    )
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var queryVector = await _embeddingClient.GetEmbeddingAsync(input);

        return await GetNearestAsync(
            queryVector,
            context,
            relatedItemType,
            limit,
            includeStale,
            afterDateTimeLocal,
            beforeDateTimeLocal
        );
    }

    public async Task<List<EmbeddingEntry>> GetNearestAsync(
        Vector queryVector,
        EmbeddingContextKind? context,
        Type? relatedItemType,
        int limit = 3,
        bool includeStale = false,
        DateTimeOffset? afterDateTime = null,
        DateTimeOffset? beforeDateTime = null
    )
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var query = applicationContext.Embeddings.Where(x => x.Embedding != null);

        if (context.HasValue)
            query = query.Where(x => x.Context == context);

        if (relatedItemType != null)
        {
            var tableName = applicationContext
                .Model
                .FindEntityType(relatedItemType)?
                .GetTableName();
            query = query.Where(x => x.RelatedItemTableName == tableName);
        }

        if (!includeStale)
            query = query.Where(x => !x.IsStale);

        if (afterDateTime.HasValue)
            query = query.Where(x => x.AddedAtUtc > afterDateTime.Value.LocalDateTime);

        if (beforeDateTime.HasValue)
            query = query.Where(x => x.AddedAtUtc < beforeDateTime.Value.LocalDateTime);

        return await query
            .OrderBy(x => x.Embedding!.L2Distance(queryVector))
            .Take(limit)
            .ToListAsync();
    }

    public async Task<EmbeddingEntry?> FindByRelatedItemIdAsync<T>(int relatedItemId)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tableName = applicationContext.Model.FindEntityType(typeof(T))?.GetTableName();

        return await applicationContext.Embeddings
            .Where(x => x.RelatedItemTableName == tableName)
            .Where(x => x.RelatedItemId == relatedItemId)
            .FirstOrDefaultAsync();
    }

    public async Task<EmbeddingEntry> RemoveAsync(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entry = await applicationContext.Embeddings.FindAsync(id)
            ?? throw new ArgumentException($"An embedding with ID {id} was not found.");

        applicationContext.Embeddings.Remove(entry);
        await applicationContext.SaveChangesAsync();

        return entry;
    }

    public async Task RemoveAsync(EmbeddingEntry entry)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        applicationContext.Embeddings.Remove(entry);
        await applicationContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, string content)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entry = await applicationContext.Embeddings.FindAsync(id)
            ?? throw new ArgumentException($"An embedding with ID {id} was not found.");

        entry.Content = content;
        entry.Embedding = await _embeddingClient.GetEmbeddingAsync(content);

        applicationContext.Embeddings.Update(entry);
        await applicationContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(EmbeddingEntry entry)
    {
        using var scope = _serviceProvider.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        applicationContext.Embeddings.Update(entry);
        await applicationContext.SaveChangesAsync();
    }
}
