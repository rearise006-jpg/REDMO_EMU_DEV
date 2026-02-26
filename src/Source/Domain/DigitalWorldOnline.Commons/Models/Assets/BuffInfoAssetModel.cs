using DigitalWorldOnline.Commons.Enums.ClientEnums;

namespace DigitalWorldOnline.Commons.Models.Assets
{
    public class BuffInfoAssetModel
    {
        public int BuffId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public int Icon { get; set; }
        public int Type { get; set; }
        public int LifeType { get; set; }
        public int TimeType { get; set; }
        public int MinLevel { get; set; }
        public int Class { get; set; }
        public int Unknow { get; set; }
        public int SkillCode { get; set; }
        public int DigimonSkillCode { get; set; }
        public bool Delete { get; set; }
        public string EffectFile { get; set; } = string.Empty;
        public int ConditionLevel { get; set; }
        public int U { get; set; }
        
        public SkillInfoAssetModel? SkillInfo { get; set; }
        public int SkillId => DigimonSkillCode > 0 ? DigimonSkillCode : SkillCode;
        public void SetSkillInfo(SkillInfoAssetModel? skillCode) => SkillInfo ??= skillCode;

        public bool Pray
        {
            get
            {
                var validPrayTypes = new[]
                {
                    SkillPrayType.Normal,
                    SkillPrayType.Normal1,
                    SkillPrayType.Ultimate
                };

                return validPrayTypes.Contains((SkillPrayType)DigimonSkillCode) || validPrayTypes.Contains((SkillPrayType)SkillCode);
            }
        }

        public bool Cheer
        {
            get
            {
                var validCheerTypes = new[]
                {
                    SkillCheerType.Normal,
                    SkillCheerType.Normal1,
                    SkillCheerType.Normal2,
                    SkillCheerType.Ultimate
                };

                return validCheerTypes.Contains((SkillCheerType)DigimonSkillCode) || validCheerTypes.Contains((SkillCheerType)SkillCode);
            }
        }
    }
}
