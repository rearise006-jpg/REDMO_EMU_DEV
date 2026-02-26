namespace DigitalWorldOnline.Commons.DTOs.Digimon
{
    public class DigimonEvolutionSkillDTO
    {
        /// <summary>
        /// Unique sequential identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Current skill level.
        /// </summary>
        public byte CurrentLevel { get; set; }

        /// <summary>
        /// Current Skill Cooldown.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Current Skill Cooldown End Time.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Max skill level.
        /// </summary>
        public byte MaxLevel { get; set; }

        /// <summary>
        /// Reference to the owner.
        /// </summary>
        public DigimonEvolutionDTO Evolution { get; set; }
        public long EvolutionId { get; set; }
    }
}