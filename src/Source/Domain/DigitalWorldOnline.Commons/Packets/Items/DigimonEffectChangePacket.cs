using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.Items
{
    public class DigimonEffectChangePacket : PacketWriter
    {
        private const int PacketNumber = 3248;

        public DigimonEffectChangePacket(int targetHandler, int effectType)
        {
            Type(PacketNumber);
            WriteInt(targetHandler);
            WriteInt(effectType);
        }
    }
}
