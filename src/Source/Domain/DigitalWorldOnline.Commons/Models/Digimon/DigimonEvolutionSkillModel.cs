namespace DigitalWorldOnline.Commons.Models.Digimon
{
    public sealed partial class DigimonEvolutionSkillModel
    {
        public long Id { get; private set; }

        public byte CurrentLevel { get; private set; }

        public int Duration { get; private set; }

        public DateTime EndDate { get; private set; }

        public byte MaxLevel { get; private set; }

        public long EvolutionId { get; set; }

        public DigimonEvolutionSkillModel()
        {
            CurrentLevel = 1;
            MaxLevel = 10;
        }
    }
}
