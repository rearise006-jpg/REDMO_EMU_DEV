using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer.Combat
{
    public class KillOnHitPacket : PacketWriter
    {
        private const int PacketNumber = 1020;

        public KillOnHitPacket(int attackerHandler, int targetHandler, int damage, int hitType = 0)
        {
            Type(PacketNumber);
            WriteInt(attackerHandler);
            WriteInt(targetHandler);
            WriteInt(damage * -1);
            WriteInt(hitType);
            WriteShort(0);
        }
    }
}