using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonSkillMemoryCoolTimePacket : PacketWriter
    {
        private const int PacketNumber = 1121;

        /// <summary>
        /// Sends the current available channels list.
        /// </summary>
        public DigimonSkillMemoryCoolTimePacket(int skillCode, int coolTime, int targetHandler, bool isMonster)
        {
            Type(PacketNumber);
            WriteInt(skillCode);
            WriteInt(coolTime);
            if (isMonster)
            {
                WriteInt(targetHandler);
            }
        }
    }
}