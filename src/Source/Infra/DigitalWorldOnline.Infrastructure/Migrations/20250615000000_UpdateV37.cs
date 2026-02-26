using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalWorldOnline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateV37 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Verifica se o schema 'MastersMatch' existe e o cria se não existir.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'MastersMatch')
                BEGIN
                    EXEC('CREATE SCHEMA MastersMatch');
                END;
            ");

            // CREATE TABLE MastersMatch.MastersMatch
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'MastersMatch' AND TABLE_NAME = 'MastersMatch')
                BEGIN
                    CREATE TABLE [MastersMatch].[MastersMatch](
                        [Id] [bigint] NOT NULL IDENTITY(1,1),
                        [LastResetDate] [datetime2] NOT NULL,
                        [TeamADonations] [int] NOT NULL DEFAULT 0,
                        [TeamBDonations] [int] NOT NULL DEFAULT 0,
                    CONSTRAINT [PK_MastersMatch] PRIMARY KEY CLUSTERED
                    (
                        [Id] ASC
                    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                    ) ON [PRIMARY];
                END;
            ");

            // CREATE TABLE Mechanics.MastersMatchRanker
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'MastersMatch' AND TABLE_NAME = 'MastersMatchRanker')
                BEGIN
                    CREATE TABLE [MastersMatch].[MastersMatchRanker](
                        [Id] [bigint] NOT NULL IDENTITY(1,1),
                        [MastersMatchId] [bigint] NOT NULL,
                        [Rank] [smallint] NOT NULL,
                        [TamerName] [varchar](24) NOT NULL,
                        [Donations] [int] NOT NULL,
                        [Team] [tinyint] NOT NULL,
                        [CharacterId] [bigint] NOT NULL, -- Coluna CharacterId adicionada
                    CONSTRAINT [PK_MastersMatchRanker] PRIMARY KEY CLUSTERED
                    (
                        [Id] ASC
                    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                    ) ON [PRIMARY];

                    -- FOREIGN KEY para MastersMatch.MastersMatch
                    ALTER TABLE [MastersMatch].[MastersMatchRanker] WITH CHECK ADD CONSTRAINT [FK_MastersMatchRanker_MastersMatch_MastersMatchId] FOREIGN KEY([MastersMatchId])
                    REFERENCES [MastersMatch].[MastersMatch] ([Id])
                    ON DELETE CASCADE;

                    ALTER TABLE [MastersMatch].[MastersMatchRanker] CHECK CONSTRAINT [FK_MastersMatchRanker_MastersMatch_MastersMatchId];

                    -- FOREIGN KEY para Character.Tamer (Assumindo que CharacterDTO mapeia para [Character].[Tamer])
                    ALTER TABLE [MastersMatch].[MastersMatchRanker] WITH CHECK ADD CONSTRAINT [FK_MastersMatchRanker_Tamer_CharacterId] FOREIGN KEY([CharacterId])
                    REFERENCES [Character].[Tamer] ([Id]) -- Ajuste o nome do schema e tabela se CharacterDTO mapeia para algo diferente
                    ON DELETE CASCADE; -- Deleção em cascata se o Tamer for deletado (ajuste se necessário)

                    ALTER TABLE [MastersMatch].[MastersMatchRanker] CHECK CONSTRAINT [FK_MastersMatchRanker_Tamer_CharacterId];

                    -- UNIQUE INDEX em CharacterId para garantir que um Character tenha apenas um ranker
                    CREATE UNIQUE INDEX [IX_MastersMatchRanker_CharacterId] ON [MastersMatch].[MastersMatchRanker] ([CharacterId]);
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop Foreign Key e Unique Index primeiro para evitar erros de dependência
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MastersMatchRanker_CharacterId' AND object_id = OBJECT_ID('[MastersMatch].[MastersMatchRanker]'))
                BEGIN
                    DROP INDEX [IX_MastersMatchRanker_CharacterId] ON [MastersMatch].[MastersMatchRanker];
                END;

                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_MastersMatchRanker_Tamer_CharacterId')
                BEGIN
                    ALTER TABLE [MastersMatch].[MastersMatchRanker] DROP CONSTRAINT [FK_MastersMatchRanker_Tamer_CharacterId];
                END;

                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_MastersMatchRanker_MastersMatch_MastersMatchId')
                BEGIN
                    ALTER TABLE [MastersMatch].[MastersMatchRanker] DROP CONSTRAINT [FK_MastersMatchRanker_MastersMatch_MastersMatchId];
                END;
            ");

            // DROP TABLE MastersMatch.MastersMatchRanker
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'MastersMatch' AND TABLE_NAME = 'MastersMatchRanker')
                BEGIN
                    DROP TABLE [MastersMatch].[MastersMatchRanker];
                END;
            ");

            // DROP TABLE MastersMatch.MastersMatch
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'MastersMatch' AND TABLE_NAME = 'MastersMatch')
                BEGIN
                    DROP TABLE [MastersMatch].[MastersMatch];
                END;
            ");
        }
    }
}
