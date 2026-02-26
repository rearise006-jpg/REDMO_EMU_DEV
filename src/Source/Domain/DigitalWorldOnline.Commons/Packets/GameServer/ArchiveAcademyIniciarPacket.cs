using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Writers;
using DigitalWorldOnline.Commons.Models.Character;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ArchiveAcademyIniciarPacket : PacketWriter
    {
        private const int PacketNumber = 3226;

        /// <summary>
        /// Load Digimon Academy List
        /// </summary>
        public ArchiveAcademyIniciarPacket()
        {
            Type(PacketNumber);

            for (int i = 0; i < 3; i++)
            {
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
            }
        }

        public ArchiveAcademyIniciarPacket(List<CharacterDigimonGrowthSystemModel> growthDigimons)
        {
            Type(PacketNumber);

            for (int i = 0; i < 3; i++)
            {
                var digimon = growthDigimons.FirstOrDefault(d => d.GrowthSlot == i);

                WriteInt(digimon?.ArchiveSlot + 1000 ?? 0);
                WriteInt(digimon?.GrowthItemId ?? 0);

                int totalMinutes = digimon != null ? (int)(digimon.EndDate - DateTime.Now).TotalMinutes : 0;
                totalMinutes = Math.Max(totalMinutes, 0);

                int remainingMinutes = UtilitiesFunctions.RemainingTimeMinutes(totalMinutes);
                WriteInt(remainingMinutes);
            }
        }
    }
}