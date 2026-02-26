using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Assets;

namespace DigitalWorldOnline.Commons.DTOs.Assets
{
    public sealed class NpcPortalsAmountAssetModel
    {
        /// <summary>
        /// Sequencial unique identifier.
        /// </summary>
        public long Id { get; set; }

        public List<NpcPortalsAssetModel> npcPortalsAsset { get; set; }
    }
}