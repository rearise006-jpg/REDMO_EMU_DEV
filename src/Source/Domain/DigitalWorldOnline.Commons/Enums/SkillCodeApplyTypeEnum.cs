namespace DigitalWorldOnline.Commons.Enums
{
    public enum SkillCodeApplyTypeEnum
    {
        //TODO: identificar
        None = 0,
        Unknown1 = 1, //(PA-((AP*RD)*(DA*0.01)-(mde*(NA*0.01))+((AttackerLv-TargetLv)*10))) --- Damage calculation
        Unknown2 = 2, //PA-(((AP*RD)*(DA*0.01)+(PB+(skill_apply*skilllevel))-((mde*(NA*0.01))*Attackskill_atb*0.01))+((AttackerLv-TargetLv)*10)) -- Skill damage calculation
        Unknown10 = 10, //(AT*PB)+[AT*{(1+Random/100)*PB}]-[{MD/(TLv/MLv)*100}*100] / Tamer Skill Damage calculation: (AT * B) + {AT * (1 + Random(0~200) / 100) * B} - [{MD / (TL / ML) * 100} * 100]

        Default = 101, //A = A + B
        Percent = 102, //BA = BA + (BA×B÷100)
        AlsoPercent = 106, //+10%

        Unknown103 = 103, //RA = RA + (RA×B÷100)
        Unknown104 = 104, //A = B
        Unknown105 = 105, //A = A + (A×B÷100)
        Unknown107 = 107, //A = A - (B + (Lv×증가수치값÷100)) -- Status ailment/abnormal state
        Unknown108 = 108, //A = A
        Unknown200 = 200, //효과적용 안함
        Unknown201 = 201, //C초동안 A = A + B
        Unknown202 = 202, // For B seconds, A = A + (A * C / 100)
        Unknown203 = 203, // For B seconds, A=A*C/100
        Unknown204 = 204, // For B seconds, A=C
        Unknown205 = 205, //B + ((Skill Lv - 1) *Increase value)
        // Unknown205 :DoT (Damage over Time) formula <- Effect type added in 2014.04.16 patch version
        Unknown206 = 206, //B + ((Skill Lv - 1) *Increase value)
        // Unknown206: Skill damage increase (fixed value) buff <- Effect type added in 2014.04.16 patch version
        Unknown207 = 207, //B + ((Skill Lv - 1) *Increase value)
        // Unknown207 : Skill damage increase (percentage) buff <- Effect type added in 2014.04.16 patch version
        Unknown208 = 208, //Time_DamageS + ((Skill Lv - 1) *Increase value)  -- Time = Time + (SkillLv * Apply)
        Unknown301 = 301, //A = A - B
        Unknown302 = 302, //A = A - (AxB÷100)
        Unknown401 = 401, //B~C Event Synchronization // Event synchronization // For things like fireworks
        Unknown402 = 402, // Temporary change to Digimon scale // For things like mysterious growth fruit
        Unknown403 = 403, //A = A + (Bx100)
        Unknown404 = 404 //A = A - (Bx100)
    }
}