using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Assistant.Migrations
{
    /// <inheritdoc />
    public partial class FullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "FullTextSearchVector",
                table: "Embeddings",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Content" });

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_FullTextSearchVector",
                table: "Embeddings",
                column: "FullTextSearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Embeddings_FullTextSearchVector",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "FullTextSearchVector",
                table: "Embeddings");
        }
    }
}
