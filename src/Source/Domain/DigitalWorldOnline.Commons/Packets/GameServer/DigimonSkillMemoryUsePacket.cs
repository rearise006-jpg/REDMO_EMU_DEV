using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonSkillMemoryUsePacket : PacketWriter
    {
        private const int PacketNumber = 1120;

        /// <summary>
        /// Sends the current available channels list.
        /// </summary>
        public DigimonSkillMemoryUsePacket(int skillCode, int coolTime, SkillTargetTypeEnum digimon)
        {
            Type(PacketNumber);
            WriteInt(skillCode);
            WriteInt(coolTime);
            WriteInt(digimon.GetHashCode());


        }
    }
}