namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class EvolutionStageAssetModel
    {
        /// <summary>
        /// Unique sequential identifier.
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// Stage type.
        /// </summary>
        public int Type { get; private set; }

        /// <summary>
        /// Stage value.
        /// </summary>
        public int Value { get; private set; }
    }
}