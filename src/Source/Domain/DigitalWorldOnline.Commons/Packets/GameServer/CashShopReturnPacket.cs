using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class CashShopReturnPacket : PacketWriter
    {
        private const int PacketNumber = 3413;

        public CashShopReturnPacket(short Result, int RealCash, int RealBonus, sbyte TotalSuccess, sbyte TotalFail, List<int> successIds = null, List<int> failIds = null)
        {
            Type(PacketNumber);
            WriteShort(Result);
            WriteInt(RealCash);
            WriteInt(RealBonus);

            if (successIds == null) successIds = new List<int>();
            if (failIds == null) failIds = new List<int>();

            WriteByte((byte)successIds.Count);
            foreach (int id in successIds)
                WriteInt(id);

            WriteByte((byte)failIds.Count);
            foreach (int id in failIds)
                WriteInt(id);
        }
    }
}