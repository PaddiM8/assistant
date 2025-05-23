using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assistant.Migrations
{
    /// <inheritdoc />
    public partial class Staleness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasTriggered",
                table: "SelfPrompts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasTriggered",
                table: "Reminders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStale",
                table: "Embeddings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SelfPrompts_HasTriggered",
                table: "SelfPrompts",
                column: "HasTriggered");

            migrationBuilder.CreateIndex(
                name: "IX_SelfPrompts_TriggerAtUtc",
                table: "SelfPrompts",
                column: "TriggerAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_HasTriggered",
                table: "Reminders",
                column: "HasTriggered");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TriggerAtUtc",
                table: "Reminders",
                column: "TriggerAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_AddedAtUtc",
                table: "Embeddings",
                column: "AddedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_IsStale",
                table: "Embeddings",
                column: "IsStale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SelfPrompts_HasTriggered",
                table: "SelfPrompts");

            migrationBuilder.DropIndex(
                name: "IX_SelfPrompts_TriggerAtUtc",
                table: "SelfPrompts");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_HasTriggered",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_TriggerAtUtc",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_AddedAtUtc",
                table: "Embeddings");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_IsStale",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "HasTriggered",
                table: "SelfPrompts");

            migrationBuilder.DropColumn(
                name: "HasTriggered",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "IsStale",
                table: "Embeddings");
        }
    }
}
