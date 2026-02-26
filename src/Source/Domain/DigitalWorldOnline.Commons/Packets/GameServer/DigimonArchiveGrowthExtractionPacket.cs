using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonArchiveGrowthExtractionPacket : PacketWriter
    {
        private const int PacketNumber = 3229;

        public DigimonArchiveGrowthExtractionPacket(byte slotGrowth)
        {
            Type(PacketNumber);
            WriteByte(slotGrowth);
        }
    }
}
