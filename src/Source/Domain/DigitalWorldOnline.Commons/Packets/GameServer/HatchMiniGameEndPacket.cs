using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class HatchMiniGameEndPacket : PacketWriter
    {
        private const int PacketNumber = 5008;

        // score, elapsedTime (ms/ ticks), resultStatus
        public HatchMiniGameEndPacket(int score, long elapsedTimeMs, byte resultStatus)
        {
            Type(PacketNumber);
            WriteInt(score);
            WriteInt64(elapsedTimeMs);
            WriteByte(resultStatus);
        }
    }
}