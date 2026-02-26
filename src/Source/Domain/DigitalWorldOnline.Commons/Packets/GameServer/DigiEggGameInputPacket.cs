using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    /// <summary>
    /// GS2C: DigiEgg Mini-Game Button Input Acknowledgement (5006)
    /// Server → Client: Acknowledges button click from client
    /// Sent in response to client's button click during minigame
    /// </summary>
    public class DigiEggGameInputPacket : PacketWriter
    {
        private const int PacketNumber = 5006;

        /// <summary>
        /// Sends click acknowledgement to client
        /// </summary>
        /// <param name="status">Click status: 1 = Correct, 0 = Wrong, 2 = Sequence Complete</param>
        public DigiEggGameInputPacket(byte status)
        {
            Type(PacketNumber);
            WriteByte(status);
        }

        /// <summary>
        /// Sends sequence complete notification
        /// </summary>
        /// <param name="completionPercentage">Percentage of sequence completed (0-100)</param>
        public DigiEggGameInputPacket(byte status, int completionPercentage)
        {
            Type(PacketNumber);
            WriteByte(status); // 2 = Sequence Complete
            WriteInt(completionPercentage);
        }

        /// <summary>
        /// Sends detailed game input response with extra data
        /// </summary>
        /// <param name="status">Click status</param>
        /// <param name="currentPosition">Current position in sequence</param>
        /// <param name="totalSequenceLength">Total sequence length</param>
        /// <param name="timeRemaining">Time remaining in seconds</param>
        public DigiEggGameInputPacket(byte status, int currentPosition, int totalSequenceLength, int timeRemaining)
        {
            Type(PacketNumber);
            WriteByte(status);
            WriteInt(currentPosition);
            WriteInt(totalSequenceLength);
            WriteInt(timeRemaining);
        }
    }
}