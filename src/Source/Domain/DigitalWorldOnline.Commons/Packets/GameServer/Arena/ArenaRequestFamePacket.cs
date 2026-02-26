using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ArenaRequestFamePacket : PacketWriter
    {
        private const int PacketNumber = 16025;

        public ArenaRequestFamePacket(short nSeason, ArenaRankingModel arena)
        {
            Type(PacketNumber);
            WriteShort(nSeason);     // nSeason
            WriteByte(0);           // nResult

            arena.GetTop50();

            WriteByte((byte)arena.Competitors.Count);    // nRankSize

            foreach (var competitor in arena.Competitors.OrderBy(x => x.Position))
            {
                WriteByte((byte)(competitor.Position));  // nRank
                WriteString(competitor.TamerName);       // szName

                if (String.IsNullOrEmpty(competitor.GuildName))
                {
                    WriteString("-----");    // szGuild
                }
                else
                {
                    WriteString(competitor.GuildName);   // szGuild
                }

                WriteInt(competitor.Points);     // nPoint
            }
        }
    }
}