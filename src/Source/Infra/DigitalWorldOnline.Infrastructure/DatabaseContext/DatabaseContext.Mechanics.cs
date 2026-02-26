using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Infrastructure.ContextConfiguration.Mechanics;
using Microsoft.EntityFrameworkCore;

namespace DigitalWorldOnline.Infrastructure
{
    public partial class DatabaseContext : DbContext
    {
        // Guild
        public DbSet<GuildDTO> Guild { get; set; }
        public DbSet<GuildSkillDTO> GuildSkill { get; set; }
        public DbSet<GuildMemberDTO> GuildMember { get; set; }
        public DbSet<GuildAuthorityDTO> GuildAuthority { get; set; }
        public DbSet<GuildHistoricDTO> GuildHistoric { get; set; }

        // MasterMatch
        public DbSet<MastersMatchDTO> MastersMatches { get; set; }
        public DbSet<MastersMatchRankerDTO> MastersMatchRankers { get; set; }

        internal static void MechanicsEntityConfiguration(ModelBuilder builder)
        {
            // Guild
            builder.ApplyConfiguration(new GuildConfiguration());
            builder.ApplyConfiguration(new GuildSkillConfiguration());
            builder.ApplyConfiguration(new GuildMemberConfiguration());
            builder.ApplyConfiguration(new GuildAuthorityConfiguration());
            builder.ApplyConfiguration(new GuildHistoricConfiguration());

            // MasterMatch
            builder.ApplyConfiguration(new MastersMatchConfiguration());
            builder.ApplyConfiguration(new MastersMatchRankerConfiguration());
        }
    }
}