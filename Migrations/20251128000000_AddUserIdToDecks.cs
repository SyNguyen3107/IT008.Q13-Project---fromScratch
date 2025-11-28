using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyFlips.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Decks",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            // Optional: create index to speed up queries by UserId
            migrationBuilder.CreateIndex(
                name: "IX_Decks_UserId",
                table: "Decks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Decks_UserId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Decks");
        }
    }
}
