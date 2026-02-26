using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ArenaRequestOldRankPacket : PacketWriter
    {
        private const int PacketNumber = 16024;

        public ArenaRequestOldRankPacket(byte nResult, int tamerId, ArenaRankingModel arena, byte nType, ArenaRankingStatusEnum nRank, ArenaRankingPositionTypeEnum position)
        {
            Type(PacketNumber);
            WriteByte(nType);
            WriteByte(nResult);
            WriteByte((byte)nRank);

            arena.GetTop100();

            WriteByte((byte)arena.Competitors.Count);
            foreach (var competitor in arena.Competitors.OrderBy(x => x.Position))
            {
                WriteByte(competitor.Position);
                WriteString(competitor.TamerName);

                if (String.IsNullOrEmpty(competitor.GuildName))
                {
                    WriteString("-----");
                }
                else
                {
                    WriteString(competitor.GuildName);
                }

                WriteInt(competitor.Points);
            }
        }
    }
}