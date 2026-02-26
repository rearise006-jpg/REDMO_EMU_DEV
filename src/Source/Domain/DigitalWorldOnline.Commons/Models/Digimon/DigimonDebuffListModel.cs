
namespace DigitalWorldOnline.Commons.Models.Digimon
{
    public sealed partial class DigimonDebuffListModel
    {
        /// <summary>
        /// Unique sequential identifier.
        /// </summary>
        public long Id { get; private set; }

        /// <summary>
        /// Buffs inside the list.
        /// </summary>
        public List<DigimonDebuffModel> Buffs { get; set; }


        public DigimonDebuffListModel()
        {
            Buffs = new List<DigimonDebuffModel>();
        }

        // Add this to the appropriate namespace and file
        public class UpdateDigimonDebuffListCommand
        {
            public DigimonDebuffListModel DebuffList { get; }

            public UpdateDigimonDebuffListCommand(DigimonDebuffListModel debuffList)
            {
                DebuffList = debuffList;
            }
        }
    }
}