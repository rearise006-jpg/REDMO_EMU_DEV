using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class HatchMiniGameTimeOutPacket : PacketWriter
    {
        private const int PacketNumber = 5007;

        /// <summary>
        /// Server -> client: timeout / init for next bar
        /// nBarTime: u2 next bar charging time
        /// </summary>
        public HatchMiniGameTimeOutPacket(ushort nBarTime)
        {
            Type(PacketNumber);
            WriteShort((short)nBarTime);
        }
    }
}