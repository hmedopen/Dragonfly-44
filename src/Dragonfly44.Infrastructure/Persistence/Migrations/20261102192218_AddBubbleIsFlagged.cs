using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dragonfly44.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBubbleIsFlagged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "Bubbles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Bubbles_PageId_IsFlagged",
                table: "Bubbles",
                columns: new[] { "PageId", "IsFlagged" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bubbles_PageId_IsFlagged",
                table: "Bubbles");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "Bubbles");
        }
    }
}
