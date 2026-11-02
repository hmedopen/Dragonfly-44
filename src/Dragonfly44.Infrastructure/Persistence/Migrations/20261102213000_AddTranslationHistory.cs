using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dragonfly44.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastManualTranslation",
                table: "Bubbles",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAiTranslated",
                table: "Bubbles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TranslationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BubbleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    TranslatedText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AlternativeTranslationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAccepted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationHistories_Bubbles_BubbleId",
                        column: x => x.BubbleId,
                        principalTable: "Bubbles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationHistories_BubbleId",
                table: "TranslationHistories",
                column: "BubbleId");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationHistories_BubbleId_CreatedAtUtc",
                table: "TranslationHistories",
                columns: new[] { "BubbleId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationHistories");

            migrationBuilder.DropColumn(
                name: "LastManualTranslation",
                table: "Bubbles");

            migrationBuilder.DropColumn(
                name: "IsAiTranslated",
                table: "Bubbles");
        }
    }
}
