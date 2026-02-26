
namespace DigitalWorldOnline.Commons.DTOs.Character
{
    public sealed class CharacterActiveDeckDTO
    {
        /// <summary>
        /// Sequencial unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Reference tamer type for the atributes.
        /// </summary>
        public int DeckId { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public string DeckName { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public int Condition { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public int ATType { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public int Option { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public int Probability { get; set; }

        /// <summary>
        /// Level reference for the status.
        /// </summary>
        public int Time { get; set; }
        public byte DeckIndex { get; set; }
        /// <summary>
        /// Reference to the owner.
        /// </summary>
        public CharacterDTO Character { get; set; }
        public long CharacterId { get; set; }
        
    }
}
