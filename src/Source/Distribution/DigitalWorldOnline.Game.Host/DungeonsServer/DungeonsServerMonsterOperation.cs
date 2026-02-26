using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.Map;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Assets.XML.ItemList;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Map.Dungeons;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Arena;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.ViewModel.Players;
using DigitalWorldOnline.Commons.Writers;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.Infrastructure.Migrations;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace DigitalWorldOnline.GameHost
{
    public sealed partial class DungeonsServer
    {
        private void MonsterOperation(GameMap map)
        {
            if (!map.ConnectedTamers.Any())
                return;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            map.UpdateMapMobs(_assets.NpcColiseum);

            foreach (var mob in map.Mobs)
            {
                if (!mob.AwaitingKillSpawn && DateTime.Now > mob.ViewCheckTime)
                {
                    if (mob.CurrentAction == MobActionEnum.Destroy)
                        continue;

                    mob.SetViewCheckTime();

                    mob.TamersViewing.RemoveAll(x => !map.ConnectedTamers.Select(y => y.Id).Contains(x));

                    var nearTamers = map.NearestTamers(mob.Id);

                    if (!nearTamers.Any() && !mob.TamersViewing.Any())
                        continue;

                    if (!mob.Dead && mob.CurrentAction != MobActionEnum.Destroy)
                    {
                        nearTamers.ForEach(nearTamer =>
                        {
                            if (!mob.TamersViewing.Contains(nearTamer))
                            {
                                mob.TamersViewing.Add(nearTamer);

                                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == nearTamer);

                                targetClient?.Send(new LoadMobsPacket(mob));
                                targetClient?.Send(new LoadBuffsPacket(mob));
                            }
                        });
                    }

                    var farTamers = map.ConnectedTamers.Select(x => x.Id).Except(nearTamers).ToList();

                    farTamers.ForEach(farTamer =>
                    {
                        if (mob.TamersViewing.Contains(farTamer))
                        {
                            var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == farTamer);

                            mob.TamersViewing.Remove(farTamer);
                            targetClient?.Send(new UnloadMobsPacket(mob));
                        }
                    });
                }

                if (!mob.CanAct)
                    continue;

                MobsOperation(map, mob);

                mob.SetNextAction();
            }

            map.UpdateMapMobs(true);

            foreach (var mob in map.SummonMobs)
            {
                if (DateTime.Now > mob.ViewCheckTime)
                {
                    mob.TamersViewing.Clear();
                    mob.SetViewCheckTime(30);

                    mob.TamersViewing.RemoveAll(x => !map.ConnectedTamers.Select(y => y.Id).Contains(x));

                    var nearTamers = map.NearestTamers(mob.Id);

                    if (!nearTamers.Any() && !mob.TamersViewing.Any())
                        continue;

                    if (!mob.Dead && mob.CurrentAction != MobActionEnum.Destroy)
                    {
                        nearTamers.ForEach(nearTamer =>
                        {
                            if (!mob.TamersViewing.Contains(nearTamer))
                            {
                                mob.TamersViewing.Add(nearTamer);

                                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == nearTamer);

                                targetClient?.Send(new LoadMobsPacket(mob, true));
                            }
                        });
                    }

                    var farTamers = map.ConnectedTamers.Select(x => x.Id).Except(nearTamers).ToList();

                    farTamers.ForEach(farTamer =>
                    {
                        if (mob.TamersViewing.Contains(farTamer))
                        {
                            var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == farTamer);

                            mob.TamersViewing.Remove(farTamer);
                            targetClient?.Send(new UnloadMobsPacket(mob));
                        }
                    });
                }

                if (!mob.CanAct)
                    continue;

                MobsOperation(map, mob);

                mob.SetNextAction();
            }

            stopwatch.Stop();

            var totalTime = stopwatch.Elapsed.TotalMilliseconds;

            if (totalTime >= 1000)
                Console.WriteLine($"MonstersOperation ({map.Mobs.Count}): {totalTime}.");
        }

        private async void MobsOperation(GameMap map, MobConfigModel mob)
        {
            long currentTick = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            switch (mob.CurrentAction)
            {
                case MobActionEnum.CrowdControl:
                    {
                        var debuff = mob.DebuffList.ActiveBuffs.Where(buff =>
                            buff.BuffInfo.SkillInfo.Apply.Any(apply =>
                                apply.Attribute == Commons.Enums.SkillCodeApplyAttributeEnum.CrowdControl
                            )
                        ).ToList();

                        if (mob.CurrentHP < 1)
                        {
                            mob.UpdateCurrentAction(MobActionEnum.Reward);
                            MobsOperation(map, mob);
                            break;
                        }

                        if (debuff.Any())
                        {
                            CheckDebuff(map, mob, debuff);
                            break;
                        }
                    }
                    break;

                case MobActionEnum.Respawn:
                    {
                        mob.Reset();
                        mob.ResetLocation();
                    }
                    break;

                case MobActionEnum.Reward:
                    {
                        ItemsReward(map, mob);
                        QuestKillReward(map, mob);
                        ExperienceReward(map, mob);

                        SourceKillSpawn(map, mob);
                        TargetKillSpawn(map, mob);

                        ColiseumStageClear(map, mob);

                        if (map.Clients.Any())
                            {
                                var anyClient = map.Clients.First(); // cualquiera sirve
                            // Normal Dungeons
                            await VerifyEDGNDungeonCompletion(map, mob, anyClient);
                            await VerifyZDGNDungeonCompletion(map, mob, anyClient);
                            await VerifyBDGNDungeonCompletion(map, mob, anyClient);
                            await VerifyQDGNDungeonCompletion(map, mob, anyClient);
                            await VerifyFDGDungeonCompletion(map, mob, anyClient);
                            await VerifyRBNDungeonCompletion(map, mob, anyClient);
                            // Hard Dungeons
                            await VerifyEDGHDungeonCompletion(map, mob, anyClient);
                            await VerifyZDGHDungeonCompletion(map, mob, anyClient);
                            await VerifyBDGHDungeonCompletion(map, mob, anyClient);
                            await VerifyQDGHDungeonCompletion(map, mob, anyClient);
                            // PvP Dungeon
                            await PvpDungeonCompletion(map, mob, anyClient);
                            
                        }
                        else
                        {
                            Console.WriteLine("[DUNGEON] Boss {MobType} died but no clients in map {InstanceId} - completion check skipped!", mob.Type, map.Id);
                        }

                        mob.UpdateCurrentAction(MobActionEnum.Destroy);
                    }
                    break;

                case MobActionEnum.Wait:
                    {
                        if (mob.Respawn && DateTime.Now > mob.DieTime.AddSeconds(2))
                        {
                            mob.SetNextWalkTime(UtilitiesFunctions.RandomInt(7, 14));
                            mob.SetAgressiveCheckTime(2);
                            mob.SetRespawn();
                        }
                        else
                        {
                            map.AttackNearbyTamer(mob, mob.TamersViewing, _assets.NpcColiseum);
                        }

                        CheckIsDead(map, mob);
                    }
                    break;

                case MobActionEnum.Walk:
                    {
                        map.BroadcastForTargetTamers(mob.TamersViewing, new SyncConditionPacket(mob.GeneralHandler, ConditionEnum.Default).Serialize());
                        mob.Move();
                        map.BroadcastForTargetTamers(mob.TamersViewing, new MobWalkPacket(mob).Serialize());

                        CheckIsDead(map, mob);
                    }
                    break;

                case MobActionEnum.GiveUp:
                    {
                        map.BroadcastForTargetTamers(mob.TamersViewing, new SyncConditionPacket(mob.GeneralHandler, ConditionEnum.Immortal).Serialize());
                        mob.ResetLocation();
                        map.BroadcastForTargetTamers(mob.TamersViewing, new MobRunPacket(mob).Serialize());
                        map.BroadcastForTargetTamers(mob.TamersViewing, new SetCombatOffPacket(mob.GeneralHandler).Serialize());

                        foreach (var targetTamer in mob.TargetTamers)
                        {
                            targetTamer.RemoveTarget(mob);
                            targetTamer.UpdateCombatInteractionTime();

                            //if (targetTamer.TargetMobs.Count <= 1)
                            if (targetTamer.TargetIMobs.Count <= 1)
                            {
                                targetTamer.StopBattle();

                                map.BroadcastForTamerViewsAndSelf(targetTamer.Id,
                                    new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                            }
                        }

                        mob.Reset(true);
                        map.BroadcastForTargetTamers(mob.TamersViewing, new UpdateCurrentHPRatePacket(mob.GeneralHandler, mob.CurrentHpRate).Serialize());
                    }
                    break;

                case MobActionEnum.Attack:
                    {
                        // 1. Check if mob is dead
                        if (mob.Dead)
                        {
                            foreach (var targetTamer in mob.TargetTamers)
                            {
                                if (targetTamer.TargetIMobs.Count <= 1)
                                {
                                    targetTamer.StopBattle();
                                    map.BroadcastForTamerViewsAndSelf(targetTamer.Id, new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                                }
                            }

                            break;
                        }

                        // 2. Check if target is valid
                        if (mob.Target == null || mob.TargetTamer == null || mob.TargetTamer.Hidden || !mob.TargetAlive)
                        {
                            mob.GiveUp();
                            break;
                        }

                        // 3. Check debuffs
                        if (mob.DebuffList.ActiveBuffs.Count > 0)
                        {
                            var debuff = mob.DebuffList.ActiveBuffs
                                .Where(buff => buff.BuffInfo.SkillInfo.Apply
                                .Any(apply => apply.Attribute == Commons.Enums.SkillCodeApplyAttributeEnum.CrowdControl)).ToList();

                            if (debuff.Any())
                            {
                                CheckDebuff(map, mob, debuff);
                                break;
                            }
                        }

                        // 4. Check skill attack
                        if (!mob.Dead && mob.SkillTime && !mob.CheckSkill && mob.IsPossibleSkill)
                        {
                            mob.UpdateCurrentAction(MobActionEnum.UseAttackSkill);
                            mob.SetNextAction();
                            break;
                        }

                        // 5. Update digimon target to digimon with more DE
                        if (mob.TargetTamers != null && mob.TargetTamers.Count > 1)
                        {
                            var targetTamer = mob.TargetTamers.OrderByDescending(t => t.Partner?.DE ?? 0).FirstOrDefault();

                            if (targetTamer != null && targetTamer.Partner != null)
                            {
                                mob.UpdateTarget(targetTamer);
                            }
                        }

                        // 6. Calculate distance to chase or attack
                        if (!mob.Dead && !mob.Chasing && mob.TargetAlive)
                        {
                            //var client = map.Clients.FirstOrDefault(x => x.TamerId == mob.TargetTamer.Id);

                            //var diff = UtilitiesFunctions.CalculateDistance(
                            //    mob.CurrentLocation.X, client.Tamer.Location.X,
                            //    mob.CurrentLocation.Y, client.Tamer.Location.Y);

                            var diff = UtilitiesFunctions.CalculateDistance(
                                mob.CurrentLocation.X, mob.Target.Location.X,
                                mob.CurrentLocation.Y, mob.Target.Location.Y);

                            var range = Math.Max(mob.ARValue, mob.Target.BaseInfo.ARValue);

                            if (diff <= range)
                            {
                                if (DateTime.Now < mob.LastHitTime.AddMilliseconds(mob.ASValue))
                                    break;

                                var missed = false;

                                if (mob.TargetTamer != null && mob.TargetTamer.GodMode)
                                    missed = true;
                                else if (mob.CanMissHit())
                                    missed = true;

                                if (missed)
                                {
                                    mob.UpdateLastHitTry();
                                    map.BroadcastForTargetTamers(mob.TamersViewing,
                                        new MissHitPacket(mob.GeneralHandler, mob.TargetHandler).Serialize());
                                    mob.UpdateLastHit();
                                    break;
                                }

                                map.AttackTarget(mob, null, _assets.NpcColiseum, currentTick);
                            }
                            else
                            {
                                map.ChaseTarget(mob);
                            }
                        }

                        CheckIsDead(map, mob);
                    }
                    break;

                case MobActionEnum.UseAttackSkill:
                    {
                        var debuff = mob.DebuffList.ActiveBuffs.Where(buff =>
                            buff.BuffInfo.SkillInfo.Apply.Any(apply =>
                                apply.Attribute == Commons.Enums.SkillCodeApplyAttributeEnum.CrowdControl)).ToList();

                        if (debuff.Any())
                        {
                            CheckDebuff(map, mob, debuff);
                            break;
                        }

                        MonsterSkillInfoAssetModel targetSkill = null;
                        //EDGN --------------------
                        int MegaSeadraSkillId = 0;
                        int MarinDevimonSkillId = 0;
                        int XuawnumonSkillId = 0;
                        //ZDGH --------------------
                        int BirdramonSkillId = 0;
                        int GarudramonSkillId = 0;
                        int phoenixSkillId = 0;
                        int zhuqiaomonSkillId = 0;
                        //BDGN --------------------
                        int SinduramonSkillId = 0;
                        int BlossomonSkillId = 0;
                        int BaihumonSkillId = 0;
                        //QDGN --------------------
                        int AntylamonSkillId = 0;
                        int MihiramonSkillId = 0;
                        int MajiramonSkillId = 0;
                        int QinglongmonSkillId = 0;


                        // Special handling for Phoenix ZDGN
                        if (mob.Type == 51084)
                        {
                            phoenixSkillId = GetPhoenixSkill(mob);

                            if (phoenixSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(phoenixSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Zhuqiaomon ZDGN
                        else if (mob.Type == 51085)
                        {
                            zhuqiaomonSkillId = GetZhuqiaomonSkill(mob);

                            if (zhuqiaomonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(zhuqiaomonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for MegaSeadramon EDGN
                        else if (mob.Type == 51070)
                        {
                            MegaSeadraSkillId = GetMegaSeadraSkill(mob);

                            if (MegaSeadraSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(MegaSeadraSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for MarinDevimon EDGN
                        else if (mob.Type == 51071)
                        {
                            MarinDevimonSkillId = GetMarinDevimonSkill(mob);

                            if (MarinDevimonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(MarinDevimonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Xuanwumon EDGN
                        else if (mob.Type == 51076)
                        {
                            XuawnumonSkillId = GetXuanwumonSkill(mob);

                            if (XuawnumonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(XuawnumonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Birdramon ZDGN
                        else if (mob.Type == 51082)
                        {
                            BirdramonSkillId = GetBirdramonSkill(mob);

                            if (BirdramonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(BirdramonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Garudamon ZDGN
                        else if (mob.Type == 51083)
                        {
                            GarudramonSkillId = GetGarudamonSkill(mob);

                            if (GarudramonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(GarudramonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Sinduramon BDGN
                        else if (mob.Type == 51090)
                        {
                            SinduramonSkillId = GetSinduramonRaidSkill(mob);

                            if (SinduramonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(SinduramonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Blossomon BDGN
                        else if (mob.Type == 51092)
                        {
                            BlossomonSkillId = GetBlossomonSkill(mob);

                            if (BlossomonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(BlossomonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Baihumon BDGN
                        else if (mob.Type == 51094)
                        {
                            BaihumonSkillId = GetBaihumonSkill(mob);

                            if (BaihumonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(BaihumonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Anyltamon QDGN
                        else if (mob.Type == 51094)
                        {
                            AntylamonSkillId = GetAntylamonRaidSkill(mob);

                            if (AntylamonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(AntylamonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Mihiramon QDGN
                        else if (mob.Type == 51099)
                        {
                            MihiramonSkillId = GetMihiramonRaidSkill(mob);

                            if (MihiramonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(MihiramonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Majiramon QDGN
                        else if (mob.Type == 51100)
                        {
                            MajiramonSkillId = GetMajiramonRaidSkill(mob);

                            if (MajiramonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(MajiramonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        // Special handling for Qinglonmon QDGN
                        else if (mob.Type == 51101)
                        {
                            QinglongmonSkillId = GetQinglongmonRaSkill(mob);

                            if (QinglongmonSkillId == 0)
                            {
                                // Use basic attack instead of skill
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }

                            // Find the skill with the specified ID
                            targetSkill = _assets.MonsterSkillInfo.GetValueOrDefault(QinglongmonSkillId);

                            if (targetSkill == null)
                            {
                                // Fallback to basic attack if skill not found
                                mob.UpdateCurrentAction(MobActionEnum.Attack);
                                mob.SetNextAction();
                                break;
                            }
                        }
                        else
                        {
                            // Normal random skill selection for other mob types
                            var skillList = _assets.MonsterSkillInfo.Values.Where(x => x.Type == mob.Type).ToList();

                            if (!skillList.Any())
                            {
                                mob.UpdateCheckSkill(true);
                                mob.UpdateCurrentAction(MobActionEnum.Wait);
                                mob.UpdateLastSkill();
                                mob.UpdateLastSkillTry();
                                mob.SetNextAction();
                                break;
                            }

                            Random random = new Random();
                            targetSkill = skillList[random.Next(0, skillList.Count)];
                        }

                        if (!mob.Dead && !mob.Chasing && mob.TargetAlive)
                        {
                            var diff = UtilitiesFunctions.CalculateDistance(
                                mob.CurrentLocation.X,
                                mob.Target.Location.X,
                                mob.CurrentLocation.Y,
                                mob.Target.Location.Y);

                            if (diff <= 1900)
                            {
                                if (DateTime.Now < mob.LastSkillTime.AddMilliseconds(mob.Cooldown) && mob.Cooldown > 0)
                                    break;

                                map.SkillTarget(mob, targetSkill, _assets.NpcColiseum);

                                if (mob.Type == 51084 && phoenixSkillId == 146)
                                {
                                    ZDGPhoenixMechanic(mob, mob.TargetTamer?.Id ?? 0);

                                    // Get a list of nearby tamers to apply the debuff
                                    var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == mob.TargetTamer?.Id);
                                    if (targetClient != null)
                                    {
                                        // Apply the debuffs to the target and nearby partners
                                        // Apply all three types of debuffs to the main target
                                        DebuffTamersPartner(targetClient, mob, 300, 146, 60005); // Degeneration
                                        DebuffTamersPartner(targetClient, mob, 300, 146, 60006); // Return (Fire)
                                        DebuffTamersPartner(targetClient, mob, 300, 146, 60007); // Silence

                                        // Find other nearby tamers within range and apply debuffs to their partners too
                                        foreach (var client in map.Clients)
                                        {
                                            // Skip the original target as we've already applied the debuff
                                            if (client.TamerId == targetClient.TamerId)
                                                continue;

                                            // Calculate distance between mob and this tamer
                                            if (client.Tamer != null && client.Tamer.Location != null)
                                            {
                                                double distance = CalculateDistance(
                                                    mob.CurrentLocation.X, mob.CurrentLocation.Y,
                                                    client.Tamer.Location.X, client.Tamer.Location.Y);

                                                // Apply debuff if tamer is within range (use 1500 units as range)
                                                if (distance <= 1500)
                                                {
                                                    // Apply all three debuffs to nearby partners as well
                                                    DebuffTamersPartner(client, mob, 300, 146, 60005); // Degeneration
                                                    DebuffTamersPartner(client, mob, 300, 146, 60006); // Return (Fire)
                                                    DebuffTamersPartner(client, mob, 300, 146, 60007); // Silence

                                                   // _logger.Information($"Applied all debuffs to nearby partner of tamer {client.Tamer.Name}");
                                                }
                                            }
                                        }
                                    }

                                    // Still call ZDGZDEBUFF for backward compatibility or other effects
                                    ZDGZDEBUFF(mob, mob.TargetTamer?.Id ?? 0, true);
                                }

                                // Special mechanics for Phoenix using skill 147
                                if (mob.Type == 51084 && phoenixSkillId == 147)
                                {
                                    // Call ZDGPhoenixMechanic to spawn minions
                                    ZDGPhoenixMechanic(mob, mob.TargetTamer?.Id ?? 0);

                                }

                                // Special mechanics for Zhuqiaomon using skill 152
                                if (mob.Type == 51085 && zhuqiaomonSkillId == 152)
                                {
                                    // Call ZDGZhuqiaomonMechanic to spawn minions
                                    ZDGZhuqiaomonMechanic(mob, mob.TargetTamer?.Id ?? 0);

                                    // Apply debuffs to nearby partners
                                    ZDGZDEBUFF(mob, mob.TargetTamer?.Id ?? 0, true);
                                }

                                // Special mechanics for Qinglongmon using skill 152
                                if (mob.Type == 51101 && QinglongmonSkillId == 853)
                                {
                                    // Call ZDGZhuqiaomonMechanic to spawn minions
                                    QinglongmonDungeonMechanic(mob, mob.TargetTamer?.Id ?? 0);
                                }

                                if (mob.Target != null)
                                {
                                    mob.UpdateCurrentAction(MobActionEnum.Wait);
                                    mob.SetNextAction();
                                }
                            }
                            else
                            {
                                map.ChaseTarget(mob);
                            }
                        }

                        CheckIsDead(map, mob);
                    }
                    break;
            }
        }

        private static void CheckDebuff(GameMap map, MobConfigModel mob, List<MobDebuffModel> debuffs)
        {
            if (debuffs != null)
            {
                for (int i = 0; i < debuffs.Count; i++)
                {
                    var debuff = debuffs[i];

                    if (!debuff.Expired && mob.CurrentAction != MobActionEnum.CrowdControl)
                    {
                        mob.UpdateCurrentAction(MobActionEnum.CrowdControl);
                    }

                    if (debuff.Expired && mob.CurrentAction == MobActionEnum.CrowdControl)
                    {
                        debuffs.Remove(debuff);

                        if (debuffs.Count == 0)
                        {
                            map.BroadcastForTargetTamers(mob.TamersViewing,
                                new RemoveBuffPacket(mob.GeneralHandler, debuff.BuffId, 1).Serialize());

                            mob.DebuffList.Buffs.Remove(debuff);

                            mob.UpdateCurrentAction(MobActionEnum.Wait);
                            mob.SetNextAction();
                        }
                        else
                        {
                            mob.DebuffList.Buffs.Remove(debuff);
                        }
                    }
                }
            }
        }

        private void ColiseumStageClear(GameMap map, MobConfigModel mob)
        {
            if (map.ColiseumMobs.Contains((int)mob.Id))
            {
                map.ColiseumMobs.Remove((int)mob.Id);

                if (map.ColiseumMobs.Count == 1)
                {
                    var npcInfo = _assets.NpcColiseum.FirstOrDefault(x => x.NpcId == map.ColiseumMobs.First());

                    if (npcInfo != null)
                    {
                        foreach (var player in map.Clients.Where(x => x.Tamer.Partner.Alive))
                        {
                            player.Tamer.Points.IncreaseAmount(npcInfo.MobInfo[player.Tamer.Points.CurrentStage - 1].WinPoints);

                            _sender.Send(new UpdateCharacterArenaPointsCommand(player.Tamer.Points));

                            player?.Send(new DungeonArenaStageClearPacket(mob.Type, mob.TargetTamer.Points.CurrentStage,
                                mob.TargetTamer.Points.Amount,
                                npcInfo.MobInfo[mob.TargetTamer.Points.CurrentStage - 1].WinPoints,
                                map.ColiseumMobs.First()));
                        }
                    }
                }
            }
        }

        private static void TargetKillSpawn(GameMap map, MobConfigModel mob)
        {
            var targetKillSpawn = map.KillSpawns.FirstOrDefault(x =>
                x.TargetMobs.Any(mobConfigModel => mobConfigModel.TargetMobType == mob.Type));

            if (targetKillSpawn != null)
            {
                mob.SetAwaitingKillSpawn();

                foreach (var targetMob in targetKillSpawn.TargetMobs.Where(x => x.TargetMobType == mob.Type).ToList())
                {
                    if (!map.Mobs.Exists(x => x.Type == targetMob.TargetMobType && !x.AwaitingKillSpawn))
                    {
                        targetKillSpawn.DecreaseTempMobs(targetMob);
                        targetKillSpawn.ResetCurrentSourceMobAmount();

                        map.BroadcastForMap(new KillSpawnEndChatNotifyPacket(targetMob.TargetMobType).Serialize());
                    }
                }
            }
        }

        private static void SourceKillSpawn(GameMap map, MobConfigModel mob)
        {
            var sourceMobKillSpawn = map.KillSpawns.FirstOrDefault(ks => ks.SourceMobs.Any(sm => sm.SourceMobType == mob.Type));

            if (sourceMobKillSpawn == null)
                return;

            var sourceKillSpawn = sourceMobKillSpawn.SourceMobs.FirstOrDefault(x => x.SourceMobType == mob.Type);

            if (sourceKillSpawn != null)
            {
                sourceKillSpawn.IncreaseCurrentSourceMobAmount();

                if (sourceKillSpawn.CurrentSourceMobRequiredAmount <= sourceKillSpawn.SourceMobRequiredAmount)
                {
                    var mobAmount = sourceKillSpawn.SourceMobRequiredAmount - sourceKillSpawn.CurrentSourceMobRequiredAmount;

                    map.BroadcastForMap(new KillSpawnMinimapNotifyPacket(sourceKillSpawn.SourceMobType, (byte)mobAmount).Serialize());
                }
                else if (sourceKillSpawn.CurrentSourceMobRequiredAmount > sourceKillSpawn.SourceMobRequiredAmount)
                {
                    sourceKillSpawn.ResetCurrentSourceMobAmount();
                }

                if (sourceMobKillSpawn.Spawn(sourceKillSpawn.SourceMobRequiredAmount))
                {
                    foreach (var targetMob in sourceMobKillSpawn.TargetMobs)
                    {
                        map.BroadcastForMap(new KillSpawnChatNotifyPacket(map.MapId, map.Channel, targetMob.TargetMobType).Serialize());

                        map.Mobs.Where(x => x.Type == targetMob.TargetMobType)?.ToList()
                            .ForEach(mobConfigModel =>
                            {
                                mobConfigModel.SetRespawn(true);
                                mobConfigModel.SetAwaitingKillSpawn(false);
                            });
                    }
                }
            }
        }

        private void QuestKillReward(GameMap map, MobConfigModel mob)
        {
            var partyIdList = new List<int>();

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                var giveUpList = new List<short>();

                foreach (var questInProgress in tamer.Progress.InProgressQuestData)
                {
                    var questInfo = _assets.Quest.FirstOrDefault(x => x.QuestId == questInProgress.QuestId);
                    if (questInfo != null)
                    {
                        if (!questInfo.QuestGoals.Exists(x => x.GoalType == QuestGoalTypeEnum.KillMonster))
                            continue;

                        var goalIndex = -1;
                        foreach (var questGoal in questInfo.QuestGoals)
                        {
                            if (questGoal.GoalId == mob?.Type)
                            {
                                goalIndex = questInfo.QuestGoals.FindIndex(x => x == questGoal);
                                break;
                            }
                        }

                        if (goalIndex != -1)
                        {
                            var currentGoalValue =
                                tamer.Progress.GetQuestGoalProgress(questInProgress.QuestId, goalIndex);
                            if (currentGoalValue < questInfo.QuestGoals[goalIndex].GoalAmount)
                            {
                                currentGoalValue++;
                                tamer.Progress.UpdateQuestInProgress(questInProgress.QuestId, goalIndex,
                                    currentGoalValue);

                                targetClient.Send(new QuestGoalUpdatePacket(questInProgress.QuestId, (byte)goalIndex,
                                    currentGoalValue));
                                var questToUpdate =
                                    targetClient.Tamer.Progress.InProgressQuestData.FirstOrDefault(x =>
                                        x.QuestId == questInProgress.QuestId);
                                _sender.Send(new UpdateCharacterInProgressCommand(questToUpdate));
                            }
                        }
                    }
                    else
                    {
                        _logger.Error($"Unknown quest id {questInProgress.QuestId}.");
                        targetClient.Send(new SystemMessagePacket($"Unknown quest id {questInProgress.QuestId}."));
                        giveUpList.Add(questInProgress.QuestId);
                    }
                }

                giveUpList.ForEach(giveUp => { tamer.Progress.RemoveQuest(giveUp); });

                var party = _partyManager.FindParty(targetClient.TamerId);
                if (party != null && !partyIdList.Contains(party.Id))
                {
                    partyIdList.Add(party.Id);

                    foreach (var partyMemberId in party.Members.Values.Select(x => x.Id))
                    {
                        var partyMemberClient = map.Clients.FirstOrDefault(x => x.TamerId == partyMemberId);
                        if (partyMemberClient == null || partyMemberId == targetClient.TamerId)
                            continue;

                        giveUpList = new List<short>();

                        foreach (var questInProgress in partyMemberClient.Tamer.Progress.InProgressQuestData)
                        {
                            var questInfo = _assets.Quest.FirstOrDefault(x => x.QuestId == questInProgress.QuestId);
                            if (questInfo != null)
                            {
                                if (!questInfo.QuestGoals.Exists(x => x.GoalType == QuestGoalTypeEnum.KillMonster))
                                    continue;

                                var goalIndex = -1;
                                foreach (var questGoal in questInfo.QuestGoals)
                                {
                                    if (questGoal.GoalId == mob?.Type)
                                    {
                                        goalIndex = questInfo.QuestGoals.FindIndex(x => x == questGoal);
                                        break;
                                    }
                                }

                                if (goalIndex != -1)
                                {
                                    var currentGoalValue =
                                        partyMemberClient.Tamer.Progress.GetQuestGoalProgress(questInProgress.QuestId,
                                            goalIndex);
                                    if (currentGoalValue < questInfo.QuestGoals[goalIndex].GoalAmount)
                                    {
                                        currentGoalValue++;
                                        partyMemberClient.Tamer.Progress.UpdateQuestInProgress(questInProgress.QuestId,
                                            goalIndex, currentGoalValue);

                                        partyMemberClient.Send(new QuestGoalUpdatePacket(questInProgress.QuestId,
                                            (byte)goalIndex, currentGoalValue));
                                        var questToUpdate =
                                            partyMemberClient.Tamer.Progress.InProgressQuestData.FirstOrDefault(x =>
                                                x.QuestId == questInProgress.QuestId);
                                        _sender.Send(new UpdateCharacterInProgressCommand(questToUpdate));
                                    }
                                }
                            }
                            else
                            {
                                _logger.Error($"Unknown quest id {questInProgress.QuestId}.");
                                partyMemberClient.Send(
                                    new SystemMessagePacket($"Unknown quest id {questInProgress.QuestId}."));
                                giveUpList.Add(questInProgress.QuestId);
                            }
                        }

                        giveUpList.ForEach(giveUp => { partyMemberClient.Tamer.Progress.RemoveQuest(giveUp); });
                    }
                }
            }

            partyIdList.Clear();
        }

        private void ItemsReward(GameMap map, MobConfigModel mob)
        {
            if (mob.DropReward == null)
                return;

            QuestDropReward(map, mob);

            if (mob.Class == 8)
                RaidReward(map, mob);
            else
                DropReward(map, mob);
        }

        private long BonusPartnerExp(GameMap map, MobConfigModel mob)
        {
            long totalPartnerExp = 0;

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                double expBonusMultiplier = tamer.BonusEXP / 100.0 + targetClient.ServerExperience / 100.0;
                double levelDifference = mob.Level - tamer.Partner.Level;


                long partnerExpToReceive = (long)CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.DigimonExperience);
                long finalExp = (long)(partnerExpToReceive * expBonusMultiplier);

                totalPartnerExp += (finalExp - partnerExpToReceive);


            }

            return totalPartnerExp;
        }

        private long BonusTamerExp(GameMap map, SummonMobModel mob)
        {
            long totalTamerExp = 0;

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                double expBonusMultiplier = tamer.BonusEXP / 100.0 + targetClient.ServerExperience / 100.0;
                double levelDifference = mob.Level - tamer.Partner.Level;

                long tamerExpToReceive = (long)CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.TamerExperience);

                long finalExp = (long)(tamerExpToReceive * expBonusMultiplier);

                totalTamerExp += (finalExp - tamerExpToReceive);
            }

            return totalTamerExp;
        }

        private long BonusPartnerExp(GameMap map, SummonMobModel mob)
        {
            long totalPartnerExp = 0;

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                double expBonusMultiplier = tamer.BonusEXP / 100.0 + targetClient.ServerExperience / 100.0;
                double levelDifference = mob.Level - tamer.Partner.Level;


                long partnerExpToReceive = (long)CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.DigimonExperience);

                long finalExp = (long)(partnerExpToReceive * expBonusMultiplier);

                totalPartnerExp += (finalExp - partnerExpToReceive);


            }

            return totalPartnerExp;
        }

        private long BonusTamerExp(GameMap map, MobConfigModel mob)
        {
            long totalTamerExp = 0;

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;


                double expBonusMultiplier = tamer.BonusEXP / 100.0 + targetClient.ServerExperience / 100.0;
                double levelDifference = mob.Level - tamer.Partner.Level;

                long tamerExpToReceive = (long)CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.TamerExperience);


                long finalExp = (long)(tamerExpToReceive * expBonusMultiplier);


                totalTamerExp += (finalExp - tamerExpToReceive);
            }

            return totalTamerExp;
        }

        private void ExperienceReward(GameMap map, MobConfigModel mob)
        {
            if (mob.ExpReward == null)
                return;

            var partyIdList = new List<int>();

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                var tamerExpToReceive = (long)(CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.TamerExperience)); //TODO: +bonus
                if (CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.TamerExperience) == 0)
                    tamerExpToReceive = 0;

                if (tamerExpToReceive > 100) tamerExpToReceive += UtilitiesFunctions.RandomInt(-35, 45);
                var tamerResult = ReceiveTamerExp(targetClient, targetClient.Tamer, tamerExpToReceive);

                var partnerExpToReceive = (long)(CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.DigimonExperience));



                if (CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.DigimonExperience) == 0)
                    partnerExpToReceive = 0;

                if (partnerExpToReceive > 100) partnerExpToReceive += UtilitiesFunctions.RandomInt(-35, 45);
                var partnerResult = ReceivePartnerExp(targetClient, targetClient.Partner, mob, partnerExpToReceive);

                var totalTamerExp = BonusTamerExp(map, mob);

                var bonusTamerExp = ReceiveBonusTamerExp(targetClient, targetClient.Tamer, totalTamerExp);

                var totalPartnerExp = BonusPartnerExp(map, mob);

                var bonusPartnerExp = ReceiveBonusPartnerExp(targetClient, targetClient.Partner, mob, totalPartnerExp);

                targetClient.blockAchievement = false;

                targetClient.Send(
                    new ReceiveExpPacket(
                        tamerExpToReceive,
                        totalTamerExp,
                        targetClient.Tamer.CurrentExperience,
                        targetClient.Partner.GeneralHandler,
                        partnerExpToReceive,
                        totalPartnerExp,
                        targetClient.Partner.CurrentExperience,
                        targetClient.Partner.CurrentEvolution.SkillExperience
                    )
                );

                SkillExpReward(map, targetClient);

                if (tamerResult.LevelGain > 0 || partnerResult.LevelGain > 0)
                {
                    targetClient.Send(new UpdateStatusPacket(targetClient.Tamer));

                    map.BroadcastForTamerViewsAndSelf(targetClient.TamerId,
                        new UpdateMovementSpeedPacket(targetClient.Tamer).Serialize());
                }

                _sender.Send(new UpdateCharacterExperienceCommand(tamer));
                _sender.Send(new UpdateDigimonExperienceCommand(tamer.Partner));

                PartyExperienceReward(map, mob, partyIdList, targetClient, tamerExpToReceive, tamerResult, partnerExpToReceive, partnerResult);
            }

            partyIdList.Clear();
        }

        public long CalculateExperience(int tamerLevel, int mobLevel, long baseExperience)
        {
            int levelDifference = tamerLevel - mobLevel;

            if (levelDifference <= 25)
            {
            }
            else
            {
                return 0;
            }

            return baseExperience;
        }

        private void SkillExpReward(GameMap map, GameClient? targetClient)
        {
            var ExpNeed = int.MaxValue;
            var evolutionType = _assets.DigimonBaseInfo.First(x => x.Type == targetClient.Partner.CurrentEvolution.Type)
                .EvolutionType;

            ExpNeed = SkillExperienceTable(evolutionType, targetClient.Partner.CurrentEvolution.SkillMastery);

            if (targetClient.Partner.CurrentEvolution.SkillMastery < 30)
            {
                if (targetClient.Partner.CurrentEvolution.SkillExperience >= ExpNeed)
                {
                    targetClient.Partner.ReceiveSkillPoint();
                    targetClient.Partner.ResetSkillExp(0);

                    var evolutionIndex = targetClient.Partner.Evolutions.IndexOf(targetClient.Partner.CurrentEvolution);

                    var packet = new PacketWriter();
                    packet.Type(1105);
                    packet.WriteInt(targetClient.Partner.GeneralHandler);
                    packet.WriteByte((byte)(evolutionIndex + 1));
                    packet.WriteByte(targetClient.Partner.CurrentEvolution.SkillPoints);
                    packet.WriteByte(targetClient.Partner.CurrentEvolution.SkillMastery);
                    packet.WriteInt(targetClient.Partner.CurrentEvolution.SkillExperience);

                    map.BroadcastForTamerViewsAndSelf(targetClient.TamerId, packet.Serialize());
                }
            }
        }

        private async Task PartyExperienceReward(
            GameMap map,
            MobConfigModel mob,
            List<int> partyIdList,
            GameClient? targetClient,
            long tamerExpToReceive,
            ReceiveExpResult tamerResult,
            long partnerExpToReceive,
            ReceiveExpResult partnerResult)
        {
            if (targetClient == null) throw new ArgumentNullException(nameof(targetClient));

            var party = _partyManager.FindParty(targetClient.TamerId);
            if (party != null && !partyIdList.Contains(party.Id))
            {
                partyIdList.Add(party.Id);

                foreach (var partyMemberId in party.Members.Values.Select(x => x.Id))
                {
                    var partyMemberClient = map.Clients.FirstOrDefault(x => x.TamerId == partyMemberId);
                    if (partyMemberClient == null || partyMemberId == targetClient.TamerId)
                        continue;

                    var totalTamerExp = BonusTamerExp(map, mob) / 2;
                    var bonusTamerExp = ReceiveBonusTamerExp(targetClient, partyMemberClient.Tamer, totalTamerExp);

                    var totalPartnerExp = BonusPartnerExp(map, mob) / 2;
                    var localPartnerResult = ReceivePartnerExp(targetClient, partyMemberClient.Partner, mob, totalPartnerExp);

                    partyMemberClient.Send(
                        new PartyReceiveExpPacket(
                            tamerExpToReceive,
                            totalTamerExp,//TODO: obter os bonus
                            partyMemberClient.Tamer.CurrentExperience,
                            partyMemberClient.Partner.GeneralHandler,
                            partnerExpToReceive,
                            totalPartnerExp,//TODO: obter os bonus
                            partyMemberClient.Partner.CurrentExperience,
                            partyMemberClient.Partner.CurrentEvolution.SkillExperience,
                            targetClient.Tamer.Name            // partySourceName
                        ));


                    if (tamerResult.LevelGain > 0 || localPartnerResult.LevelGain > 0)
                    {
                        targetClient.Send(new UpdateStatusPacket(targetClient.Tamer));

                        map.BroadcastForTamerViewsAndSelf(targetClient.TamerId,
                            new UpdateMovementSpeedPacket(targetClient.Tamer).Serialize());
                    }

                    await _sender.Send(new UpdateCharacterExperienceCommand(partyMemberClient.Tamer));
                    await _sender.Send(new UpdateDigimonExperienceCommand(partyMemberClient.Partner));
                }
            }
        }


        private async Task PartyExperienceReward(
          GameMap map,
          SummonMobModel mob,
          List<int> partyIdList,
          GameClient? targetClient,
           long tamerExpToReceive,
           ReceiveExpResult tamerResult,
           long partnerExpToReceive,
           ReceiveExpResult partnerResult)
        {
            var party = _partyManager.FindParty(targetClient.TamerId);
            if (party != null && !partyIdList.Contains(party.Id))
            {
                partyIdList.Add(party.Id);

                foreach (var partyMemberId in party.Members.Values.Select(x => x.Id))
                {
                    var partyMemberClient = map.Clients.FirstOrDefault(x => x.TamerId == partyMemberId);
                    if (partyMemberClient == null || partyMemberId == targetClient.TamerId)
                        continue;

                    var totalTamerExp = BonusTamerExp(map, mob) / 2;
                    var bonusTamerExp = ReceiveBonusTamerExp(targetClient, partyMemberClient.Tamer, totalTamerExp);

                    var totalPartnerExp = BonusPartnerExp(map, mob) / 2;
                    var localPartnerResult = ReceivePartnerExp(targetClient, partyMemberClient.Partner, mob, totalPartnerExp);

                    partyMemberClient.Send(
                        new PartyReceiveExpPacket(
                            tamerExpToReceive,
                            totalTamerExp,//TODO: obter os bonus
                            partyMemberClient.Tamer.CurrentExperience,
                            partyMemberClient.Partner.GeneralHandler,
                            partnerExpToReceive,
                            totalPartnerExp,//TODO: obter os bonus
                            partyMemberClient.Partner.CurrentExperience,
                            partyMemberClient.Partner.CurrentEvolution.SkillExperience,
                            targetClient.Tamer.Name            // partySourceName
                        ));

                    if (tamerResult.LevelGain > 0 || partnerResult.LevelGain > 0)
                    {
                        targetClient.Send(new UpdateStatusPacket(targetClient.Tamer));

                        map.BroadcastForTamerViewsAndSelf(targetClient.TamerId,
                            new UpdateMovementSpeedPacket(targetClient.Tamer).Serialize());
                    }

                    await _sender.Send(new UpdateCharacterExperienceCommand(partyMemberClient.Tamer));
                    await _sender.Send(new UpdateDigimonExperienceCommand(partyMemberClient.Partner));
                }
            }
        }

        private void DropReward(GameMap map, MobConfigModel mob)
        {
            _logger.Debug($"Getting normal mob reward");

            var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == mob.TargetTamer?.Id);

            if (targetClient == null)
                return;

            BitDropReward(map, mob, targetClient);

            ItemDropReward(map, mob, targetClient);
        }

        private void BitDropReward(GameMap map, MobConfigModel mob, GameClient? targetClient)
        {
            var bitsReward = mob.DropReward.BitsDrop;

            if (bitsReward != null && bitsReward.Chance >= UtilitiesFunctions.RandomDouble())
            {
                if (targetClient.Tamer.HasAura && targetClient.Tamer.Aura.ItemInfo.Section == 2100)
                {
                    var amount = UtilitiesFunctions.RandomInt(bitsReward.MinAmount, bitsReward.MaxAmount);

                    targetClient.Send(
                        new PickBitsPacket(
                            targetClient.Tamer.GeneralHandler,
                            amount
                        )
                    );

                    targetClient.Tamer.Inventory.AddBits(amount);

                    _sender.Send(new UpdateItemsCommand(targetClient.Tamer.Inventory));
                    _sender.Send(new UpdateItemListBitsCommand(targetClient.Tamer.Inventory.Id,
                        targetClient.Tamer.Inventory.Bits));
                    _logger.Verbose(
                        $"Character {targetClient.TamerId} aquired {amount} bits from mob {mob.Id} with magnetic aura {targetClient.Tamer.Aura.ItemId}.");
                }
                else
                {
                    var drop = _dropManager.CreateBitDrop(
                        targetClient.TamerId,
                        targetClient.Tamer.GeneralHandler,
                        bitsReward.MinAmount,
                        bitsReward.MaxAmount,
                        mob.CurrentLocation.MapId,
                        mob.CurrentLocation.X,
                        mob.CurrentLocation.Y
                    );

                    map.AddMapDrop(drop);
                }
            }
        }



        // Add this method if it doesn't already exist in the class
        private static readonly Random _random = new Random();

        private async void ItemDropReward(GameMap map, MobConfigModel mob, GameClient? targetClient)
        {
            if (mob.DropReward?.Drops == null || !mob.DropReward.Drops.Any() || targetClient == null)
                return;

            var itemsReward = new List<ItemDropConfigModel>(mob.DropReward.Drops);
            itemsReward.RemoveAll(x => _assets.QuestItemList.Contains(x.ItemId));
            var now = DateTime.Now;

            if (!itemsReward.Any())
                return;

            int vipMultiplier = targetClient.AccessLevel switch
            {
                Commons.Enums.Account.AccountAccessLevelEnum.Vip1 => 1,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip2 => 1,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip3 => 1,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip4 => 1,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip5 => 1,
                Commons.Enums.Account.AccountAccessLevelEnum.Administrator => 1,
                _ => 1
            };

            int dropped = 0;
            int totalDrops = UtilitiesFunctions.RandomInt(mob.DropReward.MinAmount, mob.DropReward.MaxAmount);

            while (dropped < totalDrops)
            {
                if (!itemsReward.Any())
                {
                    _logger.Warning($"Mob {mob.Id} has incorrect drops configuration.");
                    _logger.Warning($"MinAmount {mob.DropReward.MinAmount} | MaxAmount {mob.DropReward.MaxAmount}");
                    break;
                }

                var possibleDrops = itemsReward.OrderBy(x => Guid.NewGuid()).ToList();

                foreach (var itemDrop in possibleDrops)
                {
                    if (itemDrop.Chance >= UtilitiesFunctions.RandomDouble())
                    {
                        if (targetClient.Tamer?.HasAura == true && targetClient.Tamer.Aura?.ItemInfo?.Section == 2100)
                        {
                            var newItem = new ItemModel();
                           
                            newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(itemDrop.ItemId));

                            if (newItem.ItemInfo == null)
                            {
                                _logger.Warning($"No item info found with ID {itemDrop.ItemId} for tamer {targetClient.Tamer.Id}.");
                                targetClient.Send(new SystemMessagePacket($"No item info found with ID {itemDrop.ItemId}."));
                                continue;
                            }

                            newItem.ItemId = itemDrop.ItemId;
                            newItem.Amount = UtilitiesFunctions.RandomInt(itemDrop.MinAmount * vipMultiplier, itemDrop.MaxAmount * vipMultiplier);

                            if (newItem.IsTemporary)
                                newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                            var inventory = targetClient.Tamer.Inventory;

                            var existingItem = inventory.Items.FirstOrDefault(x =>
                                x.ItemId == newItem.ItemId &&
                                x.ItemInfo?.Overlap > 1 &&
                                x.Amount < x.ItemInfo.Overlap);

                            if (existingItem != null)
                            {
                                existingItem.IncreaseAmount(newItem.Amount);

                                var tempItem = (ItemModel)newItem.Clone();
                                tempItem.SetSlot(existingItem.Slot);

                                targetClient.Send(new ReceiveItemPacket(tempItem, InventoryTypeEnum.Inventory, existingItem.Slot));
                                await _sender.Send(new UpdateItemCommand(existingItem));
                            }
                            else
                            {
                                var emptySlot = inventory.GetEmptySlot;

                                if (emptySlot != -1)
                                {
                                    newItem.SetSlot(emptySlot);
                                    inventory.InsertItem(newItem);
                                    targetClient.Send(new ReceiveItemPacket(newItem, InventoryTypeEnum.Inventory, emptySlot));
                                    await _sender.Send(new UpdateItemCommand(newItem));
                                }
                                else
                                {
                                    //  _logger.Warning($"Failed to insert item {newItem.ItemId} into inventory. Inventory may be full.");

                                    targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                    targetClient.Send(new SystemMessagePacket("Seu inventário está cheio."));

                                    var itemName = newItem.ItemInfo?.Name ?? $"Item {newItem.ItemId}";
                                    targetClient.Send(new SystemMessagePacket($"Seu inventário está cheio. {itemName} não pôde ser adicionado."));

                                    var drop = _dropManager.CreateItemDrop(
                                        targetClient.Tamer.Id,
                                        targetClient.Tamer.GeneralHandler,
                                        itemDrop.ItemId,
                                        itemDrop.MinAmount * vipMultiplier,
                                        itemDrop.MaxAmount * vipMultiplier,
                                        mob.CurrentLocation.MapId,
                                        mob.CurrentLocation.X,
                                        mob.CurrentLocation.Y
                                    );

                                    map.AddMapDrop(drop);
                                }
                            }

                            _logger.Verbose(
                                $"Character {targetClient.TamerId} acquired {newItem.ItemId} x{newItem.Amount} from mob {mob.Id} with magnetic aura {targetClient.Tamer.Aura.ItemId}.");

                            dropped++;
                        }
                        else
                        {
                            var drop = _dropManager.CreateItemDrop(
                                targetClient.Tamer.Id,
                                targetClient.Tamer.GeneralHandler,
                                itemDrop.ItemId,
                                itemDrop.MinAmount * vipMultiplier,
                                itemDrop.MaxAmount * vipMultiplier,
                                mob.CurrentLocation.MapId,
                                mob.CurrentLocation.X,
                                mob.CurrentLocation.Y
                            );

                            map.AddMapDrop(drop);
                            dropped++;
                        }

                        itemsReward.RemoveAll(x => x.Id == itemDrop.Id);
                        break;
                    }
                }
            }
        }

        private void QuestDropReward(GameMap map, MobConfigModel mob)
        {
            var itemsReward = new List<ItemDropConfigModel>();
            itemsReward.AddRange(mob.DropReward.Drops);
            itemsReward.RemoveAll(x => !_assets.QuestItemList.Contains(x.ItemId));

            if (!itemsReward.Any())
                return;

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                if (!tamer.Progress.InProgressQuestData.Any())
                    continue;

                var updateItemList = false;
                var possibleDrops = itemsReward.Randomize();
                foreach (var itemDrop in possibleDrops)
                {
                    if (itemDrop.Chance >= UtilitiesFunctions.RandomDouble())
                    {
                        foreach (var questInProgress in tamer.Progress.InProgressQuestData)
                        {
                            var questInfo = _assets.Quest.FirstOrDefault(x => x.QuestId == questInProgress.QuestId);
                            if (questInfo != null)
                            {
                                if (!questInfo.QuestGoals.Exists(x => x.GoalType == QuestGoalTypeEnum.LootItem))
                                    continue;

                                var goalIndex = -1;
                                foreach (var questGoal in questInfo.QuestGoals)
                                {
                                    if (questGoal.GoalId == itemDrop?.ItemId)
                                    {
                                        var inventoryItems = tamer.Inventory.FindItemsById(questGoal.GoalId);
                                        var goalAmount = questGoal.GoalAmount;

                                        foreach (var inventoryItem in inventoryItems)
                                        {
                                            goalAmount -= inventoryItem.Amount;
                                            if (goalAmount <= 0)
                                            {
                                                goalAmount = 0;
                                                break;
                                            }
                                        }

                                        if (goalAmount > 0)
                                        {
                                            goalIndex = questInfo.QuestGoals.FindIndex(x => x == questGoal);
                                            break;
                                        }
                                    }
                                }

                                if (goalIndex != -1)
                                {
                                    var newItem = new ItemModel();
                                    newItem.SetItemInfo(
                                        _assets.ItemInfo.GetValueOrDefault(itemDrop.ItemId));

                                    if (newItem.ItemInfo == null)
                                    {
                                        _logger.Warning(
                                            $"No item info found with ID {itemDrop.ItemId} for tamer {tamer.Id}.");
                                        targetClient.Send(
                                            new SystemMessagePacket($"No item info found with ID {itemDrop.ItemId}."));
                                        continue;
                                    }

                                    newItem.ItemId = itemDrop.ItemId;
                                    newItem.Amount =
                                        UtilitiesFunctions.RandomInt(itemDrop.MinAmount, itemDrop.MaxAmount);

                                    var itemClone = (ItemModel)newItem.Clone();
                                    if (tamer.Inventory.AddItem(newItem))
                                    {
                                        updateItemList = true;
                                        targetClient.Send(new ReceiveItemPacket(itemClone,
                                            InventoryTypeEnum.Inventory));
                                    }
                                    else
                                    {
                                        targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                    }
                                }
                            }
                            else
                            {
                                _logger.Error($"Unknown quest id {questInProgress.QuestId}.");
                                targetClient.Send(
                                    new SystemMessagePacket($"Unknown quest id {questInProgress.QuestId}."));
                            }
                        }

                        if (updateItemList) _sender.Send(new UpdateItemsCommand(tamer.Inventory));

                        itemsReward.RemoveAll(x => x.Id == itemDrop.Id);
                    }
                }
            }
        }

        private async void RaidReward(GameMap map, MobConfigModel mob)
        {
            var raidResult = mob.RaidDamage.Where(x => x.Key > 0).DistinctBy(x => x.Key);
            var keyValuePairs = raidResult.ToList();

            var writer = new PacketWriter();
            writer.Type(1604);
            writer.WriteInt(keyValuePairs.Count());

            int i = 1;

            var attackerName = string.Empty;
            var attackerType = 0;

            foreach (var raidTamer in keyValuePairs.OrderByDescending(x => x.Value))
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == raidTamer.Key);

                if (i == 1 && targetClient != null)
                {
                    attackerName = targetClient.Tamer.Name;
                    attackerType = targetClient.Tamer.Partner.CurrentType;
                }

                if (i <= 10)
                {
                    writer.WriteInt(i);
                    writer.WriteString(targetClient?.Tamer?.Name ?? $"Tamer{i}");
                    writer.WriteString(targetClient?.Partner?.Name ?? $"Partner{i}");
                    writer.WriteInt(raidTamer.Value);
                }

                var bitsReward = mob.DropReward.BitsDrop;
                if (targetClient != null && bitsReward != null && bitsReward.Chance >= UtilitiesFunctions.RandomDouble())
                {
                    var drop = _dropManager.CreateBitDrop(
                        targetClient.Tamer.Id,
                        targetClient.Tamer.GeneralHandler,
                        bitsReward.MinAmount,
                        bitsReward.MaxAmount,
                        mob.CurrentLocation.MapId,
                        mob.CurrentLocation.X,
                        mob.CurrentLocation.Y
                    );

                    map.DropsToAdd.Add(drop);
                }

                var raidRewards = mob.DropReward.Drops;
                raidRewards.RemoveAll(x => _assets.QuestItemList.Contains(x.ItemId));

                if (targetClient != null && raidRewards != null && raidRewards.Any())
                {
                    int rewardRank = i <= 3 ? i : 4;

                    var itemDropConfigModels = raidRewards
                        .Where(x => x.Rank == rewardRank)
                        .ToList();

                    foreach (var reward in itemDropConfigModels)
                    {
                        if (reward.Chance >= UtilitiesFunctions.RandomDouble())
                        {
                            var newItem = new ItemModel();
                            newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(reward.ItemId));

                            if (newItem.ItemInfo == null)
                            {
                                targetClient.Send(new SystemMessagePacket($"No item info found with ID {reward.ItemId}."));
                                break;
                            }

                            newItem.ItemId = reward.ItemId;
                            newItem.Amount = UtilitiesFunctions.RandomInt(reward.MinAmount, reward.MaxAmount);

                            if (newItem.IsTemporary)
                                newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                            var inventory = targetClient.Tamer.Inventory;

                            var existingItem = inventory.Items.FirstOrDefault(x =>
                                x.ItemId == newItem.ItemId &&
                                x.ItemInfo?.Overlap > 1 &&
                                x.Amount < x.ItemInfo.Overlap);

                            if (existingItem != null)
                            {
                                existingItem.IncreaseAmount(newItem.Amount);

                                var tempItem = (ItemModel)newItem.Clone();
                                tempItem.SetSlot(existingItem.Slot);

                                targetClient.Send(new ReceiveItemPacket(tempItem, InventoryTypeEnum.Inventory, existingItem.Slot));
                                await _sender.Send(new UpdateItemCommand(existingItem));
                            }
                            else
                            {
                                var emptySlot = inventory.GetEmptySlot;

                                if (emptySlot != -1)
                                {
                                    newItem.SetSlot(emptySlot);
                                    inventory.InsertItem(newItem);
                                    targetClient.Send(new ReceiveItemPacket(newItem, InventoryTypeEnum.Inventory, emptySlot));
                                    await _sender.Send(new UpdateItemCommand(newItem));
                                }
                                else
                                {
                                    targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));

                                    var itemName = newItem.ItemInfo?.Name ?? $"Item {newItem.ItemId}";
                                    targetClient.Send(new SystemMessagePacket($"Seu inventário está cheio. {itemName} foi enviado ao Armazém de Presentes."));

                                    targetClient.Tamer.GiftWarehouse.AddItem(newItem);
                                }
                            }

                            if (i > 3)
                            {
                                targetClient.Send(new SystemMessagePacket(
                                    $"Você recebeu a recompensa padrão por participar do raid {mob.Name}."));
                            }
                        }
                    }
                }

                i++;
            }

            map.BroadcastForTargetTamers(mob.RaidDamage.Select(x => x.Key).ToList(), writer.Serialize());

        }

        // ------------------------------------------------------------------------------------

        private void RaidReward(GameMap map, SummonMobModel mob)
        {
            var raidResult = mob.RaidDamage.Where(x => x.Key > 0).DistinctBy(x => x.Key);
            var keyValuePairs = raidResult.ToList();

            var writer = new PacketWriter();
            writer.Type(1604);
            writer.WriteInt(keyValuePairs.Count());

            int i = 1;

            var updateItemList = new List<ItemListModel>();

            foreach (var raidTamer in keyValuePairs.OrderByDescending(x => x.Value))
            {
                _logger.Verbose(
                    $"Character {raidTamer.Key} rank {i} on raid {mob.Id} - {mob.Name} with damage {raidTamer.Value}.");

                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == raidTamer.Key);

                if (i <= 10)
                {
                    writer.WriteInt(i);
                    writer.WriteString(targetClient?.Tamer?.Name ?? $"Tamer{i}");
                    writer.WriteString(targetClient?.Partner?.Name ?? $"Partner{i}");
                    writer.WriteInt(raidTamer.Value);
                }

                var bitsReward = mob.DropReward.BitsDrop;
                if (targetClient != null && bitsReward != null &&
                    bitsReward.Chance >= UtilitiesFunctions.RandomDouble())
                {
                    var drop = _dropManager.CreateBitDrop(
                        targetClient.Tamer.Id,
                        targetClient.Tamer.GeneralHandler,
                        bitsReward.MinAmount,
                        bitsReward.MaxAmount,
                        mob.CurrentLocation.MapId,
                        mob.CurrentLocation.X,
                        mob.CurrentLocation.Y
                    );

                    map.DropsToAdd.Add(drop);
                }

                var raidRewards = mob.DropReward.Drops;
                raidRewards.RemoveAll(x => _assets.QuestItemList.Contains(x.ItemId));

                if (targetClient != null && raidRewards != null && raidRewards.Any())
                {
                    foreach (var reward in raidRewards)
                    {
                        if (reward.Chance >= UtilitiesFunctions.RandomDouble())
                        {
                            var newItem = new ItemModel();
                            newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(reward.ItemId));

                            if (newItem.ItemInfo == null)
                            {
                                _logger.Warning(
                                    $"No item info found with ID {reward.ItemId} for tamer {targetClient.TamerId}.");
                                targetClient.Send(
                                    new SystemMessagePacket($"No item info found with ID {reward.ItemId}."));
                                continue; // Continue para a próxima recompensa se não houver informações sobre o item.
                            }

                            newItem.ItemId = reward.ItemId;
                            newItem.Amount = UtilitiesFunctions.RandomInt(reward.MinAmount, reward.MaxAmount);

                            if (newItem.IsTemporary)
                                newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                            var itemClone = (ItemModel)newItem.Clone();
                            if (targetClient.Tamer.Inventory.AddItem(newItem))
                            {
                                targetClient.Send(new ReceiveItemPacket(itemClone, InventoryTypeEnum.Inventory));
                                updateItemList.Add(targetClient.Tamer.Inventory);
                            }
                            else
                            {
                                targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                targetClient.Tamer.GiftWarehouse.AddItem(newItem);
                                updateItemList.Add(targetClient.Tamer.GiftWarehouse);
                            }
                        }
                    }
                }


                i++;
            }

            map.BroadcastForTargetTamers(mob.RaidDamage.Select(x => x.Key).ToList(), writer.Serialize());
            updateItemList.ForEach(itemList => { _sender.Send(new UpdateItemsCommand(itemList)); });
        }

        private async void MobsOperation(GameMap map, SummonMobModel mob)
        {
            switch (mob.CurrentAction)
            {
                case MobActionEnum.Respawn:
                    {
                        mob.Reset();
                        mob.ResetLocation();
                    }
                    break;

                case MobActionEnum.Reward:
                    {
                        ItemsReward(map, mob);
                        QuestKillReward(map, mob);
                        ExperienceReward(map, mob);
                    }
                    break;

                case MobActionEnum.Wait:
                    {
                        if (mob.Respawn && DateTime.Now > mob.DieTime.AddSeconds(2))
                        {
                            mob.SetNextWalkTime(UtilitiesFunctions.RandomInt(7, 14));
                            mob.SetAgressiveCheckTime(2);
                            mob.SetRespawn();
                        }
                        else
                        {
                            map.AttackNearbyTamer(mob, mob.TamersViewing, _assets.NpcColiseum);
                        }

                        CheckIsDead(map, mob);
                    }
                    break;

                case MobActionEnum.Walk:
                    {
                        map.BroadcastForTargetTamers(mob.TamersViewing, new SyncConditionPacket(mob.GeneralHandler, ConditionEnum.Default).Serialize());
                        mob.Move();
                        map.BroadcastForTargetTamers(mob.TamersViewing, new MobWalkPacket(mob).Serialize());
                        
                        CheckIsDead(map, mob);
                    }
                    break;

                case MobActionEnum.GiveUp:
                    {
                        map.BroadcastForTargetTamers(mob.TamersViewing, new SyncConditionPacket(mob.GeneralHandler, ConditionEnum.Immortal).Serialize());
                        mob.ResetLocation();
                        map.BroadcastForTargetTamers(mob.TamersViewing, new MobRunPacket(mob).Serialize());
                        map.BroadcastForTargetTamers(mob.TamersViewing, new SetCombatOffPacket(mob.GeneralHandler).Serialize());

                        foreach (var targetTamer in mob.TargetTamers)
                        {
                            targetTamer.RemoveTarget(mob);
                            targetTamer.UpdateCombatInteractionTime();

                            //if (targetTamer.TargetMobs.Count <= 1)
                            if (targetTamer.TargetIMobs.Count <= 1)
                            {
                                targetTamer.StopBattle();

                                map.BroadcastForTamerViewsAndSelf(targetTamer.Id,
                                    new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                            }
                        }

                        mob.Reset(true);
                        map.BroadcastForTargetTamers(mob.TamersViewing, new UpdateCurrentHPRatePacket(mob.GeneralHandler, mob.CurrentHpRate).Serialize());
                    }
                    break;

                case MobActionEnum.Attack:
                    {
                        // 1. Lida com mob morto primeiro
                        if (mob.Dead)
                        {
                            foreach (var targetTamer in mob.TargetTamers)
                            {
                                if (targetTamer.TargetIMobs.Count < 1)
                                {
                                    targetTamer.StopBattle();
                                    map.BroadcastForTamerViewsAndSelf(targetTamer.Id, new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                                }
                            }

                            break;
                        }


                        // 2. Checks target validity
                        if (!mob.Dead && (mob.Target == null || mob.TargetTamer == null || mob.TargetTamer.Hidden || !mob.TargetAlive))
                        {
                            //_logger.Warning($"SummonMob {mob.Id} is giving up. Target: {mob.Target}, TargetTamer: {mob.TargetTamer}, Hidden: {mob.TargetTamer?.Hidden}, TargetAlive: {mob.TargetAlive}");
                            mob.GiveUp();
                            break;
                        }

                        // 3. Verifica e aplica debuffs
                        if (mob.DebuffList.ActiveBuffs.Count > 0)
                        {
                            var debuff = mob.DebuffList.ActiveBuffs
                                .Where(buff => buff.BuffInfo.SkillInfo.Apply
                                .Any(apply => apply.Attribute == Commons.Enums.SkillCodeApplyAttributeEnum.CrowdControl)).ToList();

                            if (debuff.Any())
                            {
                                //CheckDebuff(map, mob, debuff);
                                break;
                            }
                        }

                        // 4. Tenta usar uma skill de ataque
                        if (!mob.Dead && mob.SkillTime && !mob.CheckSkill && mob.IsPossibleSkill)
                        {
                            mob.UpdateCurrentAction(MobActionEnum.UseAttackSkill);
                            mob.SetNextAction();

                            break;
                        }

                        // 5. Atualiza o Target para o digimon com mais DE
                        if (mob.TargetTamers != null && mob.TargetTamers.Count > 1)
                        {
                            var targetTamer = mob.TargetTamers.OrderByDescending(t => t.Partner?.DE ?? 0).FirstOrDefault();

                            if (targetTamer != null && targetTamer.Partner != null)
                            {
                                mob.UpdateTarget(targetTamer);
                            }
                        }

                        // 6. Calcula distância e decide entre ataque básico ou perseguição
                        if (!mob.Dead && !mob.Chasing && mob.TargetAlive)
                        {
                            //var client = map.Clients.FirstOrDefault(x => x.TamerId == mob.TargetTamer.Id);

                            //var diff = UtilitiesFunctions.CalculateDistance(
                            //    mob.CurrentLocation.X, client.Tamer.Location.X,
                            //    mob.CurrentLocation.Y, client.Tamer.Location.Y);

                            var diff = UtilitiesFunctions.CalculateDistance(
                                mob.CurrentLocation.X, mob.Target.Location.X,
                                mob.CurrentLocation.Y, mob.Target.Location.Y);

                            var range = Math.Max(mob.ARValue, mob.Target.BaseInfo.ARValue);

                            if (diff <= range)
                            {
                                if (DateTime.Now < mob.LastHitTime.AddMilliseconds(mob.ASValue))
                                    break;

                                var missed = false;

                                if (mob.TargetTamer != null && mob.TargetTamer.GodMode)
                                    missed = true;
                                else if (mob.CanMissHit())
                                    missed = true;

                                if (missed)
                                {
                                    mob.UpdateLastHitTry();
                                    map.BroadcastForTargetTamers(mob.TamersViewing,
                                        new MissHitPacket(mob.GeneralHandler, mob.TargetHandler).Serialize());
                                    mob.UpdateLastHit();
                                    break;
                                }

                                map.AttackTarget(mob);
                            }
                            else
                            {
                                map.ChaseTarget(mob);
                            }
                        }

                        CheckIsDead(map, mob);
                    }
                    break;

                case MobActionEnum.UseAttackSkill:
                    {
                        if (!mob.Dead && ((mob.TargetTamer == null || mob.TargetTamer.Hidden))) //Anti-kite
                        {
                            mob.GiveUp();
                            break;
                        }

                        var skillList = _assets.MonsterSkillInfo.Values.Where(x => x.Type == mob.Type).ToList();

                        if (!skillList.Any())
                        {
                            mob.UpdateCheckSkill(true);
                            mob.UpdateCurrentAction(MobActionEnum.Wait);
                            mob.UpdateLastSkill();
                            mob.UpdateLastSkillTry();
                            mob.SetNextAction();
                            break;
                        }

                        Random random = new Random();

                        var targetSkill = skillList[random.Next(0, skillList.Count)];

                        if (!mob.Dead && !mob.Chasing && mob.TargetAlive)
                        {
                            var diff = UtilitiesFunctions.CalculateDistance(
                                mob.CurrentLocation.X,
                                mob.Target.Location.X,
                                mob.CurrentLocation.Y,
                                mob.Target.Location.Y);

                            if (diff <= 1900)
                            {
                                if (DateTime.Now < mob.LastSkillTime.AddMilliseconds(mob.Cooldown) && mob.Cooldown > 0)
                                    break;

                                map.SkillTarget(mob, targetSkill);

                                if (mob.Target != null)
                                {
                                    mob.UpdateCurrentAction(MobActionEnum.Wait);

                                    mob.SetNextAction();
                                }
                            }
                            else
                            {
                                map.ChaseTarget(mob);
                            }
                        }

                        CheckIsDead(map, mob);
                    }
                    break;
            }
        }

        private async void CheckIsDead(GameMap map, MobConfigModel mob)
        {
            if (mob.Dead)
            {

                foreach (var targetTamer in mob.TargetTamers)
                {
                    targetTamer.StopBattle();
                    map.BroadcastForTamerViewsAndSelf(targetTamer.Id,
                        new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                }

                //Console.WriteLine($"RoyalBase Allowed To Use Portal: {map?.RoyalBaseMap?.AllowUsingPortalFromFloorOneToFloorTwo.ToString()}");
                //if (map.IsRoyalBase && map.RoyalBaseMap != null)
                //{
                //    map.RoyalBaseMap.UpdateMonsterDead(mob);

                //    await Task.Delay(5000);

                //    int CurrentFloor = map.RoyalBaseMap.GetCurrentMobFloor(mob);
                //    if (CurrentFloor == 1)
                //    {
                //        foreach (var targetTamer in mob.TargetTamers)
                //        {
                //            targetTamer.NewLocation(1701, 32000, 32000);
                //            await _sender.Send(new UpdateCharacterLocationCommand(targetTamer.Location));

                //            targetTamer.Partner.NewLocation(1701, 32000, 32000);
                //            await _sender.Send(new UpdateDigimonLocationCommand(targetTamer.Partner.Location));

                //            map.BroadcastForUniqueTamer(targetTamer.Id, new LocalMapSwapPacket(
                //                targetTamer.GeneralHandler, targetTamer.Partner.GeneralHandler,
                //                32000, 32000, 32000, 32000).Serialize());
                //        }
                //    }
                //}

            }
        }

        private async void CheckIsDead(GameMap map, SummonMobModel mob)
        {
            if (mob.Dead)
            {
                foreach (var targetTamer in mob.TargetTamers)
                {
                    targetTamer.StopBattle();
                    map.BroadcastForTamerViewsAndSelf(targetTamer.Id,
                        new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                }

                if (mob.Type == 51023 || mob.Type == 51061 || mob.Type == 30483)
                {
                    _logger.Information($"Summoned mob {mob.Type} killed, removing debuff");
                    // Call ZDGZDEBUFF with isSpawn=false to remove the debuff
                    // Pass the mob's killer tamer ID to remove the debuff from that player's partner
                    ZDGZDEBUFF(mob, mob.TargetTamer?.Id ?? 0, false);
                }

                // Stop battle for all targeting tamers
                foreach (var targetTamer in mob.TargetTamers)
                {
                    targetTamer.StopBattle();
                    map.BroadcastForTamerViewsAndSelf(targetTamer.Id,
                        new SetCombatOffPacket(targetTamer.Partner.GeneralHandler).Serialize());
                }

            }
        }

        private void QuestKillReward(GameMap map, SummonMobModel mob)
        {
            var partyIdList = new List<int>();

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                var giveUpList = new List<short>();

                foreach (var questInProgress in tamer.Progress.InProgressQuestData)
                {
                    var questInfo = _assets.Quest.FirstOrDefault(x => x.QuestId == questInProgress.QuestId);
                    if (questInfo != null)
                    {
                        if (!questInfo.QuestGoals.Exists(x => x.GoalType == QuestGoalTypeEnum.KillMonster))
                            continue;

                        var goalIndex = -1;
                        foreach (var questGoal in questInfo.QuestGoals)
                        {
                            if (questGoal.GoalId == mob?.Type)
                            {
                                goalIndex = questInfo.QuestGoals.FindIndex(x => x == questGoal);
                                break;
                            }
                        }

                        if (goalIndex != -1)
                        {
                            var currentGoalValue =
                                tamer.Progress.GetQuestGoalProgress(questInProgress.QuestId, goalIndex);
                            if (currentGoalValue < questInfo.QuestGoals[goalIndex].GoalAmount)
                            {
                                currentGoalValue++;
                                tamer.Progress.UpdateQuestInProgress(questInProgress.QuestId, goalIndex,
                                    currentGoalValue);

                                targetClient.Send(new QuestGoalUpdatePacket(questInProgress.QuestId, (byte)goalIndex,
                                    currentGoalValue));
                                var questToUpdate =
                                    targetClient.Tamer.Progress.InProgressQuestData.FirstOrDefault(x =>
                                        x.QuestId == questInProgress.QuestId);
                                _sender.Send(new UpdateCharacterInProgressCommand(questToUpdate));
                            }
                        }
                    }
                    else
                    {
                        _logger.Error($"Unknown quest id {questInProgress.QuestId}.");
                        targetClient.Send(new SystemMessagePacket($"Unknown quest id {questInProgress.QuestId}."));
                        giveUpList.Add(questInProgress.QuestId);
                    }
                }

                giveUpList.ForEach(giveUp => { tamer.Progress.RemoveQuest(giveUp); });

                var party = _partyManager.FindParty(targetClient.TamerId);
                if (party != null && !partyIdList.Contains(party.Id))
                {
                    partyIdList.Add(party.Id);

                    foreach (var partyMemberId in party.Members.Values.Select(x => x.Id))
                    {
                        var partyMemberClient = map.Clients.FirstOrDefault(x => x.TamerId == partyMemberId);
                        if (partyMemberClient == null || partyMemberId == targetClient.TamerId)
                            continue;

                        giveUpList = new List<short>();

                        foreach (var questInProgress in partyMemberClient.Tamer.Progress.InProgressQuestData)
                        {
                            var questInfo = _assets.Quest.FirstOrDefault(x => x.QuestId == questInProgress.QuestId);
                            if (questInfo != null)
                            {
                                if (!questInfo.QuestGoals.Exists(x => x.GoalType == QuestGoalTypeEnum.KillMonster))
                                    continue;

                                var goalIndex = -1;
                                foreach (var questGoal in questInfo.QuestGoals)
                                {
                                    if (questGoal.GoalId == mob?.Type)
                                    {
                                        goalIndex = questInfo.QuestGoals.FindIndex(x => x == questGoal);
                                        break;
                                    }
                                }

                                if (goalIndex != -1)
                                {
                                    var currentGoalValue =
                                        partyMemberClient.Tamer.Progress.GetQuestGoalProgress(questInProgress.QuestId,
                                            goalIndex);
                                    if (currentGoalValue < questInfo.QuestGoals[goalIndex].GoalAmount)
                                    {
                                        currentGoalValue++;
                                        partyMemberClient.Tamer.Progress.UpdateQuestInProgress(questInProgress.QuestId,
                                            goalIndex, currentGoalValue);

                                        partyMemberClient.Send(new QuestGoalUpdatePacket(questInProgress.QuestId,
                                            (byte)goalIndex, currentGoalValue));
                                        var questToUpdate =
                                            partyMemberClient.Tamer.Progress.InProgressQuestData.FirstOrDefault(x =>
                                                x.QuestId == questInProgress.QuestId);
                                        _sender.Send(new UpdateCharacterInProgressCommand(questToUpdate));
                                    }
                                }
                            }
                            else
                            {
                                _logger.Error($"Unknown quest id {questInProgress.QuestId}.");
                                partyMemberClient.Send(
                                    new SystemMessagePacket($"Unknown quest id {questInProgress.QuestId}."));
                                giveUpList.Add(questInProgress.QuestId);
                            }
                        }

                        giveUpList.ForEach(giveUp => { partyMemberClient.Tamer.Progress.RemoveQuest(giveUp); });
                    }
                }
            }

            partyIdList.Clear();
        }

        private void ItemsReward(GameMap map, SummonMobModel mob)
        {
            if (mob.DropReward == null)
                return;

            QuestDropReward(map, mob);

            if (mob.Class == 8)
                RaidReward(map, mob);
            else
                DropReward(map, mob);
        }

        private void ExperienceReward(GameMap map, SummonMobModel mob)
        {
            if (mob.ExpReward == null)
                return;

            var partyIdList = new List<int>();

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                var tamerExpToReceive = (long)(CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.TamerExperience)); //TODO: +bonus
                if (CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.TamerExperience) == 0)
                    tamerExpToReceive = 0;

                if (tamerExpToReceive > 100) tamerExpToReceive += UtilitiesFunctions.RandomInt(-35, 45);
                var tamerResult = ReceiveTamerExp(targetClient, targetClient.Tamer, tamerExpToReceive);

                var partnerExpToReceive = (long)(CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.DigimonExperience));



                if (CalculateExperience(tamer.Partner.Level, mob.Level, mob.ExpReward.DigimonExperience) == 0)
                    partnerExpToReceive = 0;

                if (partnerExpToReceive > 100) partnerExpToReceive += UtilitiesFunctions.RandomInt(-35, 45);
                var partnerResult = ReceivePartnerExp(targetClient, targetClient.Partner, mob, partnerExpToReceive);

                var totalTamerExp = BonusTamerExp(map, mob);

                var bonusTamerExp = ReceiveBonusTamerExp(targetClient, targetClient.Tamer, totalTamerExp);

                var totalPartnerExp = BonusPartnerExp(map, mob);

                var bonusPartnerExp = ReceiveBonusPartnerExp(targetClient, targetClient.Partner, mob, totalPartnerExp);

                targetClient.blockAchievement = false;

                targetClient.Send(
                    new ReceiveExpPacket(
                        tamerExpToReceive,
                        totalTamerExp,
                        targetClient.Tamer.CurrentExperience,
                        targetClient.Partner.GeneralHandler,
                        partnerExpToReceive,
                        totalPartnerExp,
                        targetClient.Partner.CurrentExperience,
                        targetClient.Partner.CurrentEvolution.SkillExperience
                    )
                );

                //TODO: importar o DMBase e tratar isso
                SkillExpReward(map, targetClient);

                if (tamerResult.LevelGain > 0 || partnerResult.LevelGain > 0)
                {
                    targetClient.Send(new UpdateStatusPacket(targetClient.Tamer));

                    map.BroadcastForTamerViewsAndSelf(targetClient.TamerId,
                        new UpdateMovementSpeedPacket(targetClient.Tamer).Serialize());
                }

                _sender.Send(new UpdateCharacterExperienceCommand(tamer));
                _sender.Send(new UpdateDigimonExperienceCommand(tamer.Partner));

                PartyExperienceReward(map, mob, partyIdList, targetClient, tamerExpToReceive, tamerResult, partnerExpToReceive, partnerResult);
            }

            partyIdList.Clear();
        }

        private void DropReward(GameMap map, SummonMobModel mob)
        {
            var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == mob.TargetTamer?.Id);
            if (targetClient == null)
                return;

            BitDropReward(map, mob, targetClient);

            ItemDropReward(map, mob, targetClient);
        }


        private void BitDropReward(GameMap map, SummonMobModel mob, GameClient? targetClient)
        {
            var bitsReward = mob.DropReward.BitsDrop;

            if (bitsReward != null && bitsReward.Chance >= UtilitiesFunctions.RandomDouble())
            {
                if (targetClient.Tamer.HasAura && targetClient.Tamer.Aura.ItemInfo.Section == 2100)
                {
                    var amount = UtilitiesFunctions.RandomInt(bitsReward.MinAmount, bitsReward.MaxAmount);

                    targetClient.Send(
                        new PickBitsPacket(
                            targetClient.Tamer.GeneralHandler,
                            amount
                        )
                    );

                    targetClient.Tamer.Inventory.AddBits(amount);

                    _sender.Send(new UpdateItemsCommand(targetClient.Tamer.Inventory));
                    _sender.Send(new UpdateItemListBitsCommand(targetClient.Tamer.Inventory.Id, targetClient.Tamer.Inventory.Bits));
                }
                else
                {
                    var drop = _dropManager.CreateBitDrop(
                        targetClient.TamerId,
                        targetClient.Tamer.GeneralHandler,
                        bitsReward.MinAmount,
                        bitsReward.MaxAmount,
                        mob.CurrentLocation.MapId,
                        mob.CurrentLocation.X,
                        mob.CurrentLocation.Y
                    );

                    map.AddMapDrop(drop);
                }
            }
        }

        // ==================================================================================

        private void ItemDropReward(GameMap map, SummonMobModel mob, GameClient? targetClient)
        {
            if (!mob.DropReward.Drops.Any())
                return;

            var itemsReward = new List<SummonMobItemDropModel>();
            itemsReward.AddRange(mob.DropReward.Drops);
            itemsReward.RemoveAll(x => _assets.QuestItemList.Contains(x.ItemId));
            int vipMultiplier = targetClient.AccessLevel switch
            {
                Commons.Enums.Account.AccountAccessLevelEnum.Vip1 => 1,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip2 => 3,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip3 => 4,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip4 => 5,
                Commons.Enums.Account.AccountAccessLevelEnum.Vip5 => 6,
                Commons.Enums.Account.AccountAccessLevelEnum.Administrator => 10,
                _ => 1
            };
            if (!itemsReward.Any())
                return;

            var dropped = 0;
            var totalDrops = UtilitiesFunctions.RandomInt(mob.DropReward.MinAmount, mob.DropReward.MaxAmount);

            while (dropped < totalDrops)
            {
                if (!itemsReward.Any())
                {
                    _logger.Warning($"Mob {mob.Id} has incorrect drops configuration. (DungeonServer)");
                    _logger.Warning($"MinAmount {mob.DropReward.MinAmount} | MaxAmount {mob.DropReward.MaxAmount}");
                    break;
                }

                var possibleDrops = itemsReward.OrderBy(x => Guid.NewGuid()).ToList();
                foreach (var itemDrop in possibleDrops)
                {
                    if (itemDrop.Chance >= UtilitiesFunctions.RandomDouble())
                    {
                        // Create an item instance to check its section
                        var itemInfo = _assets.ItemInfo.GetValueOrDefault(itemDrop.ItemId);
                        if (itemInfo == null)
                            continue;

                        // Check if this item should be auto-collected based on its section and magnetic settings
                        bool shouldAutoCollect = ShouldAutoCollectItem(targetClient, itemInfo.Section);

                        if (targetClient.Tamer.HasAura && targetClient.Tamer.Aura.ItemInfo.Section == 2100 && shouldAutoCollect)
                        {
                            var newItem = new ItemModel();
                            newItem.SetItemInfo(itemInfo);

                            if (newItem.ItemInfo == null)
                            {
                                _logger.Warning(
                                    $"No item info found with ID {itemDrop.ItemId} for tamer {targetClient.Tamer.Id}.");
                                targetClient.Send(
                                    new SystemMessagePacket($"No item info found with ID {itemDrop.ItemId}."));
                                continue;
                            }

                            newItem.ItemId = itemDrop.ItemId;
                            newItem.Amount = UtilitiesFunctions.RandomInt(itemDrop.MinAmount * vipMultiplier, itemDrop.MaxAmount * vipMultiplier);

                            var itemClone = (ItemModel)newItem.Clone();
                            if (targetClient.Tamer.Inventory.AddItem(newItem))
                            {
                                targetClient.Send(new ReceiveItemPacket(itemClone, InventoryTypeEnum.Inventory));
                                _sender.Send(new UpdateItemsCommand(targetClient.Tamer.Inventory));
                                _logger.Verbose(
                                    $"Character {targetClient.TamerId} acquired {newItem.ItemId} x{newItem.Amount} from " +
                                    $"mob {mob.Id} with magnetic aura {targetClient.Tamer.Aura.ItemId}.");
                            }
                            else
                            {
                                targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));

                                var drop = _dropManager.CreateItemDrop(
                                    targetClient.Tamer.Id,
                                    targetClient.Tamer.GeneralHandler,
                                    itemDrop.ItemId,
                                    itemDrop.MinAmount * vipMultiplier,
                                    itemDrop.MaxAmount * vipMultiplier,
                                    mob.CurrentLocation.MapId,
                                    mob.CurrentLocation.X,
                                    mob.CurrentLocation.Y
                                );

                                map.AddMapDrop(drop);
                            }
                        }
                        else
                        {
                            var drop = _dropManager.CreateItemDrop(
                                targetClient.Tamer.Id,
                                targetClient.Tamer.GeneralHandler,
                                itemDrop.ItemId,
                                itemDrop.MinAmount * vipMultiplier,
                                itemDrop.MaxAmount * vipMultiplier,
                                mob.CurrentLocation.MapId,
                                mob.CurrentLocation.X,
                                mob.CurrentLocation.Y
                            );

                            map.AddMapDrop(drop);
                        }

                        dropped++;
                        itemsReward.RemoveAll(x => x.Id == itemDrop.Id);
                        break;
                    }
                }
            }
        }
        // Helper method to determine if an item should be auto-collected based on its section
        private bool ShouldAutoCollectItem(GameClient client, int itemSection)
        {
            // Check for Cracked items (sections 8000 & 9100)
            if ((itemSection == 8000 || itemSection == 9100) && !client.Tamer.MagneticCracked)
                return false;

            // Check for Attribute items (sections 12200, 12700, 12300, 12400, 17000)
            if ((itemSection == 12200 || itemSection == 12700 || itemSection == 12300 ||
                 itemSection == 12400 || itemSection == 17000) && !client.Tamer.MagneticAttribute)
                return false;

            // Check for Gear items (sections 2901, 2902, 17000, 3001, 3002)
            if ((itemSection == 2901 || itemSection == 2902 || itemSection == 17000 ||
                 itemSection == 3001 || itemSection == 3002) && !client.Tamer.MagneticGear)
                return false;

            // Check for DigiEggs items (sections 9200, 9300, 9400, 9100)
            if ((itemSection == 9200 || itemSection == 9300 || itemSection == 9400 ||
                 itemSection == 9100) && !client.Tamer.MagneticDigiEggs)
                return false;

            // Check for Seal items (section 19000)
            if (itemSection == 19000 && !client.Tamer.MagneticSeal)
                return false;

            // If we get here, the item should be auto-collected
            return true;
        }

        private SummonMobItemDropModel? GetRandomItem(List<SummonMobItemDropModel> itemsReward)
        {
            if (!itemsReward.Any())
                return null;

            return itemsReward.OrderBy(x => UtilitiesFunctions.RandomDouble()).FirstOrDefault();
        }

        private bool TryAddItemToInventory(GameMap map, SummonMobModel mob, GameClient targetClient, SummonMobItemDropModel itemDrop)
        {
            var newItem = new ItemModel();
            newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(itemDrop.ItemId));

            if (newItem.ItemInfo == null)
            {
                _logger.Warning($"No item info found with ID {itemDrop.ItemId} for tamer {targetClient.Tamer.Id}.");
                return false;
            }

            newItem.ItemId = itemDrop.ItemId;
            newItem.Amount = UtilitiesFunctions.RandomInt(itemDrop.MinAmount, itemDrop.MaxAmount);

            if (newItem.IsTemporary)
                newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

            var itemClone = (ItemModel)newItem.Clone();

            if (targetClient.Tamer.Inventory.AddItem(newItem))
            {
                targetClient.Send(new ReceiveItemPacket(itemClone, InventoryTypeEnum.Inventory));
                return true;
            }
            else
            {
                targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));

                var drop = _dropManager.CreateItemDrop(
                    targetClient.Tamer.Id,
                    targetClient.Tamer.GeneralHandler,
                    itemDrop.ItemId,
                    itemDrop.MinAmount,
                    itemDrop.MaxAmount,
                    mob.CurrentLocation.MapId,
                    mob.CurrentLocation.X,
                    mob.CurrentLocation.Y
                );

                map.AddMapDrop(drop);
            }

            return false;
        }

        // ==================================================================================

        private void QuestDropReward(GameMap map, SummonMobModel mob)
        {
            var itemsReward = new List<SummonMobItemDropModel>();
            itemsReward.AddRange(mob.DropReward.Drops);
            itemsReward.RemoveAll(x => !_assets.QuestItemList.Contains(x.ItemId));

            if (!itemsReward.Any())
                return;

            foreach (var tamer in mob.TargetTamers)
            {
                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamer?.Id);
                if (targetClient == null)
                    continue;

                if (!tamer.Progress.InProgressQuestData.Any())
                    continue;

                var updateItemList = false;
                var possibleDrops = itemsReward.Randomize();
                foreach (var itemDrop in possibleDrops)
                {
                    if (itemDrop.Chance >= UtilitiesFunctions.RandomDouble())
                    {
                        foreach (var questInProgress in tamer.Progress.InProgressQuestData)
                        {
                            var questInfo = _assets.Quest.FirstOrDefault(x => x.QuestId == questInProgress.QuestId);
                            if (questInfo != null)
                            {
                                if (!questInfo.QuestGoals.Exists(x => x.GoalType == QuestGoalTypeEnum.LootItem))
                                    continue;

                                var goalIndex = -1;
                                foreach (var questGoal in questInfo.QuestGoals)
                                {
                                    if (questGoal.GoalId == itemDrop?.ItemId)
                                    {
                                        var inventoryItems = tamer.Inventory.FindItemsById(questGoal.GoalId);
                                        var goalAmount = questGoal.GoalAmount;

                                        foreach (var inventoryItem in inventoryItems)
                                        {
                                            goalAmount -= inventoryItem.Amount;
                                            if (goalAmount <= 0)
                                            {
                                                goalAmount = 0;
                                                break;
                                            }
                                        }

                                        if (goalAmount > 0)
                                        {
                                            goalIndex = questInfo.QuestGoals.FindIndex(x => x == questGoal);
                                            break;
                                        }
                                    }
                                }

                                if (goalIndex != -1)
                                {
                                    var newItem = new ItemModel();
                                    newItem.SetItemInfo(
                                        _assets.ItemInfo.GetValueOrDefault(itemDrop.ItemId));

                                    if (newItem.ItemInfo == null)
                                    {
                                        _logger.Warning(
                                            $"No item info found with ID {itemDrop.ItemId} for tamer {tamer.Id}.");
                                        targetClient.Send(
                                            new SystemMessagePacket($"No item info found with ID {itemDrop.ItemId}."));
                                        continue;
                                    }

                                    newItem.ItemId = itemDrop.ItemId;
                                    newItem.Amount =
                                        UtilitiesFunctions.RandomInt(itemDrop.MinAmount, itemDrop.MaxAmount);

                                    var itemClone = (ItemModel)newItem.Clone();
                                    if (tamer.Inventory.AddItem(newItem))
                                    {
                                        updateItemList = true;
                                        targetClient.Send(new ReceiveItemPacket(itemClone,
                                            InventoryTypeEnum.Inventory));
                                    }
                                    else
                                    {
                                        targetClient.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                    }
                                }
                            }
                            else
                            {
                                _logger.Error($"Unknown quest id {questInProgress.QuestId}.");
                                targetClient.Send(
                                    new SystemMessagePacket($"Unknown quest id {questInProgress.QuestId}."));
                            }
                        }

                        if (updateItemList) _sender.Send(new UpdateItemsCommand(tamer.Inventory));

                        itemsReward.RemoveAll(x => x.Id == itemDrop.Id);
                    }
                }
            }
        }

        private int SkillExperienceTable(int evolutionType, int SkillMastery)
        {
            var RockieExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 281),
                new Tuple<int, int>(1, 315),
                new Tuple<int, int>(2, 352),
                new Tuple<int, int>(3, 395),
                new Tuple<int, int>(4, 442),
                new Tuple<int, int>(5, 495),
                new Tuple<int, int>(6, 555),
                new Tuple<int, int>(7, 621),
                new Tuple<int, int>(8, 696),
                new Tuple<int, int>(9, 779),
                new Tuple<int, int>(10, 873),
                new Tuple<int, int>(11, 977),
                new Tuple<int, int>(12, 1095),
                new Tuple<int, int>(13, 1226),
                new Tuple<int, int>(14, 1373),
                new Tuple<int, int>(15, 1538),
                new Tuple<int, int>(16, 1722),
                new Tuple<int, int>(17, 1930),
                new Tuple<int, int>(18, 2160),
                new Tuple<int, int>(19, 2420),
                new Tuple<int, int>(20, 2710),
                new Tuple<int, int>(21, 3036),
                new Tuple<int, int>(22, 3400),
                new Tuple<int, int>(23, 3808),
                new Tuple<int, int>(24, 4264),
                new Tuple<int, int>(25, 4776),
                new Tuple<int, int>(26, 5350),
                new Tuple<int, int>(27, 5992),
                new Tuple<int, int>(28, 6712),
                new Tuple<int, int>(29, 7516),
                new Tuple<int, int>(30, 8418)
            };

            var ChampionExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 621),
                new Tuple<int, int>(1, 696),
                new Tuple<int, int>(2, 779),
                new Tuple<int, int>(3, 872),
                new Tuple<int, int>(4, 977),
                new Tuple<int, int>(5, 1095),
                new Tuple<int, int>(6, 1226),
                new Tuple<int, int>(7, 1374),
                new Tuple<int, int>(8, 1538),
                new Tuple<int, int>(9, 1722),
                new Tuple<int, int>(10, 1930),
                new Tuple<int, int>(11, 2160),
                new Tuple<int, int>(12, 2420),
                new Tuple<int, int>(13, 2710),
                new Tuple<int, int>(14, 3036),
                new Tuple<int, int>(15, 3400),
                new Tuple<int, int>(16, 3808),
                new Tuple<int, int>(17, 4264),
                new Tuple<int, int>(18, 4776),
                new Tuple<int, int>(19, 5350),
                new Tuple<int, int>(20, 5992),
                new Tuple<int, int>(21, 6712),
                new Tuple<int, int>(22, 7516),
                new Tuple<int, int>(23, 8418),
                new Tuple<int, int>(24, 9428),
                new Tuple<int, int>(25, 10560),
                new Tuple<int, int>(26, 11828),
                new Tuple<int, int>(27, 13246),
                new Tuple<int, int>(28, 14386),
                new Tuple<int, int>(29, 16616),
                new Tuple<int, int>(30, 18610)
            };

            var UltimateExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 3036),
                new Tuple<int, int>(1, 3400),
                new Tuple<int, int>(2, 3808),
                new Tuple<int, int>(3, 4264),
                new Tuple<int, int>(4, 4776),
                new Tuple<int, int>(5, 5350),
                new Tuple<int, int>(6, 5992),
                new Tuple<int, int>(7, 6712),
                new Tuple<int, int>(8, 7516),
                new Tuple<int, int>(9, 8418),
                new Tuple<int, int>(10, 9428),
                new Tuple<int, int>(11, 10560),
                new Tuple<int, int>(12, 11828),
                new Tuple<int, int>(13, 13246),
                new Tuple<int, int>(14, 14836),
                new Tuple<int, int>(15, 16616),
                new Tuple<int, int>(16, 18610),
                new Tuple<int, int>(17, 20844),
                new Tuple<int, int>(18, 23344),
                new Tuple<int, int>(19, 26145),
                new Tuple<int, int>(20, 29283),
                new Tuple<int, int>(21, 32798),
                new Tuple<int, int>(22, 36734),
                new Tuple<int, int>(23, 41142),
                new Tuple<int, int>(24, 46078),
                new Tuple<int, int>(25, 51608),
                new Tuple<int, int>(26, 57800),
                new Tuple<int, int>(27, 64736),
                new Tuple<int, int>(28, 72504),
                new Tuple<int, int>(29, 81206),
                new Tuple<int, int>(30, 90950)
            };

            var MegaExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 18610),
                new Tuple<int, int>(1, 20844),
                new Tuple<int, int>(2, 23344),
                new Tuple<int, int>(3, 26145),
                new Tuple<int, int>(4, 29283),
                new Tuple<int, int>(5, 32798),
                new Tuple<int, int>(6, 36734),
                new Tuple<int, int>(7, 41142),
                new Tuple<int, int>(8, 46078),
                new Tuple<int, int>(9, 51608),
                new Tuple<int, int>(10, 57800),
                new Tuple<int, int>(11, 64736),
                new Tuple<int, int>(12, 72504),
                new Tuple<int, int>(13, 81206),
                new Tuple<int, int>(14, 90950),
                new Tuple<int, int>(15, 101864),
                new Tuple<int, int>(16, 114088),
                new Tuple<int, int>(17, 127778),
                new Tuple<int, int>(18, 143112),
                new Tuple<int, int>(19, 160286),
                new Tuple<int, int>(20, 179520),
                new Tuple<int, int>(21, 201062),
                new Tuple<int, int>(22, 225190),
                new Tuple<int, int>(23, 252212),
                new Tuple<int, int>(24, 282478),
                new Tuple<int, int>(25, 316374),
                new Tuple<int, int>(26, 354340),
                new Tuple<int, int>(27, 396860),
                new Tuple<int, int>(28, 444484),
                new Tuple<int, int>(29, 497822),
                new Tuple<int, int>(30, 557560)
            };

            var JogressExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 57800),
                new Tuple<int, int>(1, 64736),
                new Tuple<int, int>(2, 72504),
                new Tuple<int, int>(3, 81206),
                new Tuple<int, int>(4, 90950),
                new Tuple<int, int>(5, 101864),
                new Tuple<int, int>(6, 114088),
                new Tuple<int, int>(7, 127778),
                new Tuple<int, int>(8, 143112),
                new Tuple<int, int>(9, 160286),
                new Tuple<int, int>(10, 179520),
                new Tuple<int, int>(11, 201062),
                new Tuple<int, int>(12, 225190),
                new Tuple<int, int>(13, 252212),
                new Tuple<int, int>(14, 282478),
                new Tuple<int, int>(15, 316374),
                new Tuple<int, int>(16, 354340),
                new Tuple<int, int>(17, 396860),
                new Tuple<int, int>(18, 444484),
                new Tuple<int, int>(19, 497822),
                new Tuple<int, int>(20, 557560),
                new Tuple<int, int>(21, 624468),
                new Tuple<int, int>(22, 699404),
                new Tuple<int, int>(23, 783332),
                new Tuple<int, int>(24, 877332),
                new Tuple<int, int>(25, 982612),
                new Tuple<int, int>(26, 1100524),
                new Tuple<int, int>(27, 1232588),
                new Tuple<int, int>(28, 1380497),
                new Tuple<int, int>(29, 1546158),
                new Tuple<int, int>(30, 1731696)
            };

            var BurstModeExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 57800),
                new Tuple<int, int>(1, 64736),
                new Tuple<int, int>(2, 72504),
                new Tuple<int, int>(3, 81206),
                new Tuple<int, int>(4, 90950),
                new Tuple<int, int>(5, 101864),
                new Tuple<int, int>(6, 114088),
                new Tuple<int, int>(7, 127778),
                new Tuple<int, int>(8, 143112),
                new Tuple<int, int>(9, 160286),
                new Tuple<int, int>(10, 179520),
                new Tuple<int, int>(11, 201062),
                new Tuple<int, int>(12, 225190),
                new Tuple<int, int>(13, 252212),
                new Tuple<int, int>(14, 282478),
                new Tuple<int, int>(15, 316374),
                new Tuple<int, int>(16, 354340),
                new Tuple<int, int>(17, 396860),
                new Tuple<int, int>(18, 444484),
                new Tuple<int, int>(19, 497822),
                new Tuple<int, int>(20, 557560),
                new Tuple<int, int>(21, 624468),
                new Tuple<int, int>(22, 699404),
                new Tuple<int, int>(23, 783332),
                new Tuple<int, int>(24, 877332),
                new Tuple<int, int>(25, 982612),
                new Tuple<int, int>(26, 1100524),
                new Tuple<int, int>(27, 1232588),
                new Tuple<int, int>(28, 1380497),
                new Tuple<int, int>(29, 1546158),
                new Tuple<int, int>(30, 1731696)
            };

            var HybridExperienceTemp = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(0, 200),
                new Tuple<int, int>(1, 224),
                new Tuple<int, int>(2, 250),
                new Tuple<int, int>(3, 280),
                new Tuple<int, int>(4, 314),
                new Tuple<int, int>(5, 352),
                new Tuple<int, int>(6, 394),
                new Tuple<int, int>(7, 442),
                new Tuple<int, int>(8, 496),
                new Tuple<int, int>(9, 554),
                new Tuple<int, int>(10, 622),
                new Tuple<int, int>(11, 696),
                new Tuple<int, int>(12, 780),
                new Tuple<int, int>(13, 872),
                new Tuple<int, int>(14, 977),
                new Tuple<int, int>(15, 1095),
                new Tuple<int, int>(16, 1226),
                new Tuple<int, int>(17, 1374),
                new Tuple<int, int>(18, 1538),
                new Tuple<int, int>(19, 1722),
                new Tuple<int, int>(20, 1930),
                new Tuple<int, int>(21, 2160),
                new Tuple<int, int>(22, 2420),
                new Tuple<int, int>(23, 2710),
                new Tuple<int, int>(24, 3036),
                new Tuple<int, int>(25, 3400),
                new Tuple<int, int>(26, 3808),
                new Tuple<int, int>(27, 4264),
                new Tuple<int, int>(28, 4776),
                new Tuple<int, int>(29, 5350),
                new Tuple<int, int>(30, 5992)
            };

            switch ((EvolutionRankEnum)evolutionType)
            {
                case EvolutionRankEnum.RookieX:
                case EvolutionRankEnum.Rookie:
                    return RockieExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.ChampionX:
                case EvolutionRankEnum.Champion:
                    return ChampionExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.UltimateX:
                case EvolutionRankEnum.Ultimate:
                    return UltimateExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.MegaX:
                case EvolutionRankEnum.Mega:
                    return MegaExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.BurstModeX:
                case EvolutionRankEnum.BurstMode:
                    return BurstModeExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.JogressX:
                case EvolutionRankEnum.Jogress:
                    return JogressExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.Capsule:
                    return HybridExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.Spirit:
                    return HybridExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                case EvolutionRankEnum.Extra:
                    return HybridExperienceTemp.FirstOrDefault(x => x.Item1 == SkillMastery)?.Item2 ?? -1;

                default:
                    break;
            }

            return -1;
        }
    }
}