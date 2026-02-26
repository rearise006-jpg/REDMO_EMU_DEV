using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DigitalWorldOnline.Commons.DTOs.Digimon;

namespace DigitalWorldOnline.Commons.Models.Digimon
{
    public class DigimonSkillMemoryModel
    {
        public long Id { get; set; }
        public long EvolutionId { get; set; }
        public long SkillId { get; set; }

        //Evo Status 0 for close 1 for open
        public byte EvolutionStatus { get; set; }
        public int DigimonType { get; set; }
        public int Cooldown { get; set; }
        public int Duration { get; set; }
        public DateTime EndCooldown { get; set; }
        public DateTime EndDate { get; set; }

    }
}
