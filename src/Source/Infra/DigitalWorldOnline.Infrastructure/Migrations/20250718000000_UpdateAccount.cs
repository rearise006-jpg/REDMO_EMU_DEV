using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalWorldOnline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.columns 
                    WHERE Name = N'IsOnline' 
                    AND Object_ID = Object_ID(N'[Account].[Account]')
                )
                BEGIN
                    ALTER TABLE [Account].[Account]
                    ADD [IsOnline] TINYINT NOT NULL DEFAULT 0;
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
                    WHERE Name = N'IsOnline'
                    AND Object_ID = Object_ID(N'[Account].[Account]')
                )
                BEGIN
                    ALTER TABLE [Account].[Account]
                    DROP COLUMN [IsOnline];
                END;
            ");
        }
    }
}
