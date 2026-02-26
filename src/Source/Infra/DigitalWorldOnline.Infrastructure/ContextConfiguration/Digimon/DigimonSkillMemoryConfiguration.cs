using DigitalWorldOnline.Commons.DTOs.Digimon;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalWorldOnline.Infraestructure.ContextConfiguration.Digimon
{
    public class DigimonSkillMemoryConfiguration : IEntityTypeConfiguration<DigimonSkillMemoryDTO>
    {
        public void Configure(EntityTypeBuilder<DigimonSkillMemoryDTO> builder)
        {
            builder
                .ToTable("SkillMemory", "Digimon")
                .HasKey(x => x.Id);

            builder
                .Property(x => x.EvolutionId)
                .HasColumnType("bigint")
                .IsRequired();

            builder
                .Property(x => x.SkillId)
                .HasColumnType("bigint")
                .IsRequired();

            builder
                .Property(x => x.EvolutionStatus)
                .HasColumnType("tinyint")
                .IsRequired();

            builder
                .Property(x => x.Cooldown)
                .HasColumnType("int")
                .HasDefaultValue(0)
                .IsRequired();

            builder
                .Property(x => x.Duration)
                 .HasColumnType("int")
                .HasDefaultValue(0)
                .IsRequired();

            builder
                .Property(x => x.EndCooldown)
                 .HasColumnType("datetime2")
                .IsRequired();

            builder
                .Property(x => x.EndDate)
                 .HasColumnType("datetime2")
                .IsRequired();

            builder
                .Property(x => x.DigimonType)
                .HasColumnType("int")
                .HasDefaultValue(0)
                .IsRequired();

        }
    }
}