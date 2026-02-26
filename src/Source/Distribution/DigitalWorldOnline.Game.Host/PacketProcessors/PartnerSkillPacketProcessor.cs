using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.Map;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using static DigitalWorldOnline.Commons.Packets.GameServer.AddBuffPacket;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public partial class PartnerSkillPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PartnerSkill;

        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IConfiguration _configuration;

        public PartnerSkillPacketProcessor(AssetsLoader assets, MapServer mapServer, DungeonsServer dungeonServer, EventServer eventServer, PvpServer pvpServer,
            ILogger logger, ISender sender, IConfiguration configuration)
        {
            _assets = assets;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
            _configuration = configuration;
        }

        public Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var skillSlot = packet.ReadByte();
            var attackerHandler = packet.ReadInt();
            var targetHandler = packet.ReadInt();

            if (client.Partner == null)
                return Task.CompletedTask;

            var skill = _assets.DigimonSkillInfo.FirstOrDefault(x => x.Type == client.Partner.CurrentType && x.Slot == skillSlot);

            if (skill == null || skill.SkillInfo == null)
            {
                //_logger.Error($"Skill not found !!");
                return Task.CompletedTask;
            }

            // Add this call to check if it's the special party buff skill (6700391)
            // This checks the skill ID early before any processing to ensure it's captured
            if (skill.SkillId == 6700391)
            {
                _mapServer.OnPartnerSkillUsed(client.Tamer, skill.SkillId);
            }

            var targetSummonMobs = new List<SummonMobModel>();
            SkillTypeEnum skillType;

            if (client.PvpMap)
            {
                var areaOfEffect = skill.SkillInfo.AreaOfEffect;
                var range = skill.SkillInfo.Range;
                var targetType = skill.SkillInfo.Target;

                // PVP SERVER -> ATTACK MOB
                if (_pvpServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId) != null)
                {
                    var mobTarget = _pvpServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                    if (mobTarget == null || client.Partner == null)
                        return Task.CompletedTask;

                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _pvpServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _pvpServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _pvpServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg != 0)
                                {
                                    finalDmg = DebuffReductionDamage(client, finalDmg);
                                }

                                if (finalDmg <= 0) finalDmg = 1;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _pvpServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new CastSkillPacket(
                                    skillSlot,
                                    attackerHandler,
                                    targetHandler
                                ).Serialize()
                            );

                            _pvpServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new AreaSkillPacket(
                                    attackerHandler,
                                    client.Partner.HpRate,
                                    targetMobs,
                                    skillSlot,
                                    finalDmg
                                ).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());


                            }
                            else
                            {

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new KillOnSkillPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg
                                        ).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_pvpServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }

                }
                // PVP SERVER -> ATTACK PLAYER
                else if (_pvpServer.GetEnemyByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId) != null)
                {
                    //_logger.Information($"Getting digimon target !!");

                    var pvpPartner = _pvpServer.GetEnemyByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _pvpServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _pvpServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _pvpServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg != 0)
                                {
                                    finalDmg = DebuffReductionDamage(client, finalDmg);
                                }

                                if (finalDmg <= 0) finalDmg = 1;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _pvpServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new CastSkillPacket(
                                    skillSlot,
                                    attackerHandler,
                                    targetHandler
                                ).Serialize()
                            );

                            _pvpServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new AreaSkillPacket(
                                    attackerHandler,
                                    client.Partner.HpRate,
                                    targetMobs,
                                    skillSlot,
                                    finalDmg
                                ).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());


                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new KillOnSkillPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg
                                        ).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_pvpServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }

                }
                // MAP SERVER -> ATTACK MOB
                else if (_mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId) != null)
                {
                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _mapServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _mapServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else if (areaOfEffect == 0 && targetType == 80)
                    {
                        skillType = SkillTypeEnum.Implosion;

                        var targets = new List<MobConfigModel>();

                        targets = _mapServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, range, client.TamerId);

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    //_logger.Verbose($"Skill Type: {skillType}");

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round(skill.SkillInfo.CastingTime);

                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                    // This packet would send attack skill packet that would make DS consume for each monster (Visual only)
                                    // This packet should be sent only if it is single monster skill
                                    // _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new KillOnSkillPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg).Serialize());

                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }

                            });

                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize());
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new AreaSkillPacket(attackerHandler, client.Partner.HpRate, targetMobs, skillSlot, finalDmg).Serialize());

                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize());

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SkillHitPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg, targetMob.CurrentHpRate).Serialize());


                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new KillOnSkillPacket(
                                        attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_mapServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();
                            SendBattleOffTask(client, attackerHandler);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        // Save cooldown in database if the cooldown is more than 20 seconds
                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 >= 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }

                }
                // MAP SERVER -> ATTACK PLAYER
                else if (_mapServer.GetEnemyByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId) != null)
                {
                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _pvpServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _pvpServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _pvpServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg != 0)
                                {
                                    finalDmg = DebuffReductionDamage(client, finalDmg);
                                }

                                if (finalDmg <= 0) finalDmg = 1;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _pvpServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new CastSkillPacket(
                                    skillSlot,
                                    attackerHandler,
                                    targetHandler
                                ).Serialize()
                            );

                            _pvpServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new AreaSkillPacket(
                                    attackerHandler,
                                    client.Partner.HpRate,
                                    targetMobs,
                                    skillSlot,
                                    finalDmg
                                ).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());


                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _pvpServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new KillOnSkillPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg
                                        ).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_pvpServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }

                }

            }
            else if (client.DungeonMap)
            {
                var areaOfEffect = skill.SkillInfo.AreaOfEffect;
                var range = skill.SkillInfo.Range;
                var targetType = skill.SkillInfo.Target;

                // DUNGEON SERVER -> ATTACK SUMMON
                if (_dungeonServer.GetMobByHandler(targetHandler, client.TamerId, true) != null)
                {
                    //_logger.Verbose($"Using skill on Summon (Dungeon Server)");

                    var targets = new List<SummonMobModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _dungeonServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, true, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _dungeonServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, true, client.TamerId);
                        }

                        targetSummonMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _dungeonServer.GetMobByHandler(targetHandler, client.TamerId, true);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetSummonMobs.Add(mob);
                    }

                    //_logger.Verbose($"Skill Type: {skillType}");

                    if (targetSummonMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetSummonMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetSummonMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetSummonMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetSummonMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetSummonMobs.First().CurrentHP : SummonAoEDamage(client, targetSummonMobs.First(), skill, skillSlot, _configuration);

                            targetSummonMobs.ForEach(targetMob =>
                            {
                                if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize()
                            );

                            _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                new AreaSkillPacket(attackerHandler, client.Partner.HpRate, targetSummonMobs, skillSlot, finalDmg).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetSummonMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _dungeonServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _dungeonServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());
                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                    new KillOnSkillPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_dungeonServer.IsMobsAttacking(client.TamerId, true))
                        {
                            client.Tamer.StopBattle(true);

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }
                }
                // DUNGEON SERVER -> ATTACK MOB
                else
                {
                    // Add silence check before processing any skill in dungeon map
                    if (client?.Partner != null && client.Partner.BuffList != null)
                    {
                        bool hasSilenceDebuff = client.Partner.BuffList.Buffs.Any(b => b.BuffId == 60007);

                        if (hasSilenceDebuff)
                        {
                            return Task.CompletedTask;
                        }
                    }

                    //_logger.Verbose($"Using skill on Mob (Dungeon Server)");

                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _dungeonServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _dungeonServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _dungeonServer.GetMobByHandler(targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    //_logger.Verbose($"Skill Type: {skillType}");

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg != 0)
                                {
                                    finalDmg = DebuffReductionDamage(client, finalDmg);
                                }

                                if (finalDmg <= 0) finalDmg = 1;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _dungeonServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new CastSkillPacket(
                                    skillSlot,
                                    attackerHandler,
                                    targetHandler
                                ).Serialize()
                            );

                            _dungeonServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new AreaSkillPacket(
                                    attackerHandler,
                                    client.Partner.HpRate,
                                    targetMobs,
                                    skillSlot,
                                    finalDmg
                                ).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                                finalDmg = DebuffReductionDamage(client, finalDmg);

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                    new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize());

                                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                    new SkillHitPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg, targetMob.CurrentHpRate).Serialize());

                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _dungeonServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new KillOnSkillPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg
                                        ).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_dungeonServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }
                }
            }
            else if (client.EventMap)
            {
                var areaOfEffect = skill.SkillInfo.AreaOfEffect;
                var range = skill.SkillInfo.Range;
                var targetType = skill.SkillInfo.Target;

                // EVENT SERVER -> ATTACK SUMMON
                if (_eventServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, true, client.TamerId) != null)
                {
                    var targets = new List<SummonMobModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _eventServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, true, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _eventServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, true, client.TamerId);
                        }

                        targetSummonMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _eventServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, true, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetSummonMobs.Add(mob);
                    }

                    if (targetSummonMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetSummonMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetSummonMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetSummonMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetSummonMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetSummonMobs.First().CurrentHP : SummonAoEDamage(client, targetSummonMobs.First(), skill, skillSlot, _configuration);

                            targetSummonMobs.ForEach(targetMob =>
                            {
                                if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize());

                            _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                new AreaSkillPacket(attackerHandler, client.Partner.HpRate, targetSummonMobs, skillSlot, finalDmg).Serialize());
                        }
                        else
                        {
                            var targetMob = targetSummonMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _eventServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _eventServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());
                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                                    new KillOnSkillPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_eventServer.IsMobsAttacking(client.TamerId, true))
                        {
                            client.Tamer.StopBattle(true);

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }
                }
                // EVENT SERVER -> ATTACK MOB
                else if (_eventServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId) != null)
                {
                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _eventServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _eventServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _eventServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg != 0)
                                {
                                    finalDmg = DebuffReductionDamage(client, finalDmg);
                                }

                                if (finalDmg <= 0) finalDmg = 1;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _eventServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new CastSkillPacket(
                                    skillSlot,
                                    attackerHandler,
                                    targetHandler
                                ).Serialize()
                            );

                            _eventServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new AreaSkillPacket(
                                    attackerHandler,
                                    client.Partner.HpRate,
                                    targetMobs,
                                    skillSlot,
                                    finalDmg
                                ).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _eventServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _eventServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());


                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _eventServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new KillOnSkillPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg
                                        ).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_eventServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();

                            SendBattleOffTask(client, attackerHandler, true);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }
                }
            }
            else
            {
                var areaOfEffect = skill.SkillInfo.AreaOfEffect;
                var range = skill.SkillInfo.Range;
                var targetType = skill.SkillInfo.Target;

                if (_mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, true, client.TamerId) != null)
                {
                    //_logger.Debug($"Using skill on Summon (Map Server)");

                    var targets = new List<SummonMobModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _mapServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, true, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _mapServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, true, client.TamerId);
                        }

                        targetSummonMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, true, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetSummonMobs.Add(mob);
                    }

                    //_logger.Verbose($"Skill Type: {skillType}");

                    if (targetSummonMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetSummonMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round((float)skill.SkillInfo.CastingTime);
                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetSummonMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetSummonMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetSummonMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = 0;

                            finalDmg = client.Tamer.GodMode ? targetSummonMobs.First().CurrentHP : SummonAoEDamage(client, targetSummonMobs.First(), skill, skillSlot, _configuration);

                            targetSummonMobs.ForEach(targetMob =>
                            {
                                if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");
                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }
                            });

                            _mapServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new CastSkillPacket(
                                    skillSlot,
                                    attackerHandler,
                                    targetHandler
                                ).Serialize()
                            );

                            _mapServer.BroadcastForTamerViewsAndSelf(
                                client.TamerId,
                                new AreaSkillPacket(
                                    attackerHandler,
                                    client.Partner.HpRate,
                                    targetSummonMobs,
                                    skillSlot,
                                    finalDmg
                                ).Serialize()
                            );
                        }
                        else
                        {
                            var targetMob = targetSummonMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = 1;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _mapServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new CastSkillPacket(
                                        skillSlot,
                                        attackerHandler,
                                        targetHandler).Serialize());

                                _mapServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new SkillHitPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg,
                                        targetMob.CurrentHpRate
                                        ).Serialize());

                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _mapServer.BroadcastForTamerViewsAndSelf(
                                    client.TamerId,
                                    new KillOnSkillPacket(
                                        attackerHandler,
                                        targetMob.GeneralHandler,
                                        skillSlot,
                                        finalDmg
                                        ).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_mapServer.IsMobsAttacking(client.TamerId, true))
                        {
                            client.Tamer.StopBattle(true);

                            SendBattleOffTask(client, attackerHandler);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 > 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }
                }
                else
                {
                    //_logger.Debug($"Using skill on Mob (Map Server)");

                    var targetMobs = new List<MobConfigModel>();

                    if (areaOfEffect > 0)
                    {
                        skillType = SkillTypeEnum.TargetArea;

                        var targets = new List<MobConfigModel>();

                        if (targetType == 17)   // Mobs around partner
                        {
                            targets = _mapServer.GetMobsNearbyPartner(client.Partner.Location, areaOfEffect, client.TamerId);
                        }
                        else if (targetType == 18)   // Mobs around mob
                        {
                            targets = _mapServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, areaOfEffect, client.TamerId);
                        }

                        targetMobs.AddRange(targets);
                    }
                    else if (areaOfEffect == 0 && targetType == 80)
                    {
                        skillType = SkillTypeEnum.Implosion;

                        var targets = new List<MobConfigModel>();

                        targets = _mapServer.GetMobsNearbyTargetMob(client.Partner.Location.MapId, targetHandler, range, client.TamerId);

                        targetMobs.AddRange(targets);
                    }
                    else
                    {
                        skillType = SkillTypeEnum.Single;

                        var mob = _mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                        if (mob == null)
                            return Task.CompletedTask;

                        targetMobs.Add(mob);
                    }

                    //_logger.Debug($"Skill Type: {skillType}");

                    if (targetMobs.Any())
                    {
                        if (skillType == SkillTypeEnum.Single && !targetMobs.First().Alive)
                            return Task.CompletedTask;

                        client.Partner.ReceiveDamage(skill.SkillInfo.HPUsage);
                        client.Partner.UseDs(skill.SkillInfo.DSUsage);

                        var castingTime = (int)Math.Round(skill.SkillInfo.CastingTime);

                        if (castingTime <= 0) castingTime = 2000;

                        client.Partner.SetEndCasting(castingTime);

                        targetMobs.ForEach(targetMob =>
                        {
                            //_logger.Verbose($"Character {client.Tamer.Id} engaged {targetMob.Id} - {targetMob.Name} with skill {skill.SkillId}.");
                        });

                        if (!client.Tamer.InBattle)
                        {
                            client.Tamer.SetHidden(false);
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattleWithSkill(targetMobs, skillType);
                        }
                        else
                        {
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTargetWithSkill(targetMobs, skillType);
                        }

                        if (skillType != SkillTypeEnum.Single)
                        {
                            var finalDmg = client.Tamer.GodMode ? targetMobs.First().CurrentHP : AoeDamage(client, targetMobs.First(), skill, skillSlot, _configuration);

                            targetMobs.ForEach(targetMob =>
                            {
                                if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                                if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                                if (!targetMob.InBattle)
                                {
                                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                    targetMob.StartBattle(client.Tamer);
                                }
                                else
                                {
                                    targetMob.AddTarget(client.Tamer);
                                }

                                var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                                if (newHp > 0)
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");
                                }
                                else
                                {
                                    //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                    // This packet would send attack skill packet that would make DS consume for each monster (Visual only)
                                    // This packet should be sent only if it is single monster skill
                                    // _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new KillOnSkillPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg).Serialize());

                                    targetMob?.Die();
                                    client.Tamer.RemoveTarget(targetMob);
                                    client.Tamer.CleanupDeadTargetsAndBattleState();
                                    client.Partner.SetEndCasting(0);
                                }

                            });

                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize());
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new AreaSkillPacket(attackerHandler, client.Partner.HpRate, targetMobs, skillSlot, finalDmg).Serialize());

                        }
                        else
                        {
                            var targetMob = targetMobs.First();

                            if (!targetMob.InBattle)
                            {
                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                                targetMob.StartBattle(client.Tamer);
                            }
                            else
                            {
                                targetMob.AddTarget(client.Tamer);
                            }

                            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : CalculateDamageOrHeal(client, targetMob, skill, _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skill.SkillId), skillSlot);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            if (finalDmg <= 0) finalDmg = client.Tamer.Partner.AT;
                            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

                            var newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

                            if (newHp > 0)
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} damage with skill {skill.SkillId} in mob {targetMob?.Id} - {targetMob?.Name}.");

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new CastSkillPacket(skillSlot, attackerHandler, targetHandler).Serialize());

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SkillHitPacket(attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg, targetMob.CurrentHpRate).Serialize());
                            }
                            else
                            {
                                //_logger.Verbose($"Partner {client.Partner.Id} killed mob {targetMob?.Id} - {targetMob?.Name} with {finalDmg} skill {skill.Id} damage.");

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new KillOnSkillPacket(
                                        attackerHandler, targetMob.GeneralHandler, skillSlot, finalDmg).Serialize());

                                targetMob?.Die();
                                client.Tamer.RemoveTarget(targetMob);
                                client.Tamer.CleanupDeadTargetsAndBattleState();
                                client.Partner.SetEndCasting(0);
                            }
                        }

                        if (!_mapServer.IsMobsAttacking(client.TamerId))
                        {
                            client.Tamer.StopBattle();
                            SendBattleOffTask(client, attackerHandler);
                        }

                        var evolution = client.Tamer.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                        // Save cooldown in database if the cooldown is more than 20 seconds
                        if (evolution != null && skill.SkillInfo.Cooldown / 1000 >= 20)
                        {
                            evolution.Skills[skillSlot].SetCooldown(skill.SkillInfo.Cooldown / 1000);
                            _sender.Send(new UpdateEvolutionCommand(evolution));
                        }
                    }

                }
            }

            return Task.CompletedTask;
        }

        // -------------------------------------------------------------------------------------

        public async Task SendBattleOffTask(GameClient client, int attackerHandler)
        {
            await Task.Run(async () =>
            {
                Thread.Sleep(4000);

                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOffPacket(attackerHandler).Serialize());
            });
        }

        public async Task SendBattleOffTask(GameClient client, int attackerHandler, bool dungeon)
        {
            await Task.Run(async () =>
            {
                Thread.Sleep(4000);

                _dungeonServer.BroadcastForTamerViewsAndSelf(
                        client.TamerId,
                        new SetCombatOffPacket(attackerHandler).Serialize()
                    );
            });
        }

        // -------------------------------------------------------------------------------------

        private static int DebuffReductionDamage(GameClient client, int finalDmg)
        {
            if (client.Tamer.Partner.DebuffList.ActiveDebuffReductionDamage())
            {
                var debuffInfo = client.Tamer.Partner.DebuffList.ActiveBuffs.Where(buff => buff.BuffInfo.SkillInfo.Apply.Any(apply => apply.Attribute == Commons.Enums.SkillCodeApplyAttributeEnum.AttackPowerDown)).ToList();

                var totalValue = 0;
                var SomaValue = 0;

                foreach (var debuff in debuffInfo)
                {
                    foreach (var apply in debuff.BuffInfo.SkillInfo.Apply)
                    {

                        switch (apply.Type)
                        {
                            case SkillCodeApplyTypeEnum.Default:
                                totalValue += apply.Value;
                                break;

                            case SkillCodeApplyTypeEnum.AlsoPercent:
                            case SkillCodeApplyTypeEnum.Percent:
                                {

                                    SomaValue += apply.Value + (debuff.TypeN) * apply.IncreaseValue;

                                    double fatorReducao = SomaValue / 100;

                                    // Calculando o novo finalDmg após a redução
                                    finalDmg -= (int)(finalDmg * fatorReducao);

                                }
                                break;

                            case SkillCodeApplyTypeEnum.Unknown200:
                                {

                                    SomaValue += apply.AdditionalValue;

                                    double fatorReducao = SomaValue / 100.0;

                                    // Calculando o novo finalDmg após a redução
                                    finalDmg -= (int)(finalDmg * fatorReducao);

                                }
                                break;

                        }
                        break;

                    }
                }
            }

            return finalDmg;
        }

        // -------------------------------------------------------------------------------------

        private int CalculateDamageOrHeal(GameClient client, MobConfigModel? targetMob, DigimonSkillAssetModel? targetSkill, SkillCodeAssetModel? skill, byte skillSlot)
    {
        //Console.WriteLine($"[CALC DAMAGE] === INICIO CÁLCULO DAÑO HABILIDAD ===");
        //Console.WriteLine($"[CALC DAMAGE] SkillCode: {skill?.SkillCode}, Slot: {skillSlot}, Target: {targetMob?.Name ?? "Null"}, Digimon Type: {client.Partner.CurrentType}");

        // Log de buffs activos al inicio (CRÍTICO para depurar Master)
        var allBuffs = client.Tamer.Partner.BuffList?.Buffs ?? new List<DigimonBuffModel>();
        //Console.WriteLine($"[CALC DAMAGE BUFFS] Número de buffs activos: {allBuffs.Count}");
        foreach (var b in allBuffs)
        {
            //Console.WriteLine($"[CALC DAMAGE BUFFS] Buff ID: {b.BuffId}, SkillId: {b.SkillId}, EndDate: {b.EndDate}, Apply existe: {(b.Apply != null ? "Sí (Attr: {b.Apply.Attribute}, Value: {b.Apply.Value}, AddValue: {b.Apply.AdditionalValue})" : "NO")}");
        }

        var skillValue = skill.Apply
            .Where(x => x.Type > 0)
            .Take(3)
            .ToList();

        //Console.WriteLine($"[CALC DAMAGE] Applies válidos encontrados: {skillValue.Count}");
        for (int i = 0; i < skillValue.Count; i++)
        {
            //Console.WriteLine($"[CALC DAMAGE] Apply[{i}]: Attribute={skillValue[i].Attribute}, Value={skillValue[i].Value}, AddValue={skillValue[i].AdditionalValue}, Chance={skillValue[i].Chance}");
        }

        var partnerEvolution = client.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Partner.CurrentType);
        //Console.WriteLine($"[CALC DAMAGE] Evolución encontrada: Type={partnerEvolution?.Type}, Nivel slot {skillSlot}: {partnerEvolution?.Skills[skillSlot].CurrentLevel}");

        double f1BaseDamage = (skillValue[0].Value) + ((partnerEvolution.Skills[skillSlot].CurrentLevel) * skillValue[0].IncreaseValue);
        //Console.WriteLine($"[CALC DAMAGE] f1BaseDamage: {skillValue[0].Value} + (Lvl {partnerEvolution.Skills[skillSlot].CurrentLevel} * {skillValue[0].IncreaseValue}) = {f1BaseDamage}");

        var buff = _assets.BuffInfo.FirstOrDefault(x => x.SkillCode == skill.SkillCode);
        //Console.WriteLine($"[CALC DAMAGE] Búsqueda BuffInfo por SkillCode {skill.SkillCode}: {(buff != null ? $"Sí (ID: {buff.BuffId}, Name: {buff.Name})" : "No encontrado")}");

        int skillDuration = GetDurationBySkillId((int)skill.SkillCode);
        var durationBuff = UtilitiesFunctions.RemainingTimeSeconds(skillDuration);
        //Console.WriteLine($"[CALC DAMAGE] SkillDuration: {skillDuration}s, DurationBuff: {durationBuff}s");

        double SkillFactor = 0;
        int clonDamage = 0;
        var attributeMultiplier = 0.00;
        var elementMultiplier = 0.00;

        // =====================================================
        // SCD (SKILL CRITICAL DAMAGE) CALCULATION
        // =====================================================
        var Percentual = (decimal)client.Partner.SCD / 100;
        SkillFactor = (double)Percentual;
        //Console.WriteLine($"[CALC DAMAGE] SCD Value: {client.Partner.SCD} → SkillFactor: {SkillFactor:P2}");

        // =====================================================
        // CLON DAMAGE CALCULATION
        // =====================================================
        double clonPercent = client.Tamer.Partner.Digiclone.ATValue / 100.0;
        int cloValue = (int)(client.Tamer.Partner.BaseStatus.ATValue * clonPercent);
        //Console.WriteLine($"[CALC DAMAGE] Clon AT Value: {client.Tamer.Partner.Digiclone.ATValue} → Clon Percent: {clonPercent:P2}");

        double factorFromPF = 144.0 / client.Tamer.Partner.Digiclone.ATValue;
        double cloneFactor = Math.Round(1.0 + (0.43 / factorFromPF), 2);
        //Console.WriteLine($"[CALC DAMAGE] Clone Factor: {cloneFactor} (PF: {factorFromPF})");

        // =====================================================
        // BASE DAMAGE WITH CLONE FACTOR
        // =====================================================
        double originalBaseDamage = f1BaseDamage;
        f1BaseDamage = Math.Floor(f1BaseDamage * cloneFactor);
        //Console.WriteLine($"[CALC DAMAGE] f1BaseDamage con Clone: {originalBaseDamage} * {cloneFactor} = {f1BaseDamage}");

        // =====================================================
        // SCD BONUS DAMAGE
        // =====================================================
        double addedf1Damage = Math.Floor(f1BaseDamage * SkillFactor / 100.0);
        //Console.WriteLine($"[CALC DAMAGE] SCD Bonus: {f1BaseDamage} * {SkillFactor:P2} = +{addedf1Damage}");

        // =====================================================
        // TOTAL BASE DAMAGE (AT + SKD)
        // =====================================================
        int baseDamage = (int)Math.Floor(f1BaseDamage + addedf1Damage + client.Tamer.Partner.AT + client.Tamer.Partner.SKD);
        //Console.WriteLine($"[CALC DAMAGE] BaseDamage final: {f1BaseDamage} + {addedf1Damage} (SCD) + {client.Tamer.Partner.AT} (AT) + {client.Tamer.Partner.SKD} (SKD) = {baseDamage}");

        // =====================================================
        // CLON DAMAGE VERIFICATION
        // =====================================================
        if (client.Tamer.Partner.Digiclone.ATLevel > 0)
        {
            clonDamage = (int)(f1BaseDamage * 0.301);
            //Console.WriteLine($"[CALC DAMAGE] ClonDamage: {f1BaseDamage} * 0.301 = +{clonDamage} (ATLevel: {client.Tamer.Partner.Digiclone.ATLevel})");
        }
        else
        {
            clonDamage = 0;
            //Console.WriteLine($"[CALC DAMAGE] No ClonDamage (ATLevel: 0)");
        }

        // =====================================================
        // ATTRIBUTE MULTIPLIER CALCULATION
        // =====================================================
             // =====================================================
        // ATTRIBUTE MULTIPLIER CALCULATION
        // =====================================================
        var attackerAttr = client.Tamer.Partner.BaseInfo.Attribute;
        var targetAttr   = targetMob.Attribute;

        //Console.WriteLine($"[CALC DAMAGE] Attribute Check: Atacante {attackerAttr} vs Target {targetAttr}");

        attributeMultiplier = 0.0;

        // Caso especial: Unknown vs Unknown → Neutral (0%)
        if (attackerAttr == DigimonAttributeEnum.Unknown &&
            targetAttr   == DigimonAttributeEnum.Unknown)
        {
            attributeMultiplier = 0;
            //Console.WriteLine("[CALC DAMAGE] UNKNOWN vs UNKNOWN → Neutral (0%)");
        }
        else if (attackerAttr == DigimonAttributeEnum.Unknown &&
                attackerAttr.HasAttributeAdvantage(targetAttr))
        {
            // Nuevo diseño: Unknown tiene SIEMPRE +20% contra cualquier otro atributo
            attributeMultiplier = 0.20;
            //Console.WriteLine("[CALC DAMAGE] UNKNOWN advantage → Multiplier fijo = 0.20 (20%)");
        }
        else if (attackerAttr.HasAttributeAdvantage(targetAttr))
        {
            // Atributos normales usan EXP + ATT
            var attExp = client.Tamer.Partner.GetAttributeExperience();
            var attValue = client.Partner.ATT / 100.0;
            var attValuePercent = attValue / 100.0;
            var bonusMax = 1.0;
            var expMax = 10000.0;

            attributeMultiplier = ((bonusMax + attValuePercent) * attExp) / expMax;

            //Console.WriteLine($"[CALC DAMAGE] Attribute Advantage: Exp={attExp}, ATT={attValue}, Multiplier={attributeMultiplier:F4}");
        }
        else if (targetAttr.HasAttributeAdvantage(attackerAttr))
        {
            // Desventaja clásica
            attributeMultiplier = -0.25;
            //Console.WriteLine("[CALC DAMAGE] Attribute Disadvantage → Multiplier = -0.25");
        }
        else
        {
            // Neutral
            attributeMultiplier = 0;
            //Console.WriteLine("[CALC DAMAGE] Attribute Neutral → Multiplier = 0");
        }

        // ====================================================
        // ELEMENT MULTIPLIER CALCULATION
        // =====================================================
        //Console.WriteLine($"[CALC DAMAGE] Element Check: Atacante {client.Tamer.Partner.BaseInfo.Element} vs Target {targetMob?.Element}");
        if (client.Tamer.Partner.BaseInfo.Element.HasElementAdvantage(targetMob.Element))
        {
            var elementValue = client.Tamer.Partner.GetElementExperience();
            var bonusMax = 1;
            var expMax = 10000;

            elementMultiplier = (bonusMax * elementValue) / expMax;
            //Console.WriteLine($"[CALC DAMAGE] Element Advantage: Exp={elementValue}, Multiplier={elementMultiplier:F4}");
        }
        else if (targetMob.Element.HasElementAdvantage(client.Tamer.Partner.BaseInfo.Element))
        {
            elementMultiplier -= 0.25;
            //Console.WriteLine($"[CALC DAMAGE] Element Disadvantage: Multiplier=-0.25");
        }
        else
        {
            //Console.WriteLine($"[CALC DAMAGE] Element Neutral: Multiplier=0");
        }

        // =====================================================
        // ACTIVATION CHANCE CALCULATION
        // =====================================================
        var activationChance = 0.0;
        //Console.WriteLine($"[CALC DAMAGE] Activation Chance inicial: {activationChance}");
        foreach (var skillValueIndex in new[] { 1, 2 })
        {
            if (skillValue.Count <= skillValueIndex) 
            {
                //Console.WriteLine($"[CALC DAMAGE] Skip índice {skillValueIndex} (no existe)");
                continue;
            }

            var currentLevel = partnerEvolution.Skills[skillSlot].CurrentLevel;
            var thisApply = skillValue[skillValueIndex];
            //Console.WriteLine($"[CALC DAMAGE] Procesando Apply[{skillValueIndex}]: Attr={thisApply.Attribute}, Chance={thisApply.Chance}, Lvl={currentLevel}");

            if ((int)thisApply.Attribute != 39)
            {
                activationChance += thisApply.Chance + currentLevel * 43;
                //Console.WriteLine($"[CALC DAMAGE] Attr != 39 → +{thisApply.Chance + currentLevel * 43}");
            }
            else
            {
                activationChance += thisApply.Chance + currentLevel * 42;
                //Console.WriteLine($"[CALC DAMAGE] Attr == 39 → +{thisApply.Chance + currentLevel * 42}");
            }

            if ((int)thisApply.Attribute != 37 && (int)thisApply.Attribute != 38)
            {
                durationBuff += currentLevel;
                skillDuration += currentLevel + 2;
                //Console.WriteLine($"[CALC DAMAGE] Actualizando durations: durationBuff +{currentLevel}, skillDuration +{currentLevel + 2}");
            }
        }
        //Console.WriteLine($"[CALC DAMAGE] Activation Chance final: {activationChance}%");


        // =====================================================
        // DIGIVICE ATTRIBUTE + ELEMENT BONUS
        // =====================================================
        var (digiviceAttributeBonus, digiviceElementBonus) = 
            CharacterModel.GetDigiviceAttributeAndElementBonus(client);

        double digiviceAttrMult = digiviceAttributeBonus / 100.0;
        double digiviceElemMult = digiviceElementBonus   / 100.0;

        attributeMultiplier += digiviceAttrMult;
        elementMultiplier   += digiviceElemMult;

        //Console.WriteLine($"[CALC DAMAGE] DigiviceBonus: Attr={digiviceAttributeBonus}%, Elem={digiviceElementBonus}%");
        //Console.WriteLine($"[CALC DAMAGE] Mult final con Digivice → Attr={attributeMultiplier:F4}, Elem={elementMultiplier:F4}");

        int attributeBonus = (int)Math.Floor(f1BaseDamage * attributeMultiplier);
        int elementBonus   = (int)Math.Floor(f1BaseDamage * elementMultiplier);

       // Console.WriteLine($"[CALC DAMAGE] AttributeBonus: {f1BaseDamage} * {attributeMultiplier:F4} = {attributeBonus}");
        //Console.WriteLine($"[CALC DAMAGE] ElementBonus: {f1BaseDamage} * {elementMultiplier:F4} = {elementBonus}");

        // =====================================================
        // ACTIVATION PROBABILITY CHECK
        // =====================================================
        double activationProbability = activationChance / 100.0;
        Random random = new Random();
        bool isActivated = activationProbability >= 1.0 || random.NextDouble() <= activationProbability;
        //Console.WriteLine($"[CALC DAMAGE] Activation Probability: {activationProbability:P2} → Activated: {isActivated}");

        if (isActivated &&
            ((skillValue.Count > 1 && skillValue[1].Type != 0) ||
            (skillValue.Count > 2 && skillValue[2].Type != 0)))
        {
            //Console.WriteLine($"[CALC DAMAGE] Condiciones para BuffSkill cumplidas → Llamando BuffSkill con durationBuff: {durationBuff}s, skillDuration: {skillDuration}s");
            BuffSkill(client, durationBuff, skillDuration, skillSlot);
        }
        else
        {
            //Console.WriteLine($"[CALC DAMAGE] Condiciones para BuffSkill NO cumplidas → No se aplica buff");
        }

        // =====================================================
        // MEMORY SKILL MASTER BONUS (SkillDamageByAttribute = 43)
        // =====================================================
        int masterSkillDamageBonus = 0;

        int preMasterDamage = baseDamage + clonDamage + attributeBonus + elementBonus;
        //Console.WriteLine($"[CALC DAMAGE MASTER] PreMasterDamage calculado: {preMasterDamage}");

        var activeBuffs = client.Tamer.Partner.BuffList?.Buffs;
        if (activeBuffs != null && activeBuffs.Count > 0)
        {
            //Console.WriteLine($"[CALC DAMAGE MASTER] Procesando {activeBuffs.Count} buffs activos");
            foreach (var activeBuff in activeBuffs)
            {
                if (activeBuff.Apply == null)
                {
                    //Console.WriteLine($"[CALC DAMAGE MASTER] Buff {activeBuff.BuffId} NO tiene Apply guardado.");
                    continue;
                }

                //Console.WriteLine($"[CALC DAMAGE MASTER] Buff {activeBuff.BuffId}: Attr={activeBuff.Apply.Attribute}, Value={activeBuff.Apply.Value}, AddValue={activeBuff.Apply.AdditionalValue}");

                if (activeBuff.Apply.Attribute == SkillCodeApplyAttributeEnum.SkillDamageByAttribute) // 43
                {
                    int requiredAttr = activeBuff.Apply.Value;
                    int currentAttr = (int)client.Tamer.Partner.BaseInfo.Attribute;

                    //Console.WriteLine($"[CALC DAMAGE MASTER] Required Attr: {requiredAttr}, Current Attr: {currentAttr}");

                    if (requiredAttr == currentAttr)
                    {
                        double percent = activeBuff.Apply.AdditionalValue / 100.0;
                        int bonusAdded = (int)Math.Floor(preMasterDamage * percent);
                        masterSkillDamageBonus += bonusAdded;
                        //Console.WriteLine($"[CALC DAMAGE MASTER] Añadiendo {bonusAdded} ({percent:P0}) → Total Master Bonus: {masterSkillDamageBonus}");
                    }
                    else
                    {
                        //Console.WriteLine($"[CALC DAMAGE MASTER] Atributo NO coincide → Skip");
                    }
                }
                else
                {
                    //Console.WriteLine($"[CALC DAMAGE MASTER] Buff {activeBuff.BuffId} no es SkillDamageByAttribute → Skip");
                }
            }
        }
        else
        {
            //Console.WriteLine($"[CALC DAMAGE MASTER] No hay buffs activos o lista null → Master Bonus = 0");
        }

        //Console.WriteLine($"[CALC DAMAGE MASTER] MasterSkillDamageBonus final: {masterSkillDamageBonus}");

        // =====================================================
        // FINAL DAMAGE CALCULATION
        // =====================================================
        int totalDamage = preMasterDamage + masterSkillDamageBonus;
        //Console.WriteLine($"[CALC DAMAGE] Final Calculation: {baseDamage} (Base) + {clonDamage} (Clon) + {attributeBonus} (Attr) + {elementBonus} (Elem) + {masterSkillDamageBonus} (Master) = {totalDamage}");
        //Console.WriteLine($"[CALC DAMAGE] === FIN CÁLCULO DAÑO HABILIDAD ===\n");

        return totalDamage;
    }

        private int CalculateDamageOrHeal(GameClient client, SummonMobModel? targetMob, DigimonSkillAssetModel? targetSkill, SkillCodeAssetModel? skill, byte skillSlot)
        {
            var SkillValue = skill.Apply.FirstOrDefault(x => x.Type > 0);

            double f1BaseDamage = (SkillValue.Value) + ((client.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Partner.CurrentType).Skills[skillSlot].CurrentLevel) * SkillValue.IncreaseValue);

            double SkillFactor = 0;
            int clonDamage = 0;
            var attributeMultiplier = 0.00;
            var elementMultiplier = 0.00;

            var Percentual = (decimal)client.Partner.SCD / 100;
            SkillFactor = (double)Percentual;

            // -- CLON -------------------------------------------------------------------

            double clonPercent = client.Tamer.Partner.Digiclone.ATValue / 100.0;
            int cloValue = (int)(client.Tamer.Partner.BaseStatus.ATValue * clonPercent);

            double factorFromPF = 144.0 / client.Tamer.Partner.Digiclone.ATValue;
            double cloneFactor = Math.Round(1.0 + (0.43 / factorFromPF), 2);

            // ---------------------------------------------------------------------------

            f1BaseDamage = Math.Floor(f1BaseDamage * cloneFactor);

            double addedf1Damage = Math.Floor(f1BaseDamage * SkillFactor / 100.0);

            // ---------------------------------------------------------------------------

            int baseDamage = (int)Math.Floor(f1BaseDamage + addedf1Damage + client.Tamer.Partner.AT + client.Tamer.Partner.SKD);

            // clon Verification
            if (client.Tamer.Partner.Digiclone.ATLevel > 0)
                clonDamage = (int)(f1BaseDamage * 0.301);
            else
                clonDamage = 0;

            // =====================================================
            // ATTRIBUTE MULTIPLIER CALCULATION
            // =====================================================
            var attackerAttr = client.Tamer.Partner.BaseInfo.Attribute;
            var targetAttr   = targetMob.Attribute;

            //Console.WriteLine($"[CALC DAMAGE] Attribute Check: Atacante {attackerAttr} vs Target {targetAttr}");

            attributeMultiplier = 0.0;

            // Caso especial: Unknown vs Unknown → Neutral (0%)
            if (attackerAttr == DigimonAttributeEnum.Unknown &&
                targetAttr   == DigimonAttributeEnum.Unknown)
            {
                attributeMultiplier = 0;
                //Console.WriteLine("[CALC DAMAGE] UNKNOWN vs UNKNOWN → Neutral (0%)");
            }
            else if (attackerAttr == DigimonAttributeEnum.Unknown &&
                    attackerAttr.HasAttributeAdvantage(targetAttr))
            {
                // Nuevo diseño: Unknown tiene SIEMPRE +20% contra cualquier otro atributo
                attributeMultiplier = 0.20;
                //Console.WriteLine("[CALC DAMAGE] UNKNOWN advantage → Multiplier fijo = 0.20 (20%)");
            }
            else if (attackerAttr.HasAttributeAdvantage(targetAttr))
            {
                // Atributos normales usan EXP + ATT
                var attExp = client.Tamer.Partner.GetAttributeExperience();
                var attValue = client.Partner.ATT / 100.0;
                var attValuePercent = attValue / 100.0;
                var bonusMax = 1.0;
                var expMax = 10000.0;

                attributeMultiplier = ((bonusMax + attValuePercent) * attExp) / expMax;

                //Console.WriteLine($"[CALC DAMAGE] Attribute Advantage: Exp={attExp}, ATT={attValue}, Multiplier={attributeMultiplier:F4}");
            }
            else if (targetAttr.HasAttributeAdvantage(attackerAttr))
            {
                // Desventaja clásica
                attributeMultiplier = -0.25;
                //Console.WriteLine("[CALC DAMAGE] Attribute Disadvantage → Multiplier = -0.25");
            }
            else
            {
                // Neutral
                attributeMultiplier = 0;
                //Console.WriteLine("[CALC DAMAGE] Attribute Neutral → Multiplier = 0");
            }


            // Element
            if (client.Tamer.Partner.BaseInfo.Element.HasElementAdvantage(targetMob.Element))
            {
                var elementValue = client.Tamer.Partner.GetElementExperience();
                var bonusMax = 1;
                var expMax = 10000;

                elementMultiplier = (bonusMax * elementValue) / expMax;
            }
            else if (targetMob.Element.HasElementAdvantage(client.Tamer.Partner.BaseInfo.Element))
            {
                elementMultiplier = -0.25;
            }

            // ---------------------------------------------------------------------------

            int attributeBonus = (int)Math.Floor(f1BaseDamage * attributeMultiplier);
            int elementBonus = (int)Math.Floor(f1BaseDamage * elementMultiplier);

            int totalDamage = baseDamage + clonDamage + attributeBonus + elementBonus;

            return totalDamage;
        }

        private int CalculateDamageOrHealPlayer(GameClient client, DigimonModel? targetMob, DigimonSkillAssetModel? targetSkill, SkillCodeAssetModel? skill, byte skillSlot)
        {

            var SkillValue = skill.Apply.FirstOrDefault(x => x.Type > 0);

            double f1BaseDamage = (SkillValue.Value) + ((client.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Partner.CurrentType).Skills[skillSlot].CurrentLevel) * SkillValue.IncreaseValue);
            double SkillFactor = 0;
            double MultiplierAttribute = 0;

            var Percentual = (decimal)client.Partner.SCD / 100;
            SkillFactor = (double)Percentual; // AttributeMultiplier

            double factorFromPF = 144.0 / client.Tamer.Partner.Digiclone.ATValue;

            double cloneFactor = Math.Round(1.0 + (0.43 / factorFromPF), 2);
            f1BaseDamage = Math.Floor(f1BaseDamage * cloneFactor);

            double addedf1Damage = Math.Floor(f1BaseDamage * SkillFactor / 100.0);

            var attributeVantage = client.Tamer.Partner.BaseInfo.Attribute.HasAttributeAdvantage(targetMob.BaseInfo.Attribute);
            var elementVantage = client.Tamer.Partner.BaseInfo.Element.HasElementAdvantage(targetMob.BaseInfo.Element);

            var Damage = (int)Math.Floor(f1BaseDamage + addedf1Damage + (client.Tamer.Partner.AT / targetMob.DE) + client.Tamer.Partner.SKD);

            if (client.Partner.AttributeExperience.CurrentAttributeExperience && attributeVantage)
            {

                MultiplierAttribute = (2 + ((client.Partner.ATT) / 200.0));
                Random random = new Random();

                // Gere um valor aleatório entre 0% e 5% a mais do valor original
                double percentagemBonus = random.NextDouble() * 0.05;

                // Calcule o valor final com o bônus
                return (int)((int)Math.Floor(MultiplierAttribute * Damage) * (1.0 + percentagemBonus));

            }
            else if (client.Partner.AttributeExperience.CurrentElementExperience && elementVantage)
            {
                MultiplierAttribute = 2;

                Random random = new Random();

                // Gere um valor aleatório entre 0% e 5% a mais do valor original
                double percentagemBonus = random.NextDouble() * 0.05;

                // Calcule o valor final com o bônus
                return (int)((int)Math.Floor(MultiplierAttribute * Damage) * (1.0 + percentagemBonus));
            }
            else
            {
                Random random = new Random();

                // Gere um valor aleatório entre 0% e 5% a mais do valor original
                double percentagemBonus = random.NextDouble() * 0.05;

                // Calcule o valor final com o bônus
                return (int)(Damage * (1.0 + percentagemBonus));


            }

        }

        // -------------------------------------------------------------------------------------

        private int AoeDamage(GameClient client, MobConfigModel? targetMob, DigimonSkillAssetModel? targetSkill, byte skillSlot, IConfiguration configuration)
        {
            var skillCode = _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == targetSkill.SkillId);
            var skill = _assets.DigimonSkillInfo.FirstOrDefault(x => x.Type == client.Partner.CurrentType && x.Slot == skillSlot);
            var skillValue = skillCode?.Apply.FirstOrDefault(x => x.Type > 0);

            double f1BaseDamage = skillValue.Value + (client.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Partner.CurrentType).Skills[skillSlot].CurrentLevel * skillValue.IncreaseValue);

            double SkillFactor = 0;
            int clonDamage = 0;
            var attributeMultiplier = 0.00;
            var elementMultiplier = 0.00;

            var Percentual = (decimal)client.Partner.SCD / 100;
            SkillFactor = (double)Percentual;

            // -- CLON -------------------------------------------------------------------

            double factorFromPF = 144.0 / client.Tamer.Partner.Digiclone.ATValue;
            double cloneFactor = Math.Round(1.0 + (0.43 / factorFromPF), 2);

            f1BaseDamage = Math.Floor(f1BaseDamage * cloneFactor);

            double addedf1Damage = Math.Floor(f1BaseDamage * SkillFactor / 100.0);

            // ---------------------------------------------------------------------------

            int baseDamage = (int)Math.Floor(f1BaseDamage + addedf1Damage + client.Tamer.Partner.AT + client.Tamer.Partner.SKD);

            // clon Verification
            if (client.Tamer.Partner.Digiclone.ATLevel > 0)
                clonDamage = (int)(f1BaseDamage * 0.301);
            else
                clonDamage = 0;

            // Attribute
            if (client.Tamer.Partner.BaseInfo.Attribute.HasAttributeAdvantage(targetMob.Attribute))
            {
                var attExp = client.Tamer.Partner.GetAttributeExperience();
                var attValue = client.Partner.ATT / 100.0;
                var attValuePercent = attValue / 100.0;
                var bonusMax = 1;
                var expMax = 10000;

                attributeMultiplier = ((bonusMax + attValuePercent) * attExp) / expMax;
            }
            else if (targetMob.Attribute.HasAttributeAdvantage(client.Tamer.Partner.BaseInfo.Attribute))
            {
                attributeMultiplier = -0.25;
            }

            // Element
            if (client.Tamer.Partner.BaseInfo.Element.HasElementAdvantage(targetMob.Element))
            {
                var elementValue = client.Tamer.Partner.GetElementExperience();
                var bonusMax = 1;
                var expMax = 10000;

                elementMultiplier = (bonusMax * elementValue) / expMax;
            }
            else if (targetMob.Element.HasElementAdvantage(client.Tamer.Partner.BaseInfo.Element))
            {
                elementMultiplier = -0.25;
            }

            // ---------------------------------------------------------------------------

            int attributeBonus = (int)Math.Floor(f1BaseDamage * attributeMultiplier);
            int elementBonus = (int)Math.Floor(f1BaseDamage * elementMultiplier);

            int totalDamage = baseDamage + clonDamage + attributeBonus + elementBonus;

            ////_logger.Information($"Skill Damage: {f1BaseDamage} | Att Damage: {addedf1Damage} | Clon Damage: {clonDamage}");
            ////_logger.Information($"Partner.AT: {client.Tamer.Partner.AT} | Partner.SKD: {client.Tamer.Partner.SKD}");
            ////_logger.Information($"Attribute Damage: {attributeBonus} | Element Damage: {elementBonus}");
            ////_logger.Information($"Total Area Damage: {totalDamage}\n");

            return totalDamage;
        }

        private int SummonAoEDamage(GameClient client, SummonMobModel? targetMob, DigimonSkillAssetModel? targetSkill, byte skillSlot, IConfiguration configuration)
        {
            var skillCode = _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == targetSkill.SkillId);
            var skill = _assets.DigimonSkillInfo.FirstOrDefault(x => x.Type == client.Partner.CurrentType && x.Slot == skillSlot);
            var skillValue = skillCode?.Apply.FirstOrDefault(x => x.Type > 0);

            double f1BaseDamage = skillValue.Value + (client.Partner.Evolutions.FirstOrDefault(x => x.Type == client.Partner.CurrentType).Skills[skillSlot].CurrentLevel * skillValue.IncreaseValue);

            double SkillFactor = 0;
            int clonDamage = 0;
            var attributeMultiplier = 0.00;
            var elementMultiplier = 0.00;

            var Percentual = (decimal)client.Partner.SCD / 100;
            SkillFactor = (double)Percentual;

            // -- CLON -------------------------------------------------------------------

            double factorFromPF = 144.0 / client.Tamer.Partner.Digiclone.ATValue;
            double cloneFactor = Math.Round(1.0 + (0.43 / factorFromPF), 2);

            f1BaseDamage = Math.Floor(f1BaseDamage * cloneFactor);

            double addedf1Damage = Math.Floor(f1BaseDamage * SkillFactor / 100.0);

            // ---------------------------------------------------------------------------

            int baseDamage = (int)Math.Floor(f1BaseDamage + addedf1Damage + client.Tamer.Partner.AT + client.Tamer.Partner.SKD);

            // clon Verification
            if (client.Tamer.Partner.Digiclone.ATLevel > 0)
                clonDamage = (int)(f1BaseDamage * 0.301);
            else
                clonDamage = 0;

            // Attribute
            if (client.Tamer.Partner.BaseInfo.Attribute.HasAttributeAdvantage(targetMob.Attribute))
            {
                var attExp = client.Tamer.Partner.GetAttributeExperience();
                var attValue = client.Partner.ATT / 100.0;
                var attValuePercent = attValue / 100.0;
                var bonusMax = 1;
                var expMax = 10000;

                attributeMultiplier = ((bonusMax + attValuePercent) * attExp) / expMax;
            }
            else if (targetMob.Attribute.HasAttributeAdvantage(client.Tamer.Partner.BaseInfo.Attribute))
            {
                attributeMultiplier = -0.25;
            }

            // Element
            if (client.Tamer.Partner.BaseInfo.Element.HasElementAdvantage(targetMob.Element))
            {
                var elementValue = client.Tamer.Partner.GetElementExperience();
                var bonusMax = 1;
                var expMax = 10000;

                elementMultiplier = (bonusMax * elementValue) / expMax;
            }
            else if (targetMob.Element.HasElementAdvantage(client.Tamer.Partner.BaseInfo.Element))
            {
                elementMultiplier = -0.25;
            }

            // ---------------------------------------------------------------------------

            int attributeBonus = (int)Math.Floor(f1BaseDamage * attributeMultiplier);
            int elementBonus = (int)Math.Floor(f1BaseDamage * elementMultiplier);

            int totalDamage = baseDamage + clonDamage + attributeBonus + elementBonus;

            return totalDamage;
        }

        // -------------------------------------------------------------------------------------
    private void BuffSkill(GameClient client, int duration, int skillDuration, byte skillSlot)
    {
        //Console.WriteLine($"[BuffSkill] Iniciando para Tamer ID: {client.TamerId}, Digimon Type: {client.Partner.CurrentType}, Slot: {skillSlot}, Duración: {duration}s");

        var skillInfo = _assets.DigimonSkillInfo.FirstOrDefault(x => x.Type == client.Partner.CurrentType && x.Slot == skillSlot);
        //Console.WriteLine($"[BuffSkill] Búsqueda de DigimonSkillInfo → Type: {client.Partner.CurrentType}, Slot: {skillSlot}, Encontrado: {(skillInfo != null ? "Sí" : "No")}");

        if (skillInfo == null)
        {
            //Console.WriteLine($"[BuffSkill] No se encontró DigimonSkillInfo → Saliendo de la función");
            return;
        }

        var skillCode = _assets.SkillCodeInfo.FirstOrDefault(x => x.SkillCode == skillInfo.SkillId);
        //Console.WriteLine($"[BuffSkill] Búsqueda de SkillCode → SkillId: {skillInfo.SkillId}, Encontrado: {(skillCode != null ? "Sí" : "No")}");

        if (skillCode == null)
        {
            //Console.WriteLine($"[BuffSkill] No se encontró SkillCode → Saliendo de la función");
            return;
        }

        var buff = _assets.BuffInfo.FirstOrDefault(x => x.SkillCode == skillCode.SkillCode);
        //Console.WriteLine($"[BuffSkill] Búsqueda de BuffInfo → SkillCode: {skillCode.SkillCode}, Encontrado: {(buff != null ? $"Sí (ID: {buff.BuffId}, Name: {buff.Name})" : "No")}");

        if (buff == null)
        {
            //Console.WriteLine($"[BuffSkill] No se encontró BuffInfo → Cancelando aplicación del buff");
            return;
        }

        //Console.WriteLine($"[BuffSkill] Llamando a ApplyBuffEffects con Buff ID: {buff.BuffId}, SkillCode: {skillCode.SkillCode}, Duración: {duration}s");
        ApplyBuffEffects(client, buff, skillCode, duration, skillDuration, skillSlot);

        //Console.WriteLine($"[BuffSkill] Enviando UpdateDigimonBuffListCommand para la lista de buffs del Digimon");
        _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));

        //Console.WriteLine("[BuffSkill] Función completada exitosamente");
    }

    internal static class BuffAttributeSets
    {
        // No se modifica, ya que es estática y no necesita logs
        public static readonly IReadOnlyList<SkillCodeApplyAttributeEnum> PlayerBuffs = new[]
        {
            SkillCodeApplyAttributeEnum.MS,
            SkillCodeApplyAttributeEnum.SCD,
            SkillCodeApplyAttributeEnum.CC,
            SkillCodeApplyAttributeEnum.AS,
            SkillCodeApplyAttributeEnum.AT,
            SkillCodeApplyAttributeEnum.HP,
            SkillCodeApplyAttributeEnum.DamageShield,
            SkillCodeApplyAttributeEnum.CA,
            SkillCodeApplyAttributeEnum.Unbeatable,
            SkillCodeApplyAttributeEnum.DR,
            SkillCodeApplyAttributeEnum.EV,
            SkillCodeApplyAttributeEnum.SkillDamageByAttribute
        };

        public static readonly IReadOnlyList<SkillCodeApplyAttributeEnum> MobDebuffs = new[]
        {
            SkillCodeApplyAttributeEnum.CrowdControl,
            SkillCodeApplyAttributeEnum.DOT,
            SkillCodeApplyAttributeEnum.DOT2
        };
    }

    private void ApplyBuffEffects(GameClient client, BuffInfoAssetModel buff, SkillCodeAssetModel skillCode, int duration, int skillDuration, byte skillSlot)
    {
        //Console.WriteLine($"[ApplyBuffEffects] Iniciando para Buff ID: {buff.BuffId}, SkillCode: {skillCode.SkillCode}, Duración: {duration}s, Skill Duration: {skillDuration}s, Slot: {skillSlot}");

        Action<long, byte[]> broadcast = client.DungeonMap
            ? (id, data) => _dungeonServer.BroadcastForTamerViewsAndSelf(id, data)
            : (id, data) => _mapServer.BroadcastForTamerViewsAndSelf(id, data);
        //Console.WriteLine($"[ApplyBuffEffects] Broadcast delegado definido para mapa: {(client.DungeonMap ? "Dungeon" : "Map")}");

        // Tomar todos los applies válidos (Type > 0)
        var values = skillCode.Apply.Where(x => x.Type > 0).Take(3).ToList();
        //Console.WriteLine($"[ApplyBuffEffects] Número de applies válidos encontrados: {values.Count}");

        var evolution = client.Partner.Evolutions.First(x => x.Type == client.Partner.CurrentType);
        //Console.WriteLine($"[ApplyBuffEffects] Evolución actual encontrada, Type: {evolution.Type}, Nivel de skill en slot {skillSlot}: {evolution.Skills[skillSlot].CurrentLevel}");

        var mob = client.Tamer.TargetMob;
        //Console.WriteLine($"[ApplyBuffEffects] Target Mob: {(mob != null ? $"ID {mob.Id}, Name {mob.Name}" : "Ninguno")}");

        byte level = evolution.Skills[skillSlot].CurrentLevel;
        //Console.WriteLine($"[ApplyBuffEffects] Nivel de skill calculado: {level}");

        // Recorremos cada apply válido (índices 0..N-1)
        int applyIndex = 0;
        foreach (var apply in values)
        {
            //Console.WriteLine($"[ApplyBuffEffects] Procesando apply índice {applyIndex}: Attribute: {apply.Attribute}, Value: {apply.Value}, AdditionalValue: {apply.AdditionalValue}, IncreaseValue: {apply.IncreaseValue}");

            int finalValue = apply.Value + level * apply.IncreaseValue;
            //Console.WriteLine($"[ApplyBuffEffects] Valor final calculado para apply: {finalValue}");

            // Guardado temporal (mantener compatibilidad con el código existente)
            client.Tamer.Partner.BuffValueFromBuffSkill = finalValue;
            //Console.WriteLine($"[ApplyBuffEffects] BuffValueFromBuffSkill actualizado a {finalValue} en Partner");

            // ===== PLAYER BUFFS =====
            if (BuffAttributeSets.PlayerBuffs.Contains(apply.Attribute))
            {
                //Console.WriteLine($"[ApplyBuffEffects] Apply es un Player Buff válido: {apply.Attribute}");

                var activeBuff = client.Tamer.Partner.BuffList.Buffs.FirstOrDefault(x => x.BuffId == buff.BuffId);
                //Console.WriteLine($"[ApplyBuffEffects] Búsqueda de activeBuff para ID {buff.BuffId}: {(activeBuff != null ? "Encontrado" : "No encontrado")}");

                var digiBuff = DigimonBuffModel.Create(buff.BuffId, buff.SkillId, 0, skillDuration);
                //Console.WriteLine($"[ApplyBuffEffects] Nuevo DigimonBuffModel creado para Buff ID {buff.BuffId}, Skill ID {buff.SkillId}, Duration {skillDuration}s");

                switch (apply.Attribute)
                {
                    case SkillCodeApplyAttributeEnum.DR: // Reflect Damage (mantengo tu lógica)
                        //Console.WriteLine($"[ApplyBuffEffects] Caso DR (Reflect Damage)");
                        if (activeBuff == null && mob != null)
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] ActiveBuff null y mob presente → Aplicando buff");
                            digiBuff.SetBuffInfo(buff);
                            digiBuff.SetApply(apply); // ← AÑADE ESTO
                            //Console.WriteLine($"[ApplyBuffEffects] SetBuffInfo y SetApply llamados para digiBuff");

                            client.Tamer.Partner.BuffList.Add(digiBuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Buff añadido a la lista de buffs del Partner");

                            broadcast(client.TamerId, new SkillBuffPacket(client.Tamer.GeneralHandler, (int)buff.BuffId, 0, duration, (int)skillCode.SkillCode).Serialize());
                            //Console.WriteLine($"[ApplyBuffEffects] Paquete SkillBuffPacket enviado");

                            var reflectInterval = TimeSpan.FromMilliseconds(mob.ASValue);
                            var buffId = digiBuff.BuffId;
                            //Console.WriteLine($"[ApplyBuffEffects] Reflect interval: {reflectInterval}, Buff ID: {buffId}");

                            Task.Run(async () =>
                            {
                                //Console.WriteLine($"[ApplyBuffEffects] Iniciando task async para reflect damage");
                                await Task.Delay(1500);
                                //Console.WriteLine($"[ApplyBuffEffects] Delay de 1500 ms completado en task");

                                for (int t = 0; t < duration; t++)
                                {
                                    //Console.WriteLine($"[ApplyBuffEffects] Iteración {t} en loop de reflect damage");

                                    if (mob == null || !client.Tamer.Partner.BuffList.Buffs.Any(b => b.BuffId == buffId) || mob.CurrentAction != MobActionEnum.Attack)
                                    {
                                        //Console.WriteLine($"[ApplyBuffEffects] Condición para salir del loop cumplida: Mob null o buff no presente o acción no Attack");
                                        RemoveBuffAndNotify(client, digiBuff.BuffId, broadcast);
                                        //Console.WriteLine($"[ApplyBuffEffects] Llamado a RemoveBuffAndNotify");
                                        break;
                                    }

                                    int dmg = mob.ATValue * 3;
                                    //Console.WriteLine($"[ApplyBuffEffects] Daño calculado para reflect: {dmg}");

                                    var newHp = mob.ReceiveDamage(dmg, client.TamerId);
                                    //Console.WriteLine($"[ApplyBuffEffects] Daño aplicado a mob, new HP: {newHp}");

                                    broadcast(client.TamerId, new AddDotDebuffPacket(
                                        client.Tamer.Partner.GeneralHandler, mob.GeneralHandler, digiBuff.BuffId,
                                        mob.CurrentHpRate, dmg, (byte)(newHp > 0 ? 0 : 1)).Serialize());
                                    //Console.WriteLine($"[ApplyBuffEffects] Paquete AddDotDebuffPacket enviado");

                                    if (newHp <= 0)
                                    {
                                        //Console.WriteLine($"[ApplyBuffEffects] Mob muerto por reflect damage");
                                        RemoveBuffAndNotify(client, digiBuff.BuffId, broadcast);
                                        mob.Die();
                                        //Console.WriteLine($"[ApplyBuffEffects] Mob.Die() llamado");
                                        break;
                                    }

                                    await Task.Delay(reflectInterval);
                                    //Console.WriteLine($"[ApplyBuffEffects] Delay de reflect interval completado");
                                }
                                //Console.WriteLine($"[ApplyBuffEffects] Task async para reflect damage completada");
                            });
                        }
                        else
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] No se aplicó DR: ActiveBuff {(activeBuff != null ? "presente" : "null")}, Mob {(mob != null ? "presente" : "null")}");
                        }
                        break;

                    case SkillCodeApplyAttributeEnum.DamageShield:
                        //Console.WriteLine($"[ApplyBuffEffects] Caso DamageShield");
                        if (client.Tamer.Partner.DamageShieldHp <= 0)
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] DamageShieldHp <= 0 → Aplicando buff");
                            digiBuff.SetBuffInfo(buff);
                            digiBuff.SetApply(apply); // ← AÑADE ESTO
                            //Console.WriteLine($"[ApplyBuffEffects] SetBuffInfo y SetApply llamados para digiBuff");

                            client.Tamer.Partner.BuffList.Add(digiBuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Buff añadido a la lista de buffs del Partner");

                            client.Tamer.Partner.DamageShieldHp = finalValue;
                            //Console.WriteLine($"[ApplyBuffEffects] DamageShieldHp actualizado a {finalValue}");

                            broadcast(client.TamerId, new SkillBuffPacket(client.Tamer.GeneralHandler, buff.BuffId, level, duration, (int)skillCode.SkillCode).Serialize());
                            //Console.WriteLine($"[ApplyBuffEffects] Paquete SkillBuffPacket enviado");

                            Task.Run(async () =>
                            {
                                //Console.WriteLine($"[ApplyBuffEffects] Iniciando task async para DamageShield");
                                int remaining = skillDuration;
                                while (remaining > 0)
                                {
                                    //Console.WriteLine($"[ApplyBuffEffects] Iteración en loop de DamageShield, remaining: {remaining}s");

                                    await Task.Delay(1000);
                                    //Console.WriteLine($"[ApplyBuffEffects] Delay de 1000 ms completado");

                                    if (client.Tamer.Partner.DamageShieldHp <= 0)
                                    {
                                        //Console.WriteLine($"[ApplyBuffEffects] DamageShieldHp <= 0 → Limpiando y removiendo buff");
                                        client.Tamer.Partner.DamageShieldHp = 0;
                                        RemoveBuffAndNotify(client, digiBuff.BuffId, broadcast);
                                        break;
                                    }
                                    remaining--;
                                }
                                if (client.Tamer.Partner.DamageShieldHp > 0)
                                {
                                    //Console.WriteLine($"[ApplyBuffEffects] Loop completado, DamageShieldHp > 0 → Limpiando y removiendo buff");
                                    client.Tamer.Partner.DamageShieldHp = 0;
                                    RemoveBuffAndNotify(client, digiBuff.BuffId, broadcast);
                                }
                                //Console.WriteLine($"[ApplyBuffEffects] Task async para DamageShield completada");
                            });
                        }
                        else
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] No se aplicó DamageShield: DamageShieldHp > 0");
                        }
                        break;

                    case SkillCodeApplyAttributeEnum.Unbeatable:
                        //Console.WriteLine($"[ApplyBuffEffects] Caso Unbeatable");
                        if (!client.Tamer.Partner.IsUnbeatable)
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] Partner no es Unbeatable → Aplicando buff");
                            digiBuff.SetBuffInfo(buff);
                            digiBuff.SetApply(apply); // ← AÑADE ESTO
                            //Console.WriteLine($"[ApplyBuffEffects] SetBuffInfo y SetApply llamados para digiBuff");

                            client.Tamer.Partner.BuffList.Add(digiBuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Buff añadido a la lista de buffs del Partner");

                            client.Tamer.Partner.IsUnbeatable = true;
                            //Console.WriteLine($"[ApplyBuffEffects] IsUnbeatable actualizado a true");

                            broadcast(client.TamerId, new SkillBuffPacket(client.Tamer.GeneralHandler, (int)buff.BuffId, level, duration, (int)skillCode.SkillCode).Serialize());
                            //Console.WriteLine($"[ApplyBuffEffects] Paquete SkillBuffPacket enviado");

                            Task.Delay(skillDuration * 1000).ContinueWith(_ =>
                            {
                                //Console.WriteLine($"[ApplyBuffEffects] Task Delay completada → IsUnbeatable actualizado a false");
                                client.Tamer.Partner.IsUnbeatable = false;
                            });
                        }
                        else
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] No se aplicó Unbeatable: Partner ya es Unbeatable");
                        }
                        break;

                    // Resto de buffs normales (AT, HP, AS, etc.)
                    case SkillCodeApplyAttributeEnum.AT:
                    case SkillCodeApplyAttributeEnum.HP:
                    case SkillCodeApplyAttributeEnum.AS:
                    case SkillCodeApplyAttributeEnum.MS:
                    case SkillCodeApplyAttributeEnum.SCD:
                    case SkillCodeApplyAttributeEnum.CC:
                    case SkillCodeApplyAttributeEnum.CA:
                    case SkillCodeApplyAttributeEnum.EV:
                        //Console.WriteLine($"[ApplyBuffEffects] Caso buff normal: {apply.Attribute}");
                        if (activeBuff == null)
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] ActiveBuff null → Aplicando buff");
                            digiBuff.SetBuffInfo(buff);
                            digiBuff.SetApply(apply); // ← AÑADE ESTO
                            //Console.WriteLine($"[ApplyBuffEffects] SetBuffInfo y SetApply llamados para digiBuff");

                            client.Tamer.Partner.BuffList.Add(digiBuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Buff añadido a la lista de buffs del Partner");

                            broadcast(client.TamerId, new SkillBuffPacket(client.Tamer.GeneralHandler, (int)buff.BuffId, level, duration, (int)skillCode.SkillCode).Serialize());
                            //Console.WriteLine($"[ApplyBuffEffects] Paquete SkillBuffPacket enviado");
                        }
                        else
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] ActiveBuff ya presente → No se aplica nuevo buff");
                        }
                        //Console.WriteLine($"[ApplyBuffEffects] Enviando UpdateStatusPacket para Tamer");
                        client.Send(new UpdateStatusPacket(client.Tamer));
                        break;

                    // ===== NUEVO CASE PARA SkillDamageByAttribute (43) =====
                    case SkillCodeApplyAttributeEnum.SkillDamageByAttribute:
                        //Console.WriteLine($"[ApplyBuffEffects] Caso SkillDamageByAttribute (43)");
                        if (activeBuff == null)
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] ActiveBuff null → Aplicando buff");
                            digiBuff.SetBuffInfo(buff);
                            digiBuff.SetApply(apply); 
                            //Console.WriteLine($"[ApplyBuffEffects] SetBuffInfo y SetApply llamados para digiBuff");

                            client.Tamer.Partner.BuffList.Add(digiBuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Buff añadido a la lista de buffs del Partner");

                            broadcast(client.TamerId, new SkillBuffPacket(client.Tamer.GeneralHandler, (int)buff.BuffId, level, duration, (int)skillCode.SkillCode).Serialize());
                            //Console.WriteLine($"[ApplyBuffEffects] Paquete SkillBuffPacket enviado");
                        }
                        else
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] ActiveBuff ya presente → No se aplica nuevo buff");
                        }
                        break;
                }
            }
            // ===== MOB DEBUFFS =====
            else if (BuffAttributeSets.MobDebuffs.Contains(apply.Attribute) && mob != null)
            {
                //Console.WriteLine($"[ApplyBuffEffects] Apply es un Mob Debuff válido: {apply.Attribute}");

                var activeDebuff = mob.DebuffList.Buffs.FirstOrDefault(x => x.BuffId == buff.BuffId);
                //Console.WriteLine($"[ApplyBuffEffects] Búsqueda de activeDebuff para ID {buff.BuffId}: {(activeDebuff != null ? "Encontrado" : "No encontrado")}");

                var mobDebuff = MobDebuffModel.Create(buff.BuffId, (int)skillCode.SkillCode, 0, skillDuration);
                //Console.WriteLine($"[ApplyBuffEffects] Nuevo MobDebuffModel creado para Buff ID {buff.BuffId}, SkillCode {skillCode.SkillCode}, Duration {skillDuration}s");

                mobDebuff.SetBuffInfo(buff);
                //Console.WriteLine($"[ApplyBuffEffects] SetBuffInfo llamado para mobDebuff");

                switch (apply.Attribute)
                {
                    case SkillCodeApplyAttributeEnum.CrowdControl:
                        //Console.WriteLine($"[ApplyBuffEffects] Caso CrowdControl");
                        if (activeDebuff == null)
                        {
                            mob.DebuffList.Buffs.Add(mobDebuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Debuff añadido a la lista de debuffs del Mob");
                        }
                        if (mob.CurrentAction != MobActionEnum.CrowdControl)
                        {
                            mob.UpdateCurrentAction(MobActionEnum.CrowdControl);
                            //Console.WriteLine($"[ApplyBuffEffects] Actualizado CurrentAction del Mob a CrowdControl");
                        }

                        broadcast(client.TamerId, new AddStunDebuffPacket(mob.GeneralHandler, mobDebuff.BuffId, mobDebuff.SkillId, duration).Serialize());
                        //Console.WriteLine($"[ApplyBuffEffects] Paquete AddStunDebuffPacket enviado");
                        break;

                    case SkillCodeApplyAttributeEnum.DOT:
                    case SkillCodeApplyAttributeEnum.DOT2:
                        //Console.WriteLine($"[ApplyBuffEffects] Caso DOT o DOT2");
                        if (finalValue > mob.CurrentHP) finalValue = mob.CurrentHP;
                        //Console.WriteLine($"[ApplyBuffEffects] FinalValue ajustado a {finalValue} (no superior a HP actual del Mob)");

                        if (activeDebuff != null)
                        {
                            activeDebuff.SetDuration(skillDuration, true);
                            activeDebuff.SetEndDate(DateTime.Now.AddSeconds(skillDuration));
                            //Console.WriteLine($"[ApplyBuffEffects] ActiveDebuff presente → Actualizado duración a {skillDuration}s y EndDate");
                        }
                        else
                        {
                            mobDebuff.SetDuration(skillDuration, true);
                            //Console.WriteLine($"[ApplyBuffEffects] SetDuration llamado para mobDebuff a {skillDuration}s");

                            mob.DebuffList.Buffs.Add(mobDebuff);
                            //Console.WriteLine($"[ApplyBuffEffects] Debuff añadido a la lista de debuffs del Mob");
                        }

                        broadcast(client.TamerId, new AddBuffPacket(mob.GeneralHandler, buff, 0, duration).Serialize());
                        //Console.WriteLine($"[ApplyBuffEffects] Paquete AddBuffPacket enviado");

                        Task.Run(async () =>
                        {
                            //Console.WriteLine($"[ApplyBuffEffects] Iniciando task async para DOT");

                            try
                            {
                                await Task.Delay(1000);
                                //Console.WriteLine($"[ApplyBuffEffects] Delay de 1000 ms completado en task DOT");

                                int ticks = Math.Max(1, skillDuration / 2);
                                int tickDmg = finalValue / ticks;
                                //Console.WriteLine($"[ApplyBuffEffects] Ticks calculados: {ticks}, TickDmg: {tickDmg}");

                                if (skillCode.SkillCode == (int)SkillBuffAndDebuffDurationEnum.LilithmonXF1)
                                    tickDmg = 866;
                                //Console.WriteLine($"[ApplyBuffEffects] TickDmg ajustado para LilithmonXF1: {tickDmg}");

                                for (int t = 0; t < ticks; t++)
                                {
                                    //Console.WriteLine($"[ApplyBuffEffects] Iteración {t} en loop de DOT");

                                    if (mob == null || mob.CurrentHP <= 0 || !client.IsConnected) 
                                    {
                                        //Console.WriteLine($"[ApplyBuffEffects] Condición para salir del loop cumplida: Mob null o HP <=0 o cliente desconectado");
                                        break;
                                    }

                                    var newHp = mob.ReceiveDamage(tickDmg, client.TamerId);
                                    //Console.WriteLine($"[ApplyBuffEffects] Daño aplicado a mob, new HP: {newHp}");

                                    broadcast(client.TamerId, new AddDotDebuffPacket(
                                        client.Tamer.Partner.GeneralHandler, mob.GeneralHandler,
                                        mobDebuff.BuffId, mob.CurrentHpRate, tickDmg,
                                        (byte)(newHp > 0 ? 0 : 1)).Serialize());
                                    //Console.WriteLine($"[ApplyBuffEffects] Paquete AddDotDebuffPacket enviado");

                                    if (newHp <= 0)
                                    {
                                        //Console.WriteLine($"[ApplyBuffEffects] Mob muerto por DOT");
                                        mob.Die();
                                        //Console.WriteLine($"[ApplyBuffEffects] Mob.Die() llamado");
                                        break;
                                    }
                                    await Task.Delay(2000);
                                    //Console.WriteLine($"[ApplyBuffEffects] Delay de 2000 ms completado");
                                }

                                if (mob != null && mob.CurrentHP > 0)
                                {
                                    int removed = mob.DebuffList.Buffs.RemoveAll(x => x.BuffId == buff.BuffId);
                                    //Console.WriteLine($"[ApplyBuffEffects] Buffs removidos del Mob: {removed}");

                                    if (removed > 0)
                                    {
                                        broadcast(client.TamerId, new RemoveBuffPacket(mob.GeneralHandler, buff.BuffId).Serialize());
                                        //Console.WriteLine($"[ApplyBuffEffects] Paquete RemoveBuffPacket enviado");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                //Console.WriteLine($"[ApplyBuffEffects] Excepción en task DOT: {ex.Message}");
                                //_logger?.Information($"DOT error: {ex.Message}");
                            }
                            //Console.WriteLine($"[ApplyBuffEffects] Task async para DOT completada");
                        });
                        break;
                }
            }
        }

        //Console.WriteLine($"[ApplyBuffEffects] Función completada, todos los applies procesados");
    }

    private void RemoveBuffAndNotify(GameClient client, int buffId, Action<long, byte[]> broadcast)
    {
        //Console.WriteLine($"[RemoveBuffAndNotify] Iniciando para Buff ID: {buffId}");

        int removed = client.Tamer.Partner.BuffList.Buffs.RemoveAll(x => x.BuffId == buffId);
        //Console.WriteLine($"[RemoveBuffAndNotify] Buffs removidos del Partner: {removed}");

        if (removed > 0)
        {
            broadcast(client.TamerId, new RemoveBuffPacket(client.Tamer.Partner.GeneralHandler, buffId).Serialize());
            //Console.WriteLine($"[RemoveBuffAndNotify] Paquete RemoveBuffPacket enviado");
        }
        else
        {
            //Console.WriteLine($"[RemoveBuffAndNotify] No se removió ningún buff");
        }

        //Console.WriteLine($"[RemoveBuffAndNotify] Función completada");
    }





        private int GetDurationBySkillId(int skillCode)
        {
            return skillCode switch
            {
                (int)SkillBuffAndDebuffDurationEnum.FireRocket => 5, //38 = attribute enums
                (int)SkillBuffAndDebuffDurationEnum.DynamiteHead => 4, //33
                (int)SkillBuffAndDebuffDurationEnum.BlueThunder => 2, //39
                (int)SkillBuffAndDebuffDurationEnum.NeedleRain => 10, //37  missing packet?
                (int)SkillBuffAndDebuffDurationEnum.MysticBell => 3, //
                (int)SkillBuffAndDebuffDurationEnum.GoldRush => 3, // 39 missing packet petrify?
                (int)SkillBuffAndDebuffDurationEnum.NeedleStinger => 15, //6
                (int)SkillBuffAndDebuffDurationEnum.CurseOfQueen => 20, //24
                (int)SkillBuffAndDebuffDurationEnum.WhiteStatue => 15, //40 //reflect damage packet?
                (int)SkillBuffAndDebuffDurationEnum.RedSun => 10, //24 
                (int)SkillBuffAndDebuffDurationEnum.PlasmaShot => 5, //38
                (int)SkillBuffAndDebuffDurationEnum.ExtremeJihad => 10, //24
                (int)SkillBuffAndDebuffDurationEnum.MomijiOroshi => 15, //8
                (int)SkillBuffAndDebuffDurationEnum.Ittouryoudan => 20, //41
                (int)SkillBuffAndDebuffDurationEnum.MagnaAttack => 5, // MagnaAttack Magnamon Worn F1
                (int)SkillBuffAndDebuffDurationEnum.PlasmaRage => 10, // MagnaAttack Magnamon Worn F2
                (int)SkillBuffAndDebuffDurationEnum.RamapageAlterBF3 => 10, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.PMAwakenF3 => 31, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.ZeedAwakenF1 => 11, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.ZeedAwakenF2 => 6, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.ZeedAwakenF3 => 15, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.SakuyaAwakenF3 => 240, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.LilithmonXF1 => 13, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.AOAF3 => 12, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.OXF3 => 15, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.ZeedF1 => 15, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.ZeedF3 => 15, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.ExamonXF3 => 10, // Alter B Rampage
                (int)SkillBuffAndDebuffDurationEnum.UlforceFM => 30, // Tensegryti
                (int)SkillBuffAndDebuffDurationEnum.GodDramonF3 => 20,
                (int)SkillBuffAndDebuffDurationEnum.GodDramon => 120, // Tensegryti
                (int)SkillBuffAndDebuffDurationEnum.HolyDramonF3 => 20,
                (int)SkillBuffAndDebuffDurationEnum.HolyDramon => 120,

                _ => 0
            };
        }


    }
}