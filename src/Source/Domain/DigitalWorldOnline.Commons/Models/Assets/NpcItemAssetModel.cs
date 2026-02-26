namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class NpcItemAssetModel
    {
        /// <summary>
        /// Sequencial unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Reference to the target item.
        /// </summary>
        public int ItemId { get; set; }
    }
}