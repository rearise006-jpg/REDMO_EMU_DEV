using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DigitalWorldOnline.Commons.DTOs.Character;

namespace DigitalWorldOnline.Infrastructure.ContextConfiguration.Character
{
    public class CharacterDigimonGrowthSystemConfiguration : IEntityTypeConfiguration<CharacterDigimonGrowthSystemDTO>
    {
        public void Configure(EntityTypeBuilder<CharacterDigimonGrowthSystemDTO> builder)
        {
            builder
                .ToTable("DigimonGrowthSystem", "Character")
                .HasKey(x => x.Id);

            builder
                .Property(x => x.GrowthSlot)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.ArchiveSlot)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.GrowthItemId)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.EndDate)
                .HasColumnType("datetime2")
                .IsRequired();

            builder
                .Property(x => x.ExperienceAccumulated)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.IsActive)
                .HasColumnType("int")
                .IsRequired();

        }
    }
}
