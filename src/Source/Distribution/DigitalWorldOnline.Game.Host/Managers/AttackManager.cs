using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Utils;
using Serilog;
using DigitalWorldOnline.Commons.Packets.Chat;
using Microsoft.Extensions.Configuration;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.Commons.Enums;

namespace DigitalWorldOnline.Game.Managers
{
    public class AttackManager
    {
        private static bool isBattle;
        public AttackManager()
        {
            isBattle = false;
        }
        public static bool GetBattleStatus()
        {
            return isBattle;
        }

        public static void SetBattleStatus(bool status)
        {
            isBattle = status;
        }
        public static bool IsBattle => isBattle;

        /// <summary>
        /// Calcula el daño base con bonos de atributo y elemento.
        /// Logs detallados para debuggear por qué no se aplican bonos.
        /// </summary>
       public static int CalculateDamage(GameClient client, out double critBonusMultiplier, out bool blocked)
        {
            if (client.Tamer.TargetIMob == null)
            {
                blocked = false;
                critBonusMultiplier = 0;
                return 0;
            }

            var partner = client.Tamer.Partner;
            var mob     = client.Tamer.TargetIMob;

            //Console.WriteLine("\n==================== DAMAGE CALCULATION ====================");

            //Console.WriteLine($"[INPUT] Partner={partner.Name}  Lv={partner.Level}");
            //Console.WriteLine($"[INPUT] Target={mob.Name}      Lv={mob.Level}");
            //Console.WriteLine($"[INPUT] AT={partner.AT}, ATT={partner.ATT}");
            //Console.WriteLine($"[INPUT] Attr={partner.BaseInfo.Attribute} vs {mob.Attribute}");
            //Console.WriteLine($"[INPUT] Elem={partner.BaseInfo.Element} vs {mob.Element}");
            //Console.WriteLine("------------------------------------------------------------");

            double baseDamage = partner.AT;

            // ---- BUFF: Verdandi Survival ------------------------------------------------------
            bool verdandiSurvival = HasVerdandiSurvivalBuff(partner);
            if (verdandiSurvival)
            {
                //Console.WriteLine("[BONUS] VerdandiSurvival: +50% AT");
                baseDamage *= 1.5;
            }

            // ---- ATT Bonus --------------------------------------------------------------------
            double attFactor = 1 + (partner.ATT / 20000.0);
            //Console.WriteLine($"[BONUS] ATT Factor = {attFactor:F4}");
            baseDamage *= attFactor;

            // ---- DEBUFF: Verdandi --------------------------------------------------------------
            bool verdandiDebuff = HasVerdandiDebuff(partner);
            if (verdandiDebuff)
            {
                //Console.WriteLine("[DEBUFF] VerdandiDebuff: Damage * 0.5");
                baseDamage *= 0.5;
            }

            // ---- Random 0–8% bonus -------------------------------------------------------------
            double randomBonus = UtilitiesFunctions.RandomDouble() * 0.08;
            baseDamage *= (1 + randomBonus);
            //Console.WriteLine($"[RANDOM] Bonus = {randomBonus * 100:F2}% → baseDamage={baseDamage:F2}");

            // ---- Level difference bonus --------------------------------------------------------
            double levelMultiplier =
                partner.Level > mob.Level ?
                (0.01 * (partner.Level - mob.Level)) : 0;

            int levelBonus = (int)(baseDamage * levelMultiplier);
            //Console.WriteLine($"[LEVEL] Bonus = {levelBonus} (mult={levelMultiplier:F4})");

            // ---- Attribute bonus ---------------------------------------------------------------
            double attrMult = GetAttributeDamage(client);
            int attributeBonus = (int)Math.Floor(baseDamage * attrMult);

            //Console.WriteLine($"[ATTRIBUTE] Mult={attrMult:F4} → Bonus={attributeBonus}");

            // ---- Element bonus ----------------------------------------------------------------
            double elemMult = GetElementDamage(client);
            int elementBonus = (int)Math.Floor(baseDamage * elemMult);

            //Console.WriteLine($"[ELEMENT] Mult={elemMult:F4} → Bonus={elementBonus}");

            // ---- Block -------------------------------------------------------------------------
            double mobBlock = mob.BLValue;
            blocked = mobBlock >= UtilitiesFunctions.RandomDouble();

            if (blocked)
            {
                //Console.WriteLine("[BLOCK] Attack was BLOCKED → All damage halved");
                baseDamage /= 2;
                levelBonus /= 2;
                attributeBonus /= 2;
                elementBonus /= 2;
            }

            // ---- Damage before crit ------------------------------------------------------------
            double preCritDamage = baseDamage + levelBonus + attributeBonus + elementBonus;
            //Console.WriteLine($"[TOTAL BEFORE CRIT] {preCritDamage:F2}");

            // ---- Critical ----------------------------------------------------------------------
            double CC = partner.CC / 100.0;
            double CD = partner.CD / 100.0;

            double critChance = Math.Min(CC, 100);
            double excess = Math.Max(CC - 100, 0);
            double critMult = CD + excess / 2;

            bool crit = critChance >= UtilitiesFunctions.RandomDouble();
            double finalDamage;

            if (crit && critMult > 0)
            {
                critBonusMultiplier = critMult / 100.0;
                finalDamage = preCritDamage * (1 + critBonusMultiplier);

                //Console.WriteLine($"[CRIT] YES → CritMult={critMult:F2}% → Final={finalDamage:F2}");
                blocked = false; // critical overrides block
            }
            else
            {
                critBonusMultiplier = 0;
                finalDamage = preCritDamage;
                //Console.WriteLine("[CRIT] NO");
            }

            // ---- Apply enemy defense ------------------------------------------------------------
            double enemyDef = Math.Min(mob.DEValue, 3000);
            finalDamage -= enemyDef;
            //Console.WriteLine($"[DEF] EnemyDef={enemyDef} → Final={finalDamage:F2}");

            if (finalDamage < 0)
                finalDamage = 0;

            //Console.WriteLine($"==================== FINAL DAMAGE = {(int)finalDamage} ====================\n");

            return (int)finalDamage;
        }


        public static int ModifyDamageForVerdandi(int damage, DigimonModel target)
        {
            // Check if target has Verdandi debuff
            if (target?.DebuffList?.ActiveBuffs != null)
            {
                bool hasVerdandiDebuff = target.DebuffList.ActiveBuffs.Any(b => b.BuffId == 64000 && !b.Expired);

                if (hasVerdandiDebuff)
                {
                    // Increase incoming damage by 50% for Verdandi debuff
                    return (int)(damage * 1.5f);
                }
            }

            return damage;
        }

        /// <summary>
        /// Checks if a partner has the Verdandi debuff (64000)
        /// </summary>
        /// <param name="partner">The partner to check</param>
        /// <returns>True if the partner has the Verdandi debuff</returns>
        public static bool HasVerdandiDebuff(DigimonModel partner)
        {
            if (partner?.DebuffList == null)
                return false;

            return partner.DebuffList.ActiveBuffs.Any(b => b.BuffId == 64000 && !b.Expired);
        }

        /// <summary>
        /// Checks if a partner has the Verdandi Survival buff (64002)
        /// </summary>
        /// <param name="partner">The partner to check</param>
        /// <returns>True if the partner has the Verdandi Survival buff</returns>
        public static bool HasVerdandiSurvivalBuff(DigimonModel partner)
        {
            if (partner?.BuffList == null)
                return false;

            return partner.BuffList.ActiveBuffs.Any(b => b.BuffId == 64002 && !b.Expired);
        }

        /// <summary>
        /// Modifies the attack value based on VerdandiSurvival buff
        /// </summary>
        /// <param name="partner">The digimon partner</param>
        /// <param name="baseAT">The base attack value</param>
        /// <returns>Modified attack value</returns>
        public static int ApplyVerdandiBuffs(DigimonModel partner, int baseAT)
        {
            int modifiedAT = baseAT;

            // Check for Verdandi Survival buff (50% attack increase)
            if (HasVerdandiSurvivalBuff(partner))
            {
                modifiedAT = (int)(baseAT * 1.5); // 50% increase
            }

            return modifiedAT;
        }

        // <summary>
        // Calculates attribute damage multiplier 
        // Ajustado: Unknown Always +20%.
        // </summary>
        // <summary>
        // Calculates attribute damage multiplier 
        // Unknown: bonus fijo del 20% cuando tiene ventaja, sin usar EXP.
        // Logs exhaustivos para depurar atributos.
        // </summary>
        public static double GetAttributeDamage(GameClient client)
        {
            var partner      = client.Tamer.Partner;
            var partnerAttr  = partner.BaseInfo.Attribute;
            var targetMob    = client.Tamer.TargetIMob;
            var targetAttr   = targetMob.Attribute;

            //Console.WriteLine("========== [ATTR DEBUG] INICIO CÁLCULO ATRIBUTO ==========");
            //Console.WriteLine($"[ATTR DEBUG] Partner: Name={partner.Name}, Level={partner.Level}");
            //Console.WriteLine($"[ATTR DEBUG] Target:  Name={targetMob.Name}, Level={targetMob.Level}");

            //Console.WriteLine($"[ATTR DEBUG] PartnerAttr Enum={partnerAttr}, Raw={(int)partnerAttr}");
            //Console.WriteLine($"[ATTR DEBUG] TargetAttr  Enum={targetAttr}, Raw={(int)targetAttr}");

            // Normalización de atributo inválido en el target → tratar como None
            if (!Enum.IsDefined(typeof(DigimonAttributeEnum), targetAttr))
            {
                //Console.WriteLine($"[ATTR DEBUG] TargetAttr RAW {(int)targetAttr} no pertenece a DigimonAttributeEnum → Normalizando a None");
                targetAttr = DigimonAttributeEnum.None;
                //Console.WriteLine($"[ATTR DEBUG] TargetAttr NORMALIZADO → Enum={targetAttr}, Raw={(int)targetAttr}");
            }

            bool hasAdvantage     = partnerAttr.HasAttributeAdvantage(targetAttr);
            bool hasDisadvantage  = targetAttr.HasAttributeAdvantage(partnerAttr);

            //Console.WriteLine($"[ATTR DEBUG] hasAdvantage={hasAdvantage}, hasDisadvantage={hasDisadvantage}");

            // DESVENTAJA → -25%
            if (hasDisadvantage)
            {
                //Console.WriteLine("[ATTR DEBUG] Rama: DESVENTAJA → Multiplier = -0.25");
                //Console.WriteLine("========== [ATTR DEBUG] FIN CÁLCULO ATRIBUTO ==========\n");
                return -0.25;
            }

            // NEUTRAL → 0 (incluye Unknown vs Unknown si HasAttributeAdvantage devuelve false)
            if (!hasAdvantage)
            {
                //Console.WriteLine("[ATTR DEBUG] Rama: NEUTRAL → Multiplier = 0.00");
                //Console.WriteLine("========== [ATTR DEBUG] FIN CÁLCULO ATRIBUTO ==========\n");
                return 0.0;
            }

            // A partir de aquí: hay VENTAJA

            // Caso especial: Unknown → siempre +20% fijo, sin EXP
            if (partnerAttr == DigimonAttributeEnum.Unknown)
            {
                //Console.WriteLine("[ATTR DEBUG] Rama: UNKNOWN con ventaja → Multiplier fijo = 0.20 (20%)");
                //Console.WriteLine("========== [ATTR DEBUG] FIN CÁLCULO ATRIBUTO ==========\n");
                return 0.20;
            }

            // Atributos normales (Data, Vaccine, Virus) → EXP + accesorios
            double baseExp = partner.GetAttributeExperience();
            double baseMultiplier = Math.Min(baseExp / 10000.0, 1.68); // límite 168%

           //Console.WriteLine($"[ATTR DEBUG] BaseExp={baseExp} → BaseMultiplier={baseMultiplier:F4} ({baseMultiplier * 100:F2}%)");

            var (attributeBonusPercent, _) = CharacterModel.GetDigiviceAttributeAndElementBonus(client);
            double accessoryMultiplier = attributeBonusPercent / 100.0;

           //Console.WriteLine($"[ATTR DEBUG] AccessoryBonus={attributeBonusPercent:F2}% → AccessoryMultiplier={accessoryMultiplier:F4} ({accessoryMultiplier * 100:F2}%)");

            double totalMultiplier = baseMultiplier + accessoryMultiplier;

            //Console.WriteLine($"[ATTR DEBUG] Rama: VENTAJA NORMAL → TotalMultiplier={totalMultiplier:F4} ({totalMultiplier * 100:F2}%)");
            //Console.WriteLine("========== [ATTR DEBUG] FIN CÁLCULO ATRIBUTO ==========\n");

            return totalMultiplier;
        }



        /// <summary>
        /// Calculates element damage multiplier
        /// </summary>

        public static double GetElementDamage(GameClient client)
        {
            var partner = client.Tamer.Partner;
            var partnerElem = partner.BaseInfo.Element;
            var targetElem = client.Tamer.TargetIMob.Element;
            
            //Console.WriteLine($"[ELEM] Base={partnerElem}, Target={targetElem}");

            bool hasAdvantage = partnerElem.HasElementAdvantage(targetElem);
            bool hasDisadvantage = targetElem.HasElementAdvantage(partnerElem);

            // DESVENTAJA → -25%
            if (hasDisadvantage)
            {
            //Console.WriteLine("[ELEM] Disadvantage → -0.25");
                return -0.25;
            }

            // NEUTRAL → 0
            if (!hasAdvantage)
            {
            //Console.WriteLine("[ELEM] Neutral → 0");
                return 0.0;
            }

            // VENTAJA → Multiplicador dinámico basado en experiencia
            double baseExp = partner.GetElementExperience();
            double baseMultiplier = Math.Min(baseExp / 10000.0, 1.00); // Límite de 100% para elementos
            
            //Console.WriteLine($"[ELEM] Base Experience={baseExp} → Base Multiplier={baseMultiplier:F4}");

            // Bonus de accesorios (como porcentaje adicional)
            var (_, elementBonusPercent) = CharacterModel.GetDigiviceAttributeAndElementBonus(client);
            double accessoryMultiplier = elementBonusPercent / 100.0;
            
            //Console.WriteLine($"[ELEM] Accessory Bonus={elementBonusPercent:F2}% → Accessory Multiplier={accessoryMultiplier:F4}");

            // Total (suma de multiplicadores)
            double totalMultiplier = baseMultiplier + accessoryMultiplier;
            //Console.WriteLine($"[ELEM] Total Multiplier={totalMultiplier:F4} ({totalMultiplier * 100:F2}%)");

            return totalMultiplier;
        }

    }
}
