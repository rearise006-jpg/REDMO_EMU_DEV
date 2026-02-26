using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer.Combat
{
    public class DotDamageSkillPacket : PacketWriter
    {
        private const int PacketNumber = 4011;

        public DotDamageSkillPacket(byte nHpRate, int nHitterUID, int nTargetUID, short nBuffCode, int damage, bool die)
        {
            Type(PacketNumber);
            WriteInt(nHitterUID);
            WriteInt(nTargetUID);
            WriteShort(nBuffCode);
            WriteByte(nHpRate);
            WriteInt(damage);

            byte diedByte = die ? (byte)1 : (byte)0;
            WriteByte(diedByte);
        }
    }
}