using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Assistant.Migrations
{
    /// <inheritdoc />
    public partial class Recurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "Reminders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceUnit",
                table: "Reminders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SelfPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    RecurrenceUnit = table.Column<int>(type: "integer", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfPrompts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_RelatedItemId",
                table: "Embeddings",
                column: "RelatedItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SelfPrompts");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_RelatedItemId",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "RecurrenceUnit",
                table: "Reminders");
        }
    }
}
