using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    /// <summary>
    /// GS2C: Hatch Minigame Start Packet (5005-1)
    /// Sent when minigame starts - tells client the first bar timing
    /// Client receives: nBarTime (milliseconds for first bar charging)
    /// </summary>
    public class HatchMiniGameStartPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Start the hatch minigame
        /// </summary>
        /// <param name="barTime">First bar charging time in milliseconds (typically 3500ms)</param>
        public HatchMiniGameStartPacket(ushort barTime, byte stage = 0)
        {
            Type(PacketNumber);
            WriteByte(1); // Action: Game Start
            WriteShort((short)barTime); // ✅ Changed from WriteUInt16 to WriteShort
            WriteByte(stage);
        }
    }

    /// <summary>
    /// GS2C: Hatch Minigame Click Result Packet (5005-2)
    /// Sent after client clicks a button - tells if success/fail and next bar timing
    /// </summary>
    public class HatchMiniGameClickResultPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Result of the minigame button click
        /// </summary>
        /// <param name="result">Success/Fail/Break result</param>
        /// <param name="nextBarIndex">Index of next bar to click (0-6 for D,I,G,I,M,O,N)</param>
        /// <param name="nextBarTime">Charging time for next bar in milliseconds</param>
        public HatchMiniGameClickResultPacket(HatchMiniGameResultEnum result, ushort nextBarIndex, ushort nextBarTime)
        {
            Type(PacketNumber);
            WriteByte(2); // Action: Click Result
            WriteByte((byte)result);
            WriteShort((short)nextBarIndex); // ✅ Changed from WriteUInt16 to WriteShort
            WriteShort((short)nextBarTime);  // ✅ Changed from WriteUInt16 to WriteShort
        }
    }

    /// <summary>
    /// GS2C: Hatch Minigame Timeout Packet (5005-3)
    /// Sent when client times out on a bar click
    /// </summary>
    public class HatchMiniGameTimeoutPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Player timed out on current bar
        /// </summary>
        public HatchMiniGameTimeoutPacket()
        {
            Type(PacketNumber);
            WriteByte(3); // Action: Timeout
        }
    }

    /// <summary>
    /// GS2C: Hatch Minigame End Packet (5005-4)
    /// Sent when minigame completes - contains final result
    /// </summary>
    public class HatchMiniGameEndPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Minigame completed - final result
        /// </summary>
        /// <param name="successCount">Number of successful button clicks (0-7)</param>
        public HatchMiniGameEndPacket(uint successCount)
        {
            Type(PacketNumber);
            WriteByte(4); // Action: Game End
            WriteInt((int)(successCount));
        }
    }

    /// <summary>
    /// GS2C: Hatch Minigame Error Packet (5005-5)
    /// Sent when an error occurs during minigame
    /// </summary>
    public class HatchMiniGameErrorPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Error occurred during minigame
        /// </summary>
        /// <param name="errorCode">Error code from HatchMiniGameErrorEnum</param>
        public HatchMiniGameErrorPacket(HatchMiniGameErrorEnum errorCode)
        {
            Type(PacketNumber);
            WriteByte(5); // Action: Error
            WriteByte((byte)errorCode);
        }
    }

    /// <summary>
    /// GS2C: Hatch Minigame Init Packet (5005-6)
    /// Sent to acknowledge minigame initialization
    /// </summary>
    public class HatchMiniGameInitPacket : PacketWriter
    {
        private const int PacketNumber = 5005;

        /// <summary>
        /// Initialize minigame on client
        /// </summary>
        /// <param name="result">Game is ready (1 = true)</param>
        /// <param name="successCount">Current success count</param>
        public HatchMiniGameInitPacket(byte result, uint successCount)
        {
            Type(PacketNumber);
            WriteByte(6); // Action: Init
            WriteByte(result);
            WriteInt((int)(successCount));
        }
    }
}