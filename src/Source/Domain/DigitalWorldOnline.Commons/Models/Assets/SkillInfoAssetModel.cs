using DigitalWorldOnline.Commons.Enums.ClientEnums;

namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class SkillInfoAssetModel
    {
        public long Id { get; private set; }

        public int SkillId { get; private set; }
        
        public string Name { get; private set; }

        public List<SkillCodeApplyAssetModel> Apply { get; private set; }

        public DigimonAttributeEnum AttributeType { get; private set; }

        public DigimonNatureEnum NatureType { get; private set; }

        public DigimonFamilyEnum FamilyType { get; private set; }

        public int DSUsage { get; private set; }
        
        public int HPUsage { get; private set; }
        
        public int Value { get; private set; }
        
        public float CastingTime { get; private set; }
        
        public int Cooldown { get; private set; }
        
        public byte MaxLevel { get; private set; }
        
        public byte RequiredPoints { get; private set; }
        
        public byte Target { get; private set; }

        public int AreaOfEffect { get; private set; }
        
        public int AoEMinDamage { get; private set; }
        
        public int AoEMaxDamage { get; private set; }
        
        public int Range { get; private set; }

        public byte UnlockLevel { get; private set; }

        public int MemoryChip { get; private set; }

        public int RequiredItem { get; private set; }

        public int FirstConditionCode { get; private set; }
        
        public int SecondConditionCode { get; private set; }
        
        public int ThirdConditionCode { get; private set; }
        
        public int Type { get; private set; }
        
        public string Description { get; private set; }

        public SkillInfoAssetModel(int skillId, string name, DigimonFamilyEnum familyType, DigimonAttributeEnum attributeType, DigimonNatureEnum natureType,
        int dsUsage, int hpUsage, float castingTime, int coolDown, byte maxLevel, byte reqPoints, byte target, int areaOfEffect, int aoeMinDamage,
        int aoeMaxDamage, int range, byte unlockLevel, int memoryChip, int requiredItem, List<SkillCodeApplyAssetModel> skillApply)
        {
            SkillId = skillId;
            Name = name;
            FamilyType = familyType;
            AttributeType = attributeType;
            NatureType = natureType;
            DSUsage = dsUsage;
            HPUsage = hpUsage;
            CastingTime = castingTime;
            Cooldown = coolDown;
            MaxLevel = maxLevel;
            RequiredPoints = reqPoints;
            Target = target;
            AreaOfEffect = areaOfEffect;
            AoEMinDamage = aoeMinDamage;
            AoEMaxDamage = aoeMaxDamage;
            Range = range;
            UnlockLevel = unlockLevel;
            MemoryChip = memoryChip;
            RequiredItem = requiredItem;
            Apply = skillApply;
        }
    }
}