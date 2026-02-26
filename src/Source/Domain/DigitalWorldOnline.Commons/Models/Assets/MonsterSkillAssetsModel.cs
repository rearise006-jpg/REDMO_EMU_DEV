namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class MonsterSkillAssetModel
    {   /// <summary>
        /// Digimon type/model
        /// </summary>
        public int Type { get;  set; }

        /// <summary>
        /// Skill id.
        /// </summary>
        public int SkillId { get; set; }

        /// <summary>
        /// Detailed skill information.
        /// </summary>
        public MonsterSkillInfoAssetModel SkillInfo { get; private set; }

        //TODO: Behavior
        public void SetSkillInfo(MonsterSkillInfoAssetModel skillInfo) => SkillInfo ??= skillInfo;
    }
}