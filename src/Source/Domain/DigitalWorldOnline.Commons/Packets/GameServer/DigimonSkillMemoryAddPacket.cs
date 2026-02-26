using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonSkillMemoryAddPacket : PacketWriter
    {
        private const int PacketNumber = 1118;

        /// <summary>
        /// Add a new Skill memory to Digimon.
        /// </summary>
        public DigimonSkillMemoryAddPacket(int digimonId, int skillCode, int itemId)
        {
            Type(PacketNumber);
            WriteInt(digimonId);
            WriteInt(skillCode);
            WriteInt(itemId);

        }
    }
}