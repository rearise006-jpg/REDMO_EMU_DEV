using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class HatchMiniGameLimitPacket : PacketWriter
    {
        private const int PacketNumber = 5009;

        public HatchMiniGameLimitPacket(int limit)
        {
            Type(PacketNumber);
            WriteInt(limit);
        }
    }
}