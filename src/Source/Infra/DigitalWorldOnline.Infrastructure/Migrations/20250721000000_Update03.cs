using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalWorldOnline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update03 : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
