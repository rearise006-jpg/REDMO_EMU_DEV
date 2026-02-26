namespace DigitalWorldOnline.Commons.DTOs.Character
{
    public class CharacterDigimonGrowthSystemDTO
    {
        /// <summary>
        /// Unique identifier for the growth item.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Growth slot reference.
        /// </summary>
        public int GrowthSlot { get; set; }

        /// <summary>
        /// Growth slot reference.
        /// </summary>
        public int ArchiveSlot { get; set; }

        /// <summary>
        /// Reference to the item active for growth experience sharing.
        /// </summary>
        public int GrowthItemId { get; set; }

        /// <summary>
        /// Time when the growth item started being used.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Experience accumulated by the Digimon in this growth slot.
        /// </summary>
        public int ExperienceAccumulated { get; set; }

        /// <summary>
        /// Whether the growth item is still valid and active.
        /// </summary>
        public int IsActive { get; set; }

        /// <summary>
        /// Foreign key to the associated DigimonArchiveItem.
        /// </summary>
        public long DigimonId { get; set; }

        public Guid DigimonArchiveId { get; set; }
        public CharacterDigimonArchiveDTO DigimonArchive { get; set; }
        /// <summary>
        /// Foreign key to the associated DigimonArchiveItem.
        /// </summary>
        public long CharacterId { get; set; }

    }
}
