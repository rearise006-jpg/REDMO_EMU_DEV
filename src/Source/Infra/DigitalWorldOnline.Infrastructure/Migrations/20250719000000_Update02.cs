using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalWorldOnline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Character");

            migrationBuilder.CreateTable(
                name: "DigimonGrowthSystem",
                schema: "Character",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrowthSlot = table.Column<int>(type: "int", nullable: false),
                    ArchiveSlot = table.Column<int>(type: "int", nullable: false),
                    GrowthItemId = table.Column<int>(type: "int", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExperienceAccumulated = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<int>(type: "int", nullable: false),
                    DigimonId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterId = table.Column<long>(type: "bigint", nullable: false),
                    DigimonArchiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigimonGrowthSystem", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                    name: "IX_DigimonGrowthSystem_DigimonArchiveId",
                    schema: "Character",
                    table: "DigimonGrowthSystem",
                    column: "DigimonArchiveId");

            migrationBuilder.AddForeignKey(
                name: "FK_DigimonGrowthSystem_DigimonArchive_DigimonArchiveId",
                schema: "Character",
                table: "DigimonGrowthSystem",
                column: "DigimonArchiveId",
                principalSchema: "Character",
                principalTable: "DigimonArchive",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigimonGrowthSystem",
                schema: "Character");
        }
    }
}
