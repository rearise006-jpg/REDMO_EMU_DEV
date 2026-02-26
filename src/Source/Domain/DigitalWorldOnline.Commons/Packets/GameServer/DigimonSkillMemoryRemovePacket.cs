using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonSkillMemoryRemovePacket : PacketWriter
    {
        private const int PacketNumber = 1119;

        /// <summary>
        /// Sends the current available channels list.
        /// </summary>
        public DigimonSkillMemoryRemovePacket(int skillCode)
        {
            Type(PacketNumber);
            WriteInt(skillCode);

        }
    }
}