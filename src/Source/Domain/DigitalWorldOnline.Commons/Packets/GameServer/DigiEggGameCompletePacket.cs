using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    /// <summary>
    /// GS2C: DigiEgg Mini-Game Completion Confirmation (5008)
    /// Server → Client: Confirms game completion and applies bonuses
    /// Sends final results and bonus information
    /// </summary>
    public class DigiEggGameCompletePacket : PacketWriter
    {
        private const int PacketNumber = 5008;

        /// <summary>
        /// Sends basic game completion confirmation
        /// </summary>
        /// <param name="success">Completion status: 1 = Success, 0 = Failed</param>
        public DigiEggGameCompletePacket(byte success)
        {
            Type(PacketNumber);
            WriteByte(success);
        }

        /// <summary>
        /// Sends game completion with score and bonus
        /// </summary>
        /// <param name="success">Completion status</param>
        /// <param name="finalScore">Final score achieved (0-100)</param>
        /// <param name="bonusPercentage">Bonus percentage applied (as int, multiply by 100)</param>
        public DigiEggGameCompletePacket(byte success, int finalScore, int bonusPercentage)
        {
            Type(PacketNumber);
            WriteByte(success);
            WriteInt(finalScore);
            WriteInt(bonusPercentage);
        }

        /// <summary>
        /// Sends complete game completion data with all bonuses
        /// </summary>
        /// <param name="success">Completion status</param>
        /// <param name="finalScore">Final score achieved</param>
        /// <param name="bonusPercentage">Bonus percentage</param>
        /// <param name="hatchLevelIncrease">Hatch level increase</param>
        /// <param name="totalPlayTime">Total play time in seconds</param>
        public DigiEggGameCompletePacket(byte success, int finalScore, int bonusPercentage, int hatchLevelIncrease, int totalPlayTime)
        {
            Type(PacketNumber);
            WriteByte(success);
            WriteInt(finalScore);
            WriteInt(bonusPercentage);
            WriteInt(hatchLevelIncrease);
            WriteInt(totalPlayTime);
        }

        /// <summary>
        /// Sends detailed game completion information
        /// </summary>
        /// <param name="success">Completion status</param>
        /// <param name="finalScore">Final score</param>
        /// <param name="bonusPercentage">Bonus percentage</param>
        /// <param name="hatchLevelIncrease">Hatch level increase</param>
        /// <param name="totalPlayTime">Total play time</param>
        /// <param name="correctClicksCount">Number of correct clicks</param>
        /// <param name="totalClicksCount">Total number of clicks</param>
        /// <param name="messageText">Bonus/reward message text</param>
        public DigiEggGameCompletePacket(byte success, int finalScore, int bonusPercentage, int hatchLevelIncrease,
                                         int totalPlayTime, int correctClicksCount, int totalClicksCount, string messageText)
        {
            Type(PacketNumber);
            WriteByte(success);
            WriteInt(finalScore);
            WriteInt(bonusPercentage);
            WriteInt(hatchLevelIncrease);
            WriteInt(totalPlayTime);
            WriteInt(correctClicksCount);
            WriteInt(totalClicksCount);
            WriteString(messageText);
        }
    }
}