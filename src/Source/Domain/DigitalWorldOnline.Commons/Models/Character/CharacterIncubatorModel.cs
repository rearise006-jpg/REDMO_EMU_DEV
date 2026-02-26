namespace DigitalWorldOnline.Commons.Models.Character
{
    public sealed partial class CharacterIncubatorModel
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// Reference ID to the hatching egg.
        /// </summary>
        public int EggId { get; private set; }

        /// <summary>
        /// Current hatch level for the egg.
        /// </summary>
        public byte HatchLevel { get; private set; }

        public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);

        public DateTime LastHatchTime { get; set; } = DateTime.UtcNow;



        /// <summary>
        /// Backup disk item id.
        /// </summary>
        public int BackupDiskId { get; private set; }

        /// <summary>
        /// Reference ID to character.
        /// </summary>
        public long CharacterId { get; private set; }

        public decimal CurrentSuccessRate { get; set; } = 0m;

        /// <summary>
        /// Current success rate bonus from mini-games (0-100%).
        /// </summary>
        //public double CurrentSuccessRate { get; set; } = 0;

        /// <summary>
        /// Total mini-games played for this incubation.
        /// </summary>
        public int MiniGamesPlayed { get; set; } = 0;

        public double FinalScore { get; set; } = 0;
    }
}
