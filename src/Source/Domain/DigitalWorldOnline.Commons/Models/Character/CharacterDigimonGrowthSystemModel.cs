using System;

namespace DigitalWorldOnline.Commons.Models.Character
{
    public partial class CharacterDigimonGrowthSystemModel
    {
        /// <summary>
        /// Unique identifier for the growth record.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Growth slot reference.
        /// </summary>
        public int GrowthSlot { get; set; }

        /// <summary>
        /// Identifier for the item associated with this growth slot.
        /// </summary>
        public int ArchiveSlot { get; set; }

        /// <summary>
        /// Reference to the item active for growth experience sharing.
        /// </summary>
        public int GrowthItemId { get; set; }

        /// <summary>
        /// Start date for the growth process.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Total experience accumulated during growth.
        /// </summary>
        public int ExperienceAccumulated { get; set; }

        /// <summary>
        /// Indicates whether this growth slot is currently active.
        /// </summary>
        public int IsActive { get; set; }

        public Guid DigimonArchiveId { get; set; }

        public long DigimonId { get; set; }

        public long CharacterId { get; set; }
    }
}
