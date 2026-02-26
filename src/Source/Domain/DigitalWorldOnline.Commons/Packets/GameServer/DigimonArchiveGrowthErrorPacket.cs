using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonArchiveGrowthErrorPacket : PacketWriter
    {
        private const int PacketNumber = 3228;

        public DigimonArchiveGrowthErrorPacket()
        {
            Type(PacketNumber);
            WriteInt(2);
        }
    }
}