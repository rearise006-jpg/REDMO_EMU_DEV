using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonEffectPacket : PacketWriter
    {
        private const int PacketNumber = 3247;

        public DigimonEffectPacket(int result, int itemPos, int itemType, int remainItemCount, byte digimonSlot, byte effectType)
        {
            Type(PacketNumber);
            WriteInt(result);
            WriteInt(itemPos);
            WriteInt(itemType);
            WriteInt(remainItemCount);
            WriteByte(digimonSlot);
            WriteByte(effectType);
        }
    }
}