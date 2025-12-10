using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyFlips.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_v2_UUID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Decks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false),
                    FrontText = table.Column<string>(type: "TEXT", nullable: false),
                    FrontImagePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    FrontAudioPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    BackText = table.Column<string>(type: "TEXT", nullable: false),
                    BackImagePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    BackAudioPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardProgresses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CardId = table.Column<string>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Interval = table.Column<double>(type: "REAL", precision: 18, scale: 6, nullable: false),
                    EaseFactor = table.Column<double>(type: "REAL", precision: 18, scale: 6, nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false),
                    LastReviewDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardProgresses_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardProgresses_CardId",
                table: "CardProgresses",
                column: "CardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardProgresses_DueDate",
                table: "CardProgresses",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_DeckId",
                table: "Cards",
                column: "DeckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardProgresses");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Decks");
        }
    }
}
