using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer.Combat
{
    public class HitPacket : PacketWriter
    {
        private const int PacketNumber = 1013;

        public HitPacket(int attackerHandler, int targetHandler, int finalDamage, int hpBeforeHit, int hpAfterHit, int hitType = 0)
        {
            Type(PacketNumber);
            WriteInt(attackerHandler);
            WriteInt(targetHandler);
            WriteInt(finalDamage * -1);
            WriteInt(hitType);
            WriteInt(hpAfterHit);
            WriteInt(hpBeforeHit);
        }
    }
}