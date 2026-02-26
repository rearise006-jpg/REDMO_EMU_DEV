
namespace DigitalWorldOnline.Commons.Models.Assets
{

    public class ExtraEvolutionInformationAssetModel
    {
        public long Id { get; set; }

        public int IndexId { get; set; }

        public List<ExtraEvolutionAssetModel> ExtraEvolution { get; set; }

    }
}
