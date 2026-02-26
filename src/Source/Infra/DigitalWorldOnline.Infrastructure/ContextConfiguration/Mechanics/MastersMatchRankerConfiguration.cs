using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DigitalWorldOnline.Infrastructure.ContextConfiguration.Mechanics
{
    public class MastersMatchRankerConfiguration : IEntityTypeConfiguration<MastersMatchRankerDTO>
    {
        public void Configure(EntityTypeBuilder<MastersMatchRankerDTO> builder)
        {
            builder
                .ToTable("MastersMatchRanker", "MastersMatch")
                .HasKey(x => x.Id);

            builder
                .Property(x => x.Rank)
                .HasColumnType("smallint")
                .IsRequired();

            builder
                .Property(x => x.TamerName)
                .HasColumnType("varchar")
                .HasMaxLength(24) // Baseado no seu pacote [24 bytes]
                .IsRequired();

            builder
                .Property(x => x.Donations)
                .HasColumnType("int")
                .IsRequired();

            builder
                .Property(x => x.Team)
                .HasColumnType("tinyint")
                .IsRequired()
                .HasConversion(new ValueConverter<MastersMatchTeamEnum, byte>(
                    v => (byte)v,
                    v => (MastersMatchTeamEnum)v))
                .IsRequired();

            builder
                .HasOne(x => x.Character)
                .WithMany()
                .HasForeignKey(x => x.CharacterId)
                .IsRequired();
        }
    }
}