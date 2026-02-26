using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonSkillMemoryEffectSync : PacketWriter
    {
        private const int PacketNumber = 1122;

        /// <summary>
        /// Sends the current available channels list.
        /// </summary>
        public DigimonSkillMemoryEffectSync(int skillCode, int targetHandler)
        {
            Type(PacketNumber);
            WriteInt(targetHandler);
            WriteInt(skillCode);
        }
    }
}