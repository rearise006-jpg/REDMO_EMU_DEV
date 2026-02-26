using DigitalWorldOnline.Commons.Enums.ClientEnums;

namespace DigitalWorldOnline.Commons.DTOs.Character
{
    public class CharacterTamerSkillDTO
    {
        public Guid Id { get; set; }
        public TamerSkillTypeEnum Type { get; set; }
        public int SkillId { get; set; }
        public int Cooldown { get; set; }
        public int Duration { get; set; }
        public DateTime EndCooldown { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Reference to the owner.
        /// </summary>
        public CharacterDTO Character { get; set; }
        public long CharacterId { get; set; }
    }
}
