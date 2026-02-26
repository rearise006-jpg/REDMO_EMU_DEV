namespace DigitalWorldOnline.Commons.Enums
{
    public enum StatusTypeEnum
    {
        Unknow = 1,
        AS,
        AT,
        BL,
        CT,
        DE,
        DS,
        EV,
        HP,
        HT,
        MS
    }

    public enum DeckConditionEnum
    {
        None = 0,
        Passive = 1,
        Active = 2,
        ActiveTime = 3,
    }

    public enum DeckOptionEnum
    {
        None = 0,
        AttackUp = 1,
        SkillDamageUp = 2,
        CriticalUp = 3,
        SkillCooldown = 4,
        HPUp = 5,
        AttackSpeedUp = 6,
        SkillTimeUp = 15,
    }
}