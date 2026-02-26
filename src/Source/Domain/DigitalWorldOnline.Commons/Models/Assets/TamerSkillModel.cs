namespace DigitalWorldOnline.Commons.Models.Assets
{
    public class TamerSkillModel
    {
        public int SkillId { get; set; }
        public int SkillCode { get; set; }
        public int Unknown { get; set; }
        public int Type { get; set; }
        public int Unknown1 { get; set; }
        public int Factor1 { get; set; }
        public int Factor2 { get; set; }
        public int TamerSeqID { get; set; }
        public int DigimonSeqID { get; set; }
        public int UseState { get; set; }
        public int UseAreaCheck { get; set; }
        public int Available { get; set; }
        public int Unknown3 { get; set; }
        /// <summary>
        /// Duration MS
        /// </summary>
        public int Duration { get; set; } = 30;
    }

}
