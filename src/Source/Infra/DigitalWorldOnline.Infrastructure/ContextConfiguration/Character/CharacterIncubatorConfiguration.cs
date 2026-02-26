using DigitalWorldOnline.Commons.DTOs.Character;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalWorldOnline.Infrastructure.ContextConfiguration.Character
{
    public class CharacterIncubatorConfiguration : IEntityTypeConfiguration<CharacterIncubatorDTO>
    {
        public void Configure(EntityTypeBuilder<CharacterIncubatorDTO> builder)
        {
            builder
                .ToTable("Incubator", "Character")
                .HasKey(x => x.Id);

            builder
                .Property(x => x.EggId)
                .HasColumnType("int")
                .HasDefaultValue(byte.MinValue)
                .IsRequired();
            
            builder
                .Property(x => x.HatchLevel)
                .HasColumnType("tinyint")
                .HasDefaultValue(byte.MinValue)
                .IsRequired();
            
            builder
                .Property(x => x.BackupDiskId)
                .HasColumnType("int")
                .HasDefaultValue(byte.MinValue)
                .IsRequired();

            builder
                .Property(x => x.CurrentSuccessRate)
                .HasColumnType("numeric(9,2)")
                .HasDefaultValue(0)
                .IsRequired();

            builder
                .Property(x => x.MiniGamesPlayed)
                .HasColumnType("int")
                .HasDefaultValue(0)
                .IsRequired();
        }
    }
}