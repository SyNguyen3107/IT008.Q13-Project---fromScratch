using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT008.Q13_Project___fromScratch.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Decks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decks", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeckId = table.Column<int>(type: "INTEGER", nullable: false),
                    FrontText = table.Column<string>(type: "TEXT", nullable: false),
                    FrontImagePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    FrontAudioPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    BackText = table.Column<string>(type: "TEXT", nullable: false),
                    BackImagePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    BackAudioPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Interval = table.Column<double>(type: "REAL", precision: 18, scale: 6, nullable: false),
                    EaseFactor = table.Column<double>(type: "REAL", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Cards_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_DeckId_DueDate",
                table: "Cards",
                columns: new[] { "DeckId", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Decks");
        }
    }
}
