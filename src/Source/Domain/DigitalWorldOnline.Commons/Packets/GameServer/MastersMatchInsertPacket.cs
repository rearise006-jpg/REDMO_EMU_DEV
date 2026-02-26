using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class MastersMatchInsertPacket : PacketWriter
    {
        private const int PacketNumber = 3125;

        /// <summary>
        /// Masters Match Insert
        /// </summary>
        /// <param name="name">Friend character name</param>
        public MastersMatchInsertPacket(int nSlot, int nItemCnt)
        {
            Type(PacketNumber);

            WriteInt(nSlot);            // Donations inventory Slot
            WriteInt(nItemCnt);         // Donations inventory Amount
        }
    }
}