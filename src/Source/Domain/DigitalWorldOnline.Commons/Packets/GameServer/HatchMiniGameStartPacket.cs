using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class HatchMiniGameStartPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Server -> client: Start a bar.
        /// nBarTime: ushort (ms or client units)
        /// nStage: byte (layout stage)
        /// </summary>
        public HatchMiniGameStartPacket(ushort nBarTime, byte nStage)
        {
            Type(PacketNumber);
            WriteShort((short)nBarTime); // u2
            WriteByte(nStage);           // u1
        }
    }
}