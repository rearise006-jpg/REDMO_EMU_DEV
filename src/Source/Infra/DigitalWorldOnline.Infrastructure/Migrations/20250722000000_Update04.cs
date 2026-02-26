using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalWorldOnline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Character");

            migrationBuilder.CreateTable(
                name: "ActiveDeck",
                schema: "Character",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    DeckId = table.Column<int>(type: "int", nullable: false),
                    DeckName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    ATType = table.Column<int>(type: "int", nullable: false),
                    Option = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    Probability = table.Column<int>(type: "int", nullable: false),
                    TIME = table.Column<int>(type: "int", nullable: false),
                    DeckIndex = table.Column<byte>(type: "tinyint", nullable: false),
                    CharacterId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveDeck", x => x.Id);

                    table.ForeignKey(
                        name: "FK_ActiveDeck_Tamer_CharacterId",
                        column: x => x.CharacterId,
                        principalSchema: "Character",
                        principalTable: "Tamer",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveDeck",
                schema: "Character");
        }
    }
}
