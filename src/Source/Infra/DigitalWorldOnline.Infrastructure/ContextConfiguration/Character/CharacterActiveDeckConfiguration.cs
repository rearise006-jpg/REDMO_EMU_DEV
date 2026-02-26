using DigitalWorldOnline.Commons.DTOs.Character;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalWorldOnline.Infrastructure.ContextConfiguration.Character
{
    public class CharacterActiveDeckConfiguration : IEntityTypeConfiguration<CharacterActiveDeckDTO>
    {
        public void Configure(EntityTypeBuilder<CharacterActiveDeckDTO> builder)
        {
            builder
                .ToTable("ActiveDeck", "Character")
                .HasKey(x => x.Id);

            builder
                .Property(x => x.DeckId)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.DeckName)
                .HasColumnType("nvarchar(255)") // Definindo o tipo correto para strings
                .IsRequired();

            builder
                .Property(x => x.Condition)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.ATType)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.Option)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.Value)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.Probability)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.Time)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.DeckIndex)
                .HasColumnType("smallint")
                .IsRequired();
        }
    }

}
