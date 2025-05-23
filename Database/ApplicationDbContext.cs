using Microsoft.EntityFrameworkCore;

namespace Assistant.Database;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<ScheduleEntry> ScheduleEntries { get; set; }

    public DbSet<EmbeddingEntry> Embeddings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.Entity<EmbeddingEntry>(entity =>
        {
            entity.HasGeneratedTsVectorColumn(
                e => e.FullTextSearchVector,
                "english",
                e => e.Content
            );

            entity.HasIndex(x => x.FullTextSearchVector).HasMethod("gin");
        });
    }
}
