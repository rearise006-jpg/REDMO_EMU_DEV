using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonSkillLimitOpenPacket : PacketWriter
    {
        private const int PacketNumber = 3245;

        public DigimonSkillLimitOpenPacket(int nResult, int nEvoSlot, int itemSlot, int nItemType, DigimonEvolutionModel digimon)
        {
            Type(PacketNumber);
            WriteInt(nResult);
            WriteInt(nEvoSlot);

            WriteInt(digimon.SkillExperience);      // SkillExperience
            WriteInt(digimon.SkillMastery);         // CurrentLevel
            WriteInt(digimon.Unlocked);             // ???
            WriteByte(digimon.SkillPoints);         // SkillPoints

            for (int i = 0; i < 5; i++)
            {
                WriteByte(digimon.Skills[i].MaxLevel);    // MaxLevel
            }

            WriteInt(itemSlot);
            WriteInt(nItemType);
        }
    }
}