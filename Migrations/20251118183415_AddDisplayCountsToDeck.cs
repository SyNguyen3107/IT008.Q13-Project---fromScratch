using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT008.Q13_Project___fromScratch.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayCountsToDeck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DueCount",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LearnCount",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NewCount",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueCount",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "LearnCount",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "NewCount",
                table: "Decks");
        }
    }
}
