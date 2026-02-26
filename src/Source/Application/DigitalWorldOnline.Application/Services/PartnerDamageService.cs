using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Utils;

namespace DigitalWorldOnline.Game.Services
{
    public class PartnerDamageService
    {
        private readonly AssetsLoader _assets;
        private readonly Random _rand = new Random();

        public PartnerDamageService(AssetsLoader assets)
        {
            _assets = assets;
        }

        public int CalculateBaseDamage(GameClient client, IMob targetMob, int hitType, out bool blocked)
        {
            blocked = false;
            
            var partner = client.Tamer.Partner;
            var deckResult = TryApplyDeckBonus(client, partner.CD, partner.AT, partner.SCD, true);
            var baseDamage = deckResult.attackBonus;
            if (baseDamage < 0) baseDamage = 0;

            bool hasAttributeAdvantage = partner.BaseInfo.Attribute.HasAttributeAdvantage(targetMob.Attribute);
            bool hasElementAdvantage = partner.BaseInfo.Element.HasElementAdvantage(targetMob.Element);
            bool hasAttributeWeakness = targetMob.Attribute.HasAttributeAdvantage(partner.BaseInfo.Attribute);
            bool hasElementWeakness = targetMob.Element.HasElementAdvantage(partner.BaseInfo.Element);

            double multiplier = 1.0;
            if (hasAttributeAdvantage) multiplier += 0.25;
            if (hasElementAdvantage) multiplier += 0.25;
            if (hasAttributeWeakness) multiplier -= 0.10;
            if (hasElementWeakness) multiplier -= 0.10;

            baseDamage = (int)(baseDamage * multiplier);

            blocked = targetMob.BLValue >= UtilitiesFunctions.RandomDouble();

            if (blocked)
                baseDamage /= 2;

            if (hitType == 1)
                baseDamage += (int)(baseDamage * (deckResult.criticalBonus / 100.0));

            double bonusPercentage = UtilitiesFunctions.RandomDouble() / 100.0;
            int bonusDamage = (int)(baseDamage * (bonusPercentage * 0.05));

            return baseDamage + bonusDamage;
        }

        public int ApplyAdditionalBuffs(GameClient client, int damage)
        {
            if (!client.Tamer.Partner.BuffList.ActiveAttackDamageIncrease())
                return damage;

            double totalIncrease = 0;
            var partner = client.Tamer.Partner;

            var evoInfo = _assets.DigimonBaseInfo.FirstOrDefault(x => x.Type == partner.CurrentType);
            if (evoInfo == null) return damage;

            int evolutionRank = evoInfo.EvolutionType;

            var buffs = partner.BuffList.ActiveBuffs.Where(buff => buff.BuffInfo.SkillInfo.Apply
                    .Any(apply => apply.Attribute == SkillCodeApplyAttributeEnum.EvolutionStepDamageIncreaseBuff));

            foreach (var buff in buffs)
            {
                foreach (var apply in buff.BuffInfo.SkillInfo.Apply)
                {
                    switch (apply.Type)
                    {
                        case SkillCodeApplyTypeEnum.Default:
                            totalIncrease += apply.Value / 100.0;
                            break;
                        case SkillCodeApplyTypeEnum.AlsoPercent:
                        case SkillCodeApplyTypeEnum.Percent:
                            totalIncrease += (apply.Value + buff.TypeN * apply.IncreaseValue) / 100.0;
                            break;
                        case SkillCodeApplyTypeEnum.Unknown200:
                            if (new[] { 6, 14 }.Contains(evolutionRank))
                                totalIncrease += 0.50;
                            else if (new[] { 7, 15 }.Contains(evolutionRank))
                                totalIncrease += 0.25;
                            break;
                    }
                }
            }

            return damage + (int)(damage * totalIncrease);
        }

        public int ApplyDebuffReductions(GameClient client, int damage)
        {
            if (!client.Tamer.Partner.BuffList.ActiveDebuffReductionDamage())
                return damage;

            double totalReduction = 0;

            var debuffs = client.Tamer.Partner.BuffList.ActiveBuffs.Where(buff => buff.BuffInfo.SkillInfo.Apply
                    .Any(apply => apply.Attribute == SkillCodeApplyAttributeEnum.AttackPowerDown));

            foreach (var debuff in debuffs)
            {
                foreach (var apply in debuff.BuffInfo.SkillInfo.Apply)
                {
                    switch (apply.Type)
                    {
                        case SkillCodeApplyTypeEnum.Default:
                            totalReduction += apply.Value / 100.0;
                            break;
                        case SkillCodeApplyTypeEnum.AlsoPercent:
                        case SkillCodeApplyTypeEnum.Percent:
                            totalReduction += (apply.Value + debuff.TypeN * apply.IncreaseValue) / 100.0;
                            break;
                        case SkillCodeApplyTypeEnum.Unknown200:
                            totalReduction += apply.AdditionalValue / 100.0;
                            break;
                    }
                }
            }

            totalReduction = Math.Min(totalReduction, 1.0);
            return damage - (int)(damage * totalReduction);
        }

        public (double criticalBonus, int attackBonus, double skillBonus) TryApplyDeckBonus(GameClient client, double criticalDmg, int attack, double skillDamage, bool isAutoAttack)
        {
            if (client.Tamer.CurrentActiveDeck <= 0 || client.Tamer.CurrentActiveDeck == null || !client.Tamer.ActiveDeck.Any())
                return (criticalDmg, attack, skillDamage);

            double criticalBonus = criticalDmg;
            double attackBonus = attack;
            double skillBonus = skillDamage;

            foreach (var deck in client.Tamer.ActiveDeck)
            {
                // Verificação do tipo de ataque necessário
                if ((deck.ATType == 1 && !isAutoAttack) || (deck.ATType == 2 && isAutoAttack))
                    continue;

                if ((DeckOptionEnum)deck.Option != DeckOptionEnum.SkillCooldown &&
                    deck.Condition == (int)DeckConditionEnum.ActiveTime)
                {
                    switch ((DeckOptionEnum)deck.Option)
                    {
                        case DeckOptionEnum.AttackUp:
                            if (!client.Tamer.Partner.IsDeckBuffActive(DeckOptionEnum.AttackUp))
                            {
                                int chance = deck.Probability;
                                int roll = _rand.Next(1, 10001);

                                if (roll <= chance)
                                {
                                    double bonus = (client.Partner.AT * deck.Value) / 100.0;
                                    attackBonus += bonus;

                                    client.Tamer.Partner.ActivateDeckBuff(DeckOptionEnum.AttackUp, deck.Time);
                                    var duration = UtilitiesFunctions.RemainingTimeSeconds(deck.Time);
                                    client.Send(new EncyclopediaDeckEffectPacket(deck.DeckIndex, duration));
                                }
                            }
                            else
                            {
                                double bonus = (client.Partner.AT * deck.Value) / 100.0;
                                attackBonus += bonus;
                            }
                            break;

                        case DeckOptionEnum.CriticalUp:
                            if (!client.Tamer.Partner.IsDeckBuffActive(DeckOptionEnum.CriticalUp))
                            {
                                int chance = deck.Probability;
                                int roll = _rand.Next(1, 10001);

                                if (roll <= chance)
                                {
                                    double bonus = (client.Partner.CD * deck.Value) / 100.0;
                                    criticalBonus += bonus;

                                    client.Tamer.Partner.ActivateDeckBuff(DeckOptionEnum.CriticalUp, deck.Time);
                                    var duration = UtilitiesFunctions.RemainingTimeSeconds(deck.Time);
                                    client.Send(new EncyclopediaDeckEffectPacket(deck.DeckIndex, duration));
                                }
                            }
                            else
                            {
                                double bonus = (client.Partner.CD * deck.Value) / 100.0;
                                criticalBonus += bonus;
                            }
                            break;

                        case DeckOptionEnum.SkillDamageUp:
                            if (!client.Tamer.Partner.IsDeckBuffActive(DeckOptionEnum.SkillDamageUp))
                            {
                                int chance = deck.Probability;
                                int roll = _rand.Next(1, 10001);
                                if (roll <= chance)
                                {
                                    double bonus = (client.Partner.SCD * deck.Value) / 100.0;
                                    skillBonus += bonus;

                                    client.Tamer.Partner.ActivateDeckBuff(DeckOptionEnum.SkillDamageUp, deck.Time);
                                    var duration = UtilitiesFunctions.RemainingTimeSeconds(deck.Time);
                                    client.Send(new EncyclopediaDeckEffectPacket(deck.DeckIndex, duration));
                                }
                            }
                            else
                            {
                                double bonus = (client.Partner.SCD * deck.Value) / 100.0;
                                skillBonus += bonus;
                            }
                            break;

                        case DeckOptionEnum.HPUp:
                        case DeckOptionEnum.AttackSpeedUp:
                        case DeckOptionEnum.SkillTimeUp:
                            break;
                    }
                }

                if (deck.Option == (int)DeckOptionEnum.SkillCooldown)
                {
                    int roll = _rand.Next(1, 10001);

                    if (roll <= deck.Probability)
                    {
                        foreach (var skill in client.Partner.CurrentEvolution.Skills)
                        {
                            skill.ResetCooldown();
                        }

                        client.Send(new EncyclopediaDeckEffectPacket(100, 1));
                    }
                }
            }

            return (criticalBonus, (int)attackBonus, skillBonus);
        }

    }
}
