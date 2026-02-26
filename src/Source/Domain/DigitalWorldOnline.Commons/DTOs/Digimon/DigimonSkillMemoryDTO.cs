using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.DTOs.Events;
using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.DTOs.Shop;
using DigitalWorldOnline.Commons.DTOs.Base;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DigitalWorldOnline.Commons.DTOs.Digimon
{
    public class DigimonSkillMemoryDTO
    {
        public long Id { get; set; }
        public long SkillId { get; set; }
        //Evo Status 0 for close 1 for open
        public byte EvolutionStatus { get; set; }
        public int DigimonType { get; set; }
        public int Cooldown { get; set; }
        public int Duration { get; set; }
        public DateTime EndCooldown { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Reference to the owner.
        /// </summary>
        public DigimonEvolutionDTO Evolution { get; private set; }
        public long EvolutionId { get; set; }
    }
}
