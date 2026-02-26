using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class PinochiGetInfoPacket : PacketWriter
    {
        private const int PacketNumber = 3126;

        public PinochiGetInfoPacket(int nResetTime, int nAllVote, int nMyVote)
        {
            Type(PacketNumber);
            WriteInt(nResetTime);       // Next update time -> Seconds

            for (int i = 0; i < 5; i++)
            {
                WriteInt(nAllVote);         // Total votes for each card
            }

            for (int i = 0; i < 5; i++)
            {
                WriteInt(nMyVote);          // The number of votes you have for each card
            }
        }
    }
}