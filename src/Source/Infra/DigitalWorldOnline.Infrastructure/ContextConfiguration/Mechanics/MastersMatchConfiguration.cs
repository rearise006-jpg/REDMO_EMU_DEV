using DigitalWorldOnline.Commons.DTOs.Mechanics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalWorldOnline.Infrastructure.ContextConfiguration.Mechanics
{
    public class MastersMatchConfiguration : IEntityTypeConfiguration<MastersMatchDTO>
    {
        public void Configure(EntityTypeBuilder<MastersMatchDTO> builder)
        {
            builder
                .ToTable("MastersMatch", "MastersMatch")
                .HasKey(x => x.Id);

            builder
                .Property(x => x.LastResetDate)
                .HasColumnType("datetime2")
                .IsRequired();

            builder
                .Property(x => x.TeamADonations)
                .HasColumnType("int")
                .HasDefaultValue(0)
                .IsRequired();

            builder
                .Property(x => x.TeamBDonations)
                .HasColumnType("int")
                .HasDefaultValue(0)
                .IsRequired();

            builder
                .HasMany(m => m.Rankers)
                .WithOne(r => r.MastersMatch)
                .HasForeignKey(r => r.MastersMatchId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}