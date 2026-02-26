using DigitalWorldOnline.Commons.DTOs.Assets;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ArenaRankingDailyUpdatePointsPacket : PacketWriter
    {
        private const int PacketNumber = 4131;

        /// <summary>
        /// Load the arena points and receive rewards.
        /// </summary>
        /// <param name="nDailyPoints">The total points amount.</param>
        /// <param name="nUsedListSize">Amount of used items.</param>
        public ArenaRankingDailyUpdatePointsPacket(int nDailyPoints, short nUsedListSize, short itemSlot, short itemAmount, int itemId, List<ArenaRankingDailyItemRewardModel> rewardsToReceive)
        {
            Type(PacketNumber);
            WriteByte(100);             // Result
            WriteInt(nDailyPoints);     // Total Points inserted

            WriteShort(nUsedListSize);  // Size of actual used item list

            WriteShort(itemSlot);       // Used Item inventory slot
            WriteShort(itemAmount);     // Used Item amount
            WriteInt(itemId);           // Used ItemId

            WriteShort((short)rewardsToReceive.Count);  // List of items given as rewards

            foreach (var item in rewardsToReceive)
            {
                WriteShort((short)item.Amount);         // Reward Item amount
                WriteInt(item.ItemId);                  // Reward ItemId
            }
        }
    }
}
