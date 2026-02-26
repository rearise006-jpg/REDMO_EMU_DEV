using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.Game.Managers;
using Serilog;
using DigitalWorldOnline.Application;
using MediatR;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Game.Utils;
using DigitalWorldOnline.Application.Separar.Commands.Update;


namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class MemorySkillService : IMemorySkillService
    {
        private readonly AssetsLoader _assets;
        private readonly PartyManager _partyManager;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ISender _sender;
        private readonly ILogger _logger;

        // Solo para trazabilidad si quieres inspeccionar tareas activas
        private readonly List<BuffRemoveTask> _activeBuffTasks = new();

        public MemorySkillService(
            AssetsLoader assets,
            PartyManager partyManager,
            MapServer mapServer,
            DungeonsServer dungeonServer,
            ISender sender,
            ILogger logger)
        {
            _assets = assets;
            _partyManager = partyManager;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _sender = sender;
            _logger = logger;
        }

        public Task HandleMemorySkillUseAsync(
            GameClient client,
            IMapServer server,
            BuffInfoAssetModel? buffInfo,
            SkillInfoAssetModel? skillInfo,
            int skillCode,
            int targetHandler)
        {
            //Console.WriteLine(
           //     $"[MemorySkillService] HandleMemorySkillUseAsync START - " +
           //     $"TamerId={client?.TamerId}, SkillCode={skillCode}, SkillId={(skillInfo != null ? skillInfo.SkillId.ToString() : "null")}, " +
           //     $"BuffId={(buffInfo != null ? buffInfo.BuffId.ToString() : "null")}, TargetHandler={targetHandler}"
           // );

            // 1) Sin BuffInfo → skill de ataque / daño (usa SkillApply como antes)
            if (buffInfo == null)
            {
               // Console.WriteLine($"[MemorySkillService] BuffInfo IS NULL -> fallback a AttackBuffMemoryAsync (attack-style). SkillCode={skillCode}");
                return AttackBuffMemoryAsync(client, server, null, skillInfo, targetHandler, 60);
            }

            // 2) SUPPORT OFICIAL: BuffClass 529–534
            if (buffInfo.Class >= 529 && buffInfo.Class <= 534)
            {
               // Console.WriteLine($"[MemorySkillService] Buff.Class={buffInfo.Class} (529-534) -> SupportBuffMemoryAsync");
                return SupportBuffMemoryAsync(client, server, buffInfo, targetHandler, 60);
            }

            // 3) Resto de buffs → clasificar como Defense o Attack según Apply
            var (isDefense, _, isAttack) = ClassifyBuff(buffInfo);

            //Console.WriteLine($"[MemorySkillService] Clasificación → Defense={isDefense}, SupportClass={(buffInfo.Class >= 529 && buffInfo.Class <= 534)}, Attack={isAttack} (Buff.Class={buffInfo.Class})");

            if (isDefense)
            {
                //Console.WriteLine($"[MemorySkillService] Dispatch -> DefenseBuffMemoryAsync (BuffId={buffInfo.BuffId})");
                return DefenseBuffMemoryAsync(client, server, buffInfo, targetHandler, 60);
            }

            if (isAttack)
            {
                //Console.WriteLine($"[MemorySkillService] Dispatch -> AttackBuffMemoryAsync (BuffId={buffInfo.BuffId})");
                return AttackBuffMemoryAsync(client, server, buffInfo, null, targetHandler, 60);
            }

            // 4) default: buff pasivo simple
            //Console.WriteLine($"[MemorySkillService] Ninguna clasificación match -> Aplicando AddBuff PASIVO (BuffId={buffInfo.BuffId})");
            AddBuff(client, buffInfo, 60, server);

           // Console.WriteLine($"[MemorySkillService] HandleMemorySkillUseAsync END - TamerId={client?.TamerId}, SkillCode={skillCode}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Clasifica SOLO para attack/defense. Support REAL se define por BuffClass (529-534).
        /// </summary>
        private (bool isDefense, bool isSupport, bool isAttack) ClassifyBuff(BuffInfoAssetModel buff)
        {
            var list = buff.SkillInfo?.Apply;

            if (list == null)
            {
                //Console.WriteLine("[ClassifyBuff] Apply list es NULL → No se puede clasificar.");
                return (false, false, false);
            }

            var isDefense = list.Any(x =>
                x.Attribute is SkillCodeApplyAttributeEnum.PROVOKE
                    or SkillCodeApplyAttributeEnum.HPPerDefence
                    or SkillCodeApplyAttributeEnum.AttriPerDefence);

            // Support lo controlamos por Class (529–534), aquí lo dejamos siempre false
            var isSupport = false;

            var isAttack = list.Any(x =>
                    x.Attribute is SkillCodeApplyAttributeEnum.SkillDamageByAttribute
                        or SkillCodeApplyAttributeEnum.HPPerDamage) ||
                list.Any(x => x.Attribute == SkillCodeApplyAttributeEnum.HP &&
                              x.Type == SkillCodeApplyTypeEnum.Unknown1);

           // Console.WriteLine($"[ClassifyBuff] Defense={isDefense}, Support={isSupport}, Attack={isAttack} (ApplyCount={list.Count})");

            return (isDefense, isSupport, isAttack);
        }

        private void ApplyHeal(CharacterModel target, SkillCodeApplyAssetModel apply, IMapServer server)
        {
            if (apply == null || target?.Partner == null)
                return;

            int heal = (int)(target.Partner.HP * (apply.Value / 100.0));
            if (heal <= 0)
                return;

            target.Partner.RecoverHp(heal);

            server.BroadcastForTamerViewsAndSelf(
                target.Id,
                new UpdateStatusPacket(target).Serialize()
            );
        }



        private void AddBuff(GameClient client, BuffInfoAssetModel buff, int durationSeconds, IMapServer server)
        {
            if (buff == null)
            {
                Console.WriteLine("[AddBuff] buff es NULL, cancelled");
                return;
            }

            // ==== SPEED BUFF / TAMER (Class 532) ====
            if (buff.Class == 532)
            {
                //Console.WriteLine($"[AddBuff] Aplicando Speed Buff (ID={buff.BuffId}) al Tamer. Duración={durationSeconds}s");

                durationSeconds = 30;

                if (client.Tamer.BuffList.ActiveBuffs.Any(x => x.BuffId == buff.BuffId))
                {
                    client.Tamer.BuffList.Remove(buff.BuffId);
                    server.BroadcastForTamerViewsAndSelf(
                        client.TamerId,
                        new RemoveBuffPacket(client.Tamer.GeneralHandler, buff.BuffId).Serialize());
                }

                var ts = UtilitiesFunctions.RemainingTimeSeconds(durationSeconds);
                var newBuff = CharacterBuffModel.Create(buff.BuffId, buff.SkillCode, 0, durationSeconds, 3);
                newBuff.SetBuffInfo(buff);

                client.Tamer.BuffList.Add(newBuff);

                server.BroadcastForTamerViewsAndSelf(
                    client.TamerId,
                    new AddBuffPacket(client.Tamer.GeneralHandler, buff, 0, ts).Serialize());

                server.BroadcastForTamerViewsAndSelf(
                    client.TamerId,
                    new UpdateMovementSpeedPacket(client.Tamer).Serialize());

                int durationMs = durationSeconds * 1000;

                var removeTask = new BuffRemoveTask(
                    client,
                    client.Tamer.GeneralHandler,     // handler del Tamer para Speed Buff
                    buff.BuffId,
                    durationMs,
                    server
                );

                _activeBuffTasks.Add(removeTask);

               // Console.WriteLine($"[AddBuff] Speed Buff aplicado correctamente al Tamer. BuffId={buff.BuffId}, DurationMs={durationMs}");
                return;
            }

           // ==== PARTNER BUFF ====
            //Console.WriteLine($"[AddBuff] Aplicando Partner Buff (ID={buff.BuffId}) al Digimon. Duración={durationSeconds}s");

            if (client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffId == buff.BuffId))
            {
                client.Partner.BuffList.Remove(buff.BuffId);
                server.BroadcastForTamerViewsAndSelf(
                    client.TamerId,
                    new RemoveBuffPacket(client.Partner.GeneralHandler, buff.BuffId).Serialize());
            }

            var tsPartner = UtilitiesFunctions.RemainingTimeSeconds(durationSeconds);
            var newPartnerBuff = DigimonBuffModel.Create(buff.BuffId, buff.SkillCode, 0, durationSeconds, 3);
            newPartnerBuff.SetBuffInfo(buff);

            client.Partner.BuffList.Add(newPartnerBuff);

            _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));

            client.Send(new UpdateStatusPacket(client.Tamer).Serialize());

            server.BroadcastForTamerViewsAndSelf(
                client.TamerId,
                new AddBuffPacket(client.Partner.GeneralHandler, buff, 0, tsPartner).Serialize());

            int durationMsPartner = durationSeconds * 1000;

            var partnerRemoveTask = new BuffRemoveTask(
                client,
                client.Partner.GeneralHandler,
                buff.BuffId,
                durationMsPartner,
                server
            );

            _activeBuffTasks.Add(partnerRemoveTask);

           // Console.WriteLine($"[AddBuff] Partner Buff aplicado correctamente. BuffId={buff.BuffId}, DurationMs={durationMsPartner}");

        }

        private Task SupportBuffMemoryAsync(GameClient client, IMapServer server, BuffInfoAssetModel buff, int targetHandler, int durationSeconds)
        {
            var applyList = buff.SkillInfo.Apply;
            var applyHp = applyList.FirstOrDefault(x => x.Attribute == SkillCodeApplyAttributeEnum.HP && x.Value > 0);
            var applyInsurance = applyList.FirstOrDefault(x => x.Attribute == SkillCodeApplyAttributeEnum.HPInsurance && x.Value > 0);

            var party = _partyManager.FindParty(client.TamerId);

            bool healEveryoneParty = buff.Class == 529;
            bool healPartyMember = buff.Class == 530;
            bool increaseHpAndDs = buff.Class == 531;
            bool increaseSpeedParty = buff.Class == 532;
            bool increaseEvasionParty = buff.Class == 533;
            bool narrowEscape = buff.Class == 534;

            //Console.WriteLine(
              //  $"[SupportBuff] Class={buff.Class} HealAll={healEveryoneParty} HealOne={healPartyMember} " +
              //  $"HPDS={increaseHpAndDs} Speed={increaseSpeedParty} Evasion={increaseEvasionParty} NarrowEscape={narrowEscape}"
            //);

            // 1) Heal everyone (Hand of Healing - 529)
            if (healEveryoneParty)
            {
               // Console.WriteLine("[SupportBuff] Effect=HealAllParty");

                if (party != null)
                {
                    foreach (var member in party.Members.Values)
                    {
                        if (member?.Partner == null) continue;

                        Console.WriteLine($"[SupportBuff] Healing member {member.Name} ({member.Id})");
                        ApplyHeal(member, applyHp, server);
                        server.BroadcastForTamerViewsAndSelf(
                            member.Id,
                            new DigimonSkillMemoryEffectSync(buff.SkillCode, member.Partner.GeneralHandler).Serialize());
                    }
                }
                else
                {
                    //Console.WriteLine("[SupportBuff] Healing self (no party)");
                    ApplyHeal(client.Tamer, applyHp, server);
                    server.BroadcastForTamerViewsAndSelf(
                        client.TamerId,
                        new DigimonSkillMemoryEffectSync(buff.SkillCode, client.Partner.GeneralHandler).Serialize());
                }

                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }

            // 2) Heal single party member (Regenerative Ability - 530)
            if (healPartyMember)
            {
                CharacterModel target = null;

                if (party != null)
                {
                    if (targetHandler != client.Partner.GeneralHandler)
                        target = party.Members.Values.FirstOrDefault(x => x.GeneralHandler == targetHandler);
                    else
                        target = client.Tamer;

                    if (target?.Partner != null)
                    {
                       // Console.WriteLine($"[SupportBuff] HealOneTarget={target.Name} Handler={target.GeneralHandler}");
                        ApplyHeal(target, applyHp, server);
                        server.BroadcastForTamerViewsAndSelf(
                            target.Id,
                            new DigimonSkillMemoryEffectSync(buff.SkillCode, target.Partner.GeneralHandler).Serialize());
                        client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                    }
                }
                else
                {
                   // Console.WriteLine("[SupportBuff] HealOne → Self (no party)");
                    ApplyHeal(client.Tamer, applyHp, server);
                    server.BroadcastForTamerViewsAndSelf(
                        client.TamerId,
                        new DigimonSkillMemoryEffectSync(buff.SkillCode, client.Partner.GeneralHandler).Serialize());
                    client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                }

                return Task.CompletedTask;
            }

            // 3) MaxHP+MaxDS (Tree of Life - 531)
            if (increaseHpAndDs)
            {
                //Console.WriteLine("[SupportBuff] Effect=HP+DS Increase (Tree of Life)");

                if (party != null)
                {
                    foreach (var member in party.Members.Values)
                    {
                        if (member?.Partner == null) continue;

                        var memberClient = server.FindClientByTamerHandle(member.GeneralHandler);
                        if (memberClient != null)
                        {
                            //Console.WriteLine($"[SupportBuff] Applying HP+DS buff to {member.Name}");
                            
                            //Console.WriteLine($"[TreeOfLife] BEFORE: HP={member.Partner.HP}, CurrentHP={member.Partner.CurrentHp}");
                            AddBuff(memberClient, buff, 300, server);
                            //Console.WriteLine($"[TreeOfLife] AFTER: HP={member.Partner.HP}, CurrentHP={member.Partner.CurrentHp}");


                            server.BroadcastForTamerViewsAndSelf(
                                member.Id,
                                new DigimonSkillMemoryEffectSync(buff.SkillCode, member.Partner.GeneralHandler).Serialize());
                        }
                    }
                }
                else
                {
                    //Console.WriteLine("[SupportBuff] Applying HP+DS buff to self");
                    //Console.WriteLine($"[TreeOfLife] BEFORE: HP={client.Partner.HP}, CurrentHP={client.Partner.CurrentHp}");
                    AddBuff(client, buff, 300, server);
                    //Console.WriteLine($"[TreeOfLife] AFTER(buff added): HP={client.Partner.HP}, CurrentHP={client.Partner.CurrentHp}");
                    server.BroadcastForTamerViewsAndSelf(
                        client.TamerId,
                        new DigimonSkillMemoryEffectSync(buff.SkillCode, client.Partner.GeneralHandler).Serialize());
                }

                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }

            // 4) Speed / Evasion (532 / 533)
            if (increaseSpeedParty || increaseEvasionParty)
            {
                var buffDuration = 30;
                //Console.WriteLine($"[SupportBuff] Effect={(increaseSpeedParty ? "Speed" : "Evasion")} Duration={buffDuration}");

                if (party != null)
                {
                    foreach (var member in party.Members.Values)
                    {
                        if (member?.Partner == null) continue;

                        var mc = server.FindClientByTamerHandle(member.GeneralHandler);
                        if (mc != null)
                        {
                            //Console.WriteLine($"[SupportBuff] Applying {(increaseSpeedParty ? "Speed" : "Evasion")} to {member.Name}");
                            AddBuff(mc, buff, buffDuration, server);
                            server.BroadcastForTamerViewsAndSelf(
                                member.Id,
                                new DigimonSkillMemoryEffectSync(buff.SkillCode, member.Partner.GeneralHandler).Serialize());
                        }
                    }
                }
                else
                {
                    //Console.WriteLine($"[SupportBuff] Applying {(increaseSpeedParty ? "Speed" : "Evasion")} to self");
                    AddBuff(client, buff, buffDuration, server);
                    server.BroadcastForTamerViewsAndSelf(
                        client.TamerId,
                        new DigimonSkillMemoryEffectSync(buff.SkillCode, client.Tamer.GeneralHandler).Serialize());
                }

                // Narrow Escape (534) se trata aparte abajo, no aquí

                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }
          // 5) Narrow Escape (Class 534)
            // ---------------------------------------------
            // IMPORTANTE: YA NO DISPARA CURACIÓN AQUÍ.
            // SOLO aplica el buff en espera.
            // La activación la maneja DigimonOperation cada 500ms.
            // ---------------------------------------------
            if (narrowEscape)
            {
                //Console.WriteLine("[SupportBuff] NarrowEscape → apply waiting buff only");

                // target correcto (party o self)
                CharacterModel target =
                    party?.Members.Values.FirstOrDefault(x => x.Partner.GeneralHandler == targetHandler)
                    ?? client.Tamer;

                var partner = target.Partner;
                if (partner == null)
                {
                    //Console.WriteLine("[SupportBuff] NarrowEscape aborted: partner NULL");
                    return Task.CompletedTask;
                }

                // Aplicar buff activo 10s (igual que skill original)
                AddBuff(client, buff, 10, server);

                server.BroadcastForTamerViewsAndSelf(
                    target.Id,
                    new DigimonSkillMemoryEffectSync(buff.SkillCode, partner.GeneralHandler).Serialize()
                );

                client.Send(new DigimonSkillMemoryCoolTimePacket(
                    buff.SkillCode,
                    10,
                    partner.GeneralHandler,
                    false
                ).Serialize());

                //Console.WriteLine("[SupportBuff] NarrowEscape buff ACTIVE (waiting trigger)");
                return Task.CompletedTask;
            }


            // 6) Insurance (HPInsurance)
            if (applyList.Any(x => x.Attribute == SkillCodeApplyAttributeEnum.HPInsurance && x.Value > 0))
            {
                CharacterModel target =
                    party?.Members.Values.FirstOrDefault(x => x.Partner.GeneralHandler == targetHandler)
                    ?? client.Tamer;

                if (target.Partner.CurrentHp <= target.Partner.HP * 0.30)
                {
                    //Console.WriteLine($"[SupportBuff] Insurance triggered for {target.Name}");
                    ApplyHeal(target, applyInsurance, server);
                    server.BroadcastForTamerViewsAndSelf(
                        target.Id,
                        new DigimonSkillMemoryEffectSync(buff.SkillCode, target.Partner.GeneralHandler).Serialize());
                }
                else
                {
                    //Console.WriteLine($"[SupportBuff] Insurance buff applied (no HP trigger yet) target={target.Name}");

                    if (client.Partner.GeneralHandler != targetHandler)
                    {
                        var newClient = server.FindClientByTamerHandle(target.GeneralHandler);
                        if (newClient != null) client = newClient;
                    }

                    AddBuff(client, buff, 10, server);
                }

                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }

            // 6) Fallback: buff pasivo estándar
            //Console.WriteLine("[SupportBuff] Fallback passive buff");
            AddBuff(client, buff, durationSeconds, server);
            return Task.CompletedTask;
        }

        private Task AttackBuffMemoryAsync(
            GameClient client,
            IMapServer server,
            BuffInfoAssetModel? buff,
            SkillInfoAssetModel? skill,
            int targetHandler,
            int durationSeconds)
        {
            if (buff != null)
            {
                AddBuff(client, buff, durationSeconds, server);
                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }

            var mob = FindTargetMob(client, targetHandler);
            if (mob == null) return Task.CompletedTask;

            TryStartBattle(client, mob, server, client.Partner.GeneralHandler, targetHandler);
            client.Send(new DigimonSkillMemoryCoolTimePacket(skill!.SkillId, 10, client.Partner.GeneralHandler, true).Serialize());
            return Task.CompletedTask;
        }

        private Task DefenseBuffMemoryAsync(
            GameClient client,
            IMapServer server,
            BuffInfoAssetModel buff,
            int targetHandler,
            int durationSeconds)
        {
            var applyList = buff.SkillInfo.Apply;
            var applyInsurance = applyList.FirstOrDefault(x => x.Attribute == SkillCodeApplyAttributeEnum.HPPerDefence && x.Value > 0);

            if (applyInsurance != null)
            {
                var target = client.Tamer;
                server.BroadcastForTamerViewsAndSelf(
                    target.Id,
                    new DigimonSkillMemoryEffectSync(buff.SkillCode, target.Partner.GeneralHandler).Serialize());
                AddBuff(client, buff, 60, server);
                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }

            if (!buff.SkillInfo.Apply.Any(x => x.Attribute == SkillCodeApplyAttributeEnum.PROVOKE))
            {
                AddBuff(client, buff, 7, server);
                server.BroadcastForTamerViewsAndSelf(
                    client.TamerId,
                    new DigimonSkillMemoryEffectSync(buff.SkillCode, client.Partner.GeneralHandler).Serialize());
                client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, client.Partner.GeneralHandler, false).Serialize());
                return Task.CompletedTask;
            }

            var mob = FindTargetMob(client, targetHandler);
            if (mob == null) return Task.CompletedTask;

            TryStartBattle(client, mob, server, client.Partner.GeneralHandler, targetHandler);
            client.Send(new DigimonSkillMemoryCoolTimePacket(buff.SkillCode, 10, targetHandler, true).Serialize());
            return Task.CompletedTask;
        }

        private IMob? FindTargetMob(GameClient client, int targetHandler)
        {
            if (client.DungeonMap)
            {
                var summon = _dungeonServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, true, 0);
                if (summon != null) return summon;

                return _dungeonServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, 0);
            }
            else
            {
                var summon = _mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, true, client.Tamer.Channel);
                if (summon != null) return summon;

                return _mapServer.GetMobByHandler(client.Tamer.Location.MapId, targetHandler, client.Tamer.Channel);
            }
        }

        private void TryStartBattle(GameClient client, IMob mob, IMapServer server, int attacker, int target)
        {
            client.Partner.SetEndAttacking();
            client.Tamer.SetHidden(false);

            if (!client.Tamer.InBattle)
            {
                server.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attacker).Serialize());
                client.Tamer.StartBattle(mob);
            }
            else
            {
                client.Tamer.UpdateTarget(mob);
            }

            if (!mob.InBattle)
            {
                server.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(target).Serialize());
                mob.StartBattle(client.Tamer);
            }
            else
            {
                mob.AddTarget(client.Tamer);
            }
        }
    }
}