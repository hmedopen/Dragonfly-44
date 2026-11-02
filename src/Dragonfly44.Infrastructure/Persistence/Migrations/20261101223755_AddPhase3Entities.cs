using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dragonfly44.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileFormat",
                table: "Pages",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "Pages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "Pages",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DraftPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DraftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false, defaultValue: ""),
                    ImageRelativePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FileFormat = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: ""),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftPages_Drafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "Drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftPages_DraftId_PageNumber",
                table: "DraftPages",
                columns: new[] { "DraftId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_Name",
                table: "Drafts",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftPages");

            migrationBuilder.DropTable(
                name: "Drafts");

            migrationBuilder.DropColumn(
                name: "FileFormat",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "Pages");
        }
    }
}
