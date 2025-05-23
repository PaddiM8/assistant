using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Assistant.Llm.Schema;
using Microsoft.EntityFrameworkCore;

namespace Assistant.Database;

[Index(nameof(TriggerAtUtc))]
[Index(nameof(Kind))]
public class ScheduleEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    public required DateTime CreatedAtUtc { get; set; }

    public required DateTime TriggerAtUtc { get; set; }

    public required string Content { get; set; }

    public required ScheduleEntryKind Kind { get; set; }

    public bool IsActive { get; set; } = true;

    public Frequency? RecurrenceUnit { get; set; }

    public int? RecurrenceInterval { get; set; }
}
