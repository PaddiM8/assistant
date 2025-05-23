using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Assistant.Migrations
{
    /// <inheritdoc />
    public partial class Recurrence2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "SelfPrompts");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_RelatedItemId",
                table: "Embeddings");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_RelatedItemTableName",
                table: "Embeddings");

            migrationBuilder.CreateTable(
                name: "ScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RecurrenceUnit = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_RelatedItemTableName_RelatedItemId",
                table: "Embeddings",
                columns: new[] { "RelatedItemTableName", "RelatedItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_Kind",
                table: "ScheduleEntries",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_TriggerAtUtc",
                table: "ScheduleEntries",
                column: "TriggerAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_RelatedItemTableName_RelatedItemId",
                table: "Embeddings");

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceUnit = table.Column<int>(type: "integer", nullable: true),
                    TriggerAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceUnit = table.Column<int>(type: "integer", nullable: true),
                    TriggerAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfPrompts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_RelatedItemId",
                table: "Embeddings",
                column: "RelatedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_RelatedItemTableName",
                table: "Embeddings",
                column: "RelatedItemTableName");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_HasTriggered",
                table: "Reminders",
                column: "HasTriggered");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TriggerAtUtc",
                table: "Reminders",
                column: "TriggerAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SelfPrompts_HasTriggered",
                table: "SelfPrompts",
                column: "HasTriggered");

            migrationBuilder.CreateIndex(
                name: "IX_SelfPrompts_TriggerAtUtc",
                table: "SelfPrompts",
                column: "TriggerAtUtc");
        }
    }
}
