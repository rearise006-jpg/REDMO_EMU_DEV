using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    /// <summary>
    /// GS2C: DigiEgg Mini-Game Start Notification (5007)
    /// Server → Client: Confirms UI is initialized and ready to play
    /// Server sends game parameters and instructions
    /// </summary>
    public class DigiEggGameStartPacket : PacketWriter
    {
        private const int PacketNumber = 5007;

        /// <summary>
        /// Sends game ready confirmation to client
        /// </summary>
        /// <param name="isReady">Is server ready: 1 = Ready, 0 = Error</param>
        public DigiEggGameStartPacket(byte isReady)
        {
            Type(PacketNumber);
            WriteByte(isReady);
        }

        /// <summary>
        /// Sends game start with basic parameters
        /// </summary>
        /// <param name="isReady">Is server ready</param>
        /// <param name="gameType">Type of minigame (1 = DIGIMON sequence)</param>
        /// <param name="timeLimit">Time limit in seconds</param>
        public DigiEggGameStartPacket(byte isReady, int gameType, int timeLimit)
        {
            Type(PacketNumber);
            WriteByte(isReady);
            WriteInt(gameType);
            WriteInt(timeLimit);
        }

        /// <summary>
        /// Sends complete game initialization data to client
        /// </summary>
        /// <param name="isReady">Is server ready</param>
        /// <param name="gameType">Type of minigame</param>
        /// <param name="timeLimit">Time limit in seconds</param>
        /// <param name="sequenceLength">Length of button sequence to complete</param>
        /// <param name="targetSequence">Target sequence string (e.g., "DIGIMON")</param>
        public DigiEggGameStartPacket(byte isReady, int gameType, int timeLimit, int sequenceLength, string targetSequence)
        {
            Type(PacketNumber);
            WriteByte(isReady);
            WriteInt(gameType);
            WriteInt(timeLimit);
            WriteInt(sequenceLength);
            WriteString(targetSequence);
        }
    }
}