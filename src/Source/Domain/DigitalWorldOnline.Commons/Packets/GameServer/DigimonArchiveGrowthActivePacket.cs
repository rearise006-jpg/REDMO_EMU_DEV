using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonArchiveGrowthActivePacket : PacketWriter
    {
        private const int PacketNumber = 3227;

        public DigimonArchiveGrowthActivePacket(byte slotGrowth, int remainTime)
        {
            Type(PacketNumber);
            WriteByte(slotGrowth); 
            WriteInt(remainTime);
        }
    }
}
