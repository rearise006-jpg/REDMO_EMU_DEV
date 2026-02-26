using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ArenaRequestFameListPacket : PacketWriter
    {
        private const int PacketNumber = 16026;

        public ArenaRequestFameListPacket(short nSeasonSize, byte nSeasonIdx)
        {
            Type(PacketNumber);
            WriteShort(nSeasonSize);

            for (int n = 0; n < nSeasonSize; ++n)
            {
                WriteByte(nSeasonIdx);
            }
        }
    }
}