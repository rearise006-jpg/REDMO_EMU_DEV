namespace DigitalWorldOnline.Commons.DTOs.Character
{
    public class CharacterIncubatorDTO
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Hatching egg item id.
        /// </summary>
        public int EggId { get; set; } //TODO: join nos assets

        /// <summary>
        /// Current hatch level for the egg.
        /// </summary>
        public byte HatchLevel { get; set; }

        /// <summary>
        /// Backup disk item id.
        /// </summary>
        public int BackupDiskId { get; set; } //TODO: join nos assets

        /// <summary>
        /// Reference to character.
        /// </summary>
        public long CharacterId { get; set; }
        public CharacterDTO Character { get; set; }

        // <summary>
        /// Current success rate bonus from mini-games (0-100%).
        /// </summary>
        public double CurrentSuccessRate { get; set; } = 0;

        /// <summary>
        /// Total mini-games played for this incubation.
        /// </summary>
        public int MiniGamesPlayed { get; set; } = 0;
    }
}
