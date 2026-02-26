
namespace DigitalWorldOnline.Commons.DTOs.Assets
{
    public sealed class SkillInfoAssetDTO
    {
        public long Id { get; set; }

        public int SkillId { get; set; }
        
        public string Name { get; set; }

        public byte FamilyType { get; set; }

        public int DSUsage { get; set; }
        
        public int HPUsage { get; set; }
        
        public int Value { get; set; }
        
        public float CastingTime { get; set; }
        
        public int Cooldown { get; set; }
        
        public byte MaxLevel { get; set; }
        
        public byte RequiredPoints { get; set; }
        
        public byte Target { get; set; }

        public int AreaOfEffect { get; set; }
        
        public int AoEMinDamage { get; set; }
        
        public int AoEMaxDamage { get; set; }
        
        public int Range { get; set; }

        public byte UnlockLevel { get; set; }
        
        public byte MemoryChips { get; set; }
        
        public int FirstConditionCode { get; set; }
        
        public int SecondConditionCode { get; set; }
        
        public int ThirdConditionCode { get; set; }
        
        public int Type { get; set; }
        
        public string Description { get; set; }
    }
}