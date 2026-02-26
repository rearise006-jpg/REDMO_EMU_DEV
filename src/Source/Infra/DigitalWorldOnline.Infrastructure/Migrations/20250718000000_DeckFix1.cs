using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalWorldOnline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeckFix1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.columns 
                    WHERE Name = N'CurrentActiveDeck' 
                    AND Object_ID = Object_ID(N'[Character].[Tamer]')
                )
                BEGIN
                    ALTER TABLE [Character].[Tamer]
                    ADD [CurrentActiveDeck] INT NULL DEFAULT 0;
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.columns 
                    WHERE Name = N'DeckRewardReceived' 
                    AND Object_ID = Object_ID(N'[Digimon].[Digimon]')
                )
                BEGIN
                    ALTER TABLE [Digimon].[Digimon]
                    ADD [DeckRewardReceived] TINYINT NOT NULL DEFAULT 0;
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.columns 
                    WHERE Name = N'Resets' 
                    AND Object_ID = Object_ID(N'[Digimon].[Digimon]')
                )
                BEGIN
                    ALTER TABLE [Digimon].[Digimon]
                    ADD [Resets] INT NOT NULL DEFAULT 0;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE Name = N'Resets'
                    AND Object_ID = Object_ID(N'[Digimon].[Digimon]')
                )
                BEGIN
                    ALTER TABLE [Digimon].[Digimon]
                    DROP COLUMN [Resets];
                END;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE Name = N'DeckRewardReceived'
                    AND Object_ID = Object_ID(N'[Digimon].[Digimon]')
                )
                BEGIN
                    ALTER TABLE [Digimon].[Digimon]
                    DROP COLUMN [DeckRewardReceived];
                END;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE Name = N'CurrentActiveDeck'
                    AND Object_ID = Object_ID(N'[Character].[Tamer]')
                )
                BEGIN
                    ALTER TABLE [Character].[Tamer]
                    DROP COLUMN [CurrentActiveDeck];
                END;
            ");
        }
    }
}
