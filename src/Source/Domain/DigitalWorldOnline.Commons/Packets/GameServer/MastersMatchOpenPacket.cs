using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class MastersMatchOpenPacket : PacketWriter
    {
        private const int PacketNumber = 3124;

        /// <summary>
        /// Masters Match Open
        /// </summary>
        /// <param name="name">Friend character name</param>
        public MastersMatchOpenPacket(int nTeamACnt, int nTeamBCnt, int nMyCnt, short nMyRank, byte nMyTeam, List<MastersMatchRankerDTO> teamARankers, List<MastersMatchRankerDTO> teamBRankers)
        {
            Type(PacketNumber);

            WriteString(DateTime.Now.AddDays(1).ToShortDateString());   //reset time [32 bytes] 00:00:00
            WriteInt(10); // Tick time

            WriteInt(nTeamACnt);            // Number of donations Team A
            WriteInt(nTeamBCnt);            // Number of donations Team B

            WriteInt(nMyCnt);               // My Donations
            WriteShort(nMyRank);            // My Rank
            WriteByte(nMyTeam);             // My Team

            // --- Team A Top 10 ---
            int teamACount = 0;
            foreach (var ranker in teamARankers.Take(10))
            {
                WriteShort(ranker.Rank);
                WriteString(ranker.TamerName);
                WriteInt(ranker.Donations);
                teamACount++;
            }

            // Preenche os slots restantes do Time A com valores padrão
            for (int i = teamACount; i < 10; i++)
            {
                WriteShort((short)(i + 1));
                WriteString("Not Registered");
                WriteInt(0);
            }

            // --- Team B Top 10 ---
            int teamBCount = 0;
            foreach (var ranker in teamBRankers.Take(10))
            {
                WriteShort(ranker.Rank);
                WriteString(ranker.TamerName);
                WriteInt(ranker.Donations);
                teamBCount++;
            }

            // Preenche os slots restantes do Time B com valores padrão
            for (int i = teamBCount; i < 10; i++)
            {
                WriteShort((short)(i + 1));
                WriteString("Not Registered");
                WriteInt(0);
            }
        }
    }
}