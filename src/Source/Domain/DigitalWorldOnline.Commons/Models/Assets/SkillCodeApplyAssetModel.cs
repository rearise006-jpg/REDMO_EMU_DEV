using DigitalWorldOnline.Commons.Enums;

namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class SkillCodeApplyAssetModel
    {
        public int Id { get; set; }

        public SkillCodeApplyAttributeEnum Attribute { get; set; }

        public int Chance { get; set; }

        public int Value { get; set; }

        public int AdditionalValue { get; set; }

        public int BuffCode { get; set; }

        public SkillCodeApplyTypeEnum Type { get; set; }

        public int IncreaseValue { get; set; }
    }
}
