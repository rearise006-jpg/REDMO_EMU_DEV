namespace DigitalWorldOnline.Commons.Enums.ClientEnums
{
    public enum ApplyStatusEnum : int
    {
        APPLY_HP = 1,               // HP
        APPLY_DS,                   // DS
        APPLY_MAXHP,                // Max HP Extension
        APPLY_MAXDS,                // Max DS Extension
        APPLY_AP,                   // Increase Attack Power
        APPLY_CA,                   // Increase Critical Damage
        APPLY_DP,                   // Increase Defense
        APPLY_EV,                   // Increase Evasion
        APPLY_MS,                   // Increase Movement Speed
        APPLY_AS,                   // Increase Attack Speed
        APPLY_AR,                   // Increase Attack Range
        APPLY_HT,                   // Increase Hit Rate
        APPLY_FP,                   // Increase Fatigue Level
        APPLY_FS,                   // Increase Intimacy
        APPLY_EXP,                  // Experience
        APPLY_POWERAPPLYRATE,       // Improve Socket Application Ability

        APPLY_BL = 17,              // Block Rate
        APPLY_DA,                   // General Attack Damage
        APPLY_ER,                   // General Attack Evasion Probability
        APPLY_AllParam,             // All Parameters Increase (this value is used as a parameter)
        APPLY_SER,                  // Skill Evasion Probability
        APPLY_SDR,                  // Skill Defense Probability
        APPLY_SRR,                  // Skill Resistance
        APPLY_SCD,                  // Skill Damage
        APPLY_SCR,                  // Skill Critical Rate
        APPLY_HRR,                  // HP Recovery
        APPLY_DRR,                  // DS Recovery
        APPLY_MDA,                  // Normal Damage Reduction
        APPLY_HR,                   // General Attack Hit Probability
        APPLY_DSN,                  // Increase Natural DS Recovery
        APPLY_HPN,                  // Increase Natural HP Recovery
        APPLY_STA,                  // Overview/Perception
        APPLY_UB,                   // Invincibility
        APPLY_ATTRIBUTTE,           // Professional Boost/Attribute Boost
        APPLY_CC,                   // Crown Code
        APPLY_CR,                   // Cross Charger
        APPLY_DOT,                  // Damage Over Time
        APPLY_DOT2,                 // Delayed Damage
        APPLY_STUN,                 // Stun/Control Immunity
        APPLY_DR,                   // Damage Reflection
        APPLY_AB,                   // Damage Absorption // 41

        // #ifdef KSJ_ADD_MEMORY_SKILL_20140805
        APPLY_HPDMG,                // Increase Damage Based on Remaining HP, 42 times
        APPLY_ATDMG,                // Increase Damage According to Properties/Attributes
        APPLY_HPDEF,                // Reduce Damage Based on Remaining HP
        APPLY_ATDEF,                // Reduce Damage Depending on Properties/Attributes
        APPLY_PROVOKE,              // Provocation/Taunt
        APPLY_INSURANCE,            // Blossom Recovery/Health Insurance

        // RENEWAL_TAMER_SKILL_20150923
        APPLY_CAT,                  // Critical Damage Type
        APPLY_RDD                   // Resistance Damage Decrease
    }
}