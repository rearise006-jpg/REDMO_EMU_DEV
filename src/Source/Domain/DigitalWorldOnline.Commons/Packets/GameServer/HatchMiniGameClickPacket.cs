using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class HatchMiniGameClickPacket : PacketWriter
    {
        private const int PacketNumber = 5006;

        /// <summary>
        /// Server -> client: Click result, matching client side GS2C_RECV_MAKE_DIGITAMA_MINIGAME_CLICKRESULT
        /// nResult: 1 = success, 0 = fail
        /// nBarIndex: next bar index (u2)
        /// nBarTime: next bar charging time (u2)
        /// </summary>
        public HatchMiniGameClickPacket(bool result, ushort nBarIndex, ushort nBarTime)
        {
            Type(PacketNumber);
            WriteByte((byte)(result ? 1 : 0));   // nResult as byte
            WriteShort((short)nBarIndex);        // nBarIndex (u2)
            WriteShort((short)nBarTime);         // nBarTime (u2)
        }
    }
}