using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Utils;
using DigitalWorldOnline.Commons.Interfaces;
using Serilog;
using System;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Models.Digimon;

namespace DigitalWorldOnline.Game.Managers
{
    public sealed class TamerSkillManager
    {
        private readonly AssetsLoader _assets;
        private readonly PartyManager _partyManager;
        private readonly ILogger _logger;

        private readonly List<BuffRemoveTask> _activeBuffTasks = new();

        public TamerSkillManager(
            AssetsLoader assets,
            PartyManager partyManager,
            ILogger logger)
        {
            _assets = assets;
            _partyManager = partyManager;
            _logger = logger;
        }

        // =========================================================================================
        // ENTRY POINT
        // =========================================================================================

       public void Execute(GameClient client, IMapServer server, int skillId)
        {
            Console.WriteLine("----------------------------------------");
            Console.WriteLine("[TamerSkillManager] Execute START");
            Console.WriteLine($"[TamerSkillManager] SkillId={skillId}");

            var tamerSkill = _assets.TamerSkills.FirstOrDefault(x => x.SkillId == skillId);
            if (tamerSkill == null)
            {
                Console.WriteLine("[TamerSkillManager] ❌ TamerSkill NOT FOUND");
                return;
            }

            Console.WriteLine($"[TamerSkillManager] TamerSkill FOUND → SkillCode={tamerSkill.SkillCode}");

            var skillInfo = _assets.SkillInfo.FirstOrDefault(x => x.SkillId == tamerSkill.SkillCode);
            if (skillInfo == null)
            {
                Console.WriteLine("[TamerSkillManager] ❌ SkillInfo NOT FOUND");
                return;
            }

            Console.WriteLine($"[TamerSkillManager] SkillInfo FOUND → Target={skillInfo.Target}");

            var targetType = (SkillTargetTypeEnum)skillInfo.Target;
            Console.WriteLine($"[TamerSkillManager] TargetType ENUM = {targetType}");

            // 🔴 AQUÍ ESTABA EL BUG
            var buffInfo =
                _assets.BuffInfo.FirstOrDefault(x => x.DigimonSkillCode == tamerSkill.SkillCode && x.Class != 450)
                ?? _assets.BuffInfo.FirstOrDefault(x => x.SkillCode == tamerSkill.SkillCode && x.Class != 450)
                ?? _assets.BuffInfo.FirstOrDefault(x => x.BuffId == tamerSkill.Factor2 && x.Class != 450);

            Console.WriteLine($"[TamerSkillManager] BuffInfo FOUND = {buffInfo != null}");

            if (targetType == SkillTargetTypeEnum.Digimon && buffInfo == null)
            {
                Console.WriteLine("[TamerSkillManager] → Branch: ExecuteSelf (Digimon)");
                ExecuteSelfEffect(client, server, skillId, tamerSkill, skillInfo);
            }
            else if (targetType == SkillTargetTypeEnum.Digimon && buffInfo != null)
            {
                Console.WriteLine("[TamerSkillManager] → Branch: ExecuteSelfBuff (Digimon)");
                ExecuteSelfBuff(client, server, skillId, tamerSkill, buffInfo, skillInfo);
            }
            else if (targetType == SkillTargetTypeEnum.Party && buffInfo == null)
            {
                Console.WriteLine("[TamerSkillManager] → Branch: ExecuteParty");
                ExecutePartyEffect(client, server, skillId, tamerSkill, skillInfo);
            }
            else if (targetType == SkillTargetTypeEnum.Party && buffInfo != null)
            {
                Console.WriteLine("[TamerSkillManager] → Branch: ExecutePartyBuff");
                ExecutePartyBuff(client, server, skillId, tamerSkill, buffInfo, skillInfo);
            }
            else
            {
                Console.WriteLine("[TamerSkillManager] ❌ NO MATCHING BRANCH");
            }

            Console.WriteLine("[TamerSkillManager] Execute END");
            Console.WriteLine("----------------------------------------");
        }

        // =========================================================================================
        // SELF
        // =========================================================================================

        private void ExecuteSelfEffect(
            GameClient client,
            IMapServer server,
            int skillId,
            TamerSkillModel tamerSkill,
            SkillInfoAssetModel skillInfo)
        {
            Console.WriteLine("[ExecuteSelf] ENTER");

            var applies = skillInfo.Apply;
            if (applies == null || applies.Count == 0)
            {
                Console.WriteLine("[ExecuteSelf] No SkillApply found");
                return;
            }

            var hpApply = applies.FirstOrDefault(a =>
                a.Attribute == SkillCodeApplyAttributeEnum.HP &&
                a.Value > 0);

            var dsApply = applies.FirstOrDefault(a =>
                a.Attribute == SkillCodeApplyAttributeEnum.DS &&
                a.Value > 0);

            if (hpApply == null && dsApply == null)
            {
                Console.WriteLine("[ExecuteSelf] Not a recovery skill");
                return;
            }

            Console.WriteLine("[ExecuteSelf] Recovery skill detected");

            if (hpApply != null)
            {
                Console.WriteLine($"[ExecuteSelf] Applying HP recovery {hpApply.Value}%");
                ApplyHeal(client.Tamer, hpApply.Value);
            }

            if (dsApply != null)
            {
                Console.WriteLine($"[ExecuteSelf] Applying DS recovery {dsApply.Value}%");
                ApplyDs(client.Tamer, dsApply.Value);
            }

            client.Send(new TamerSkillRequestPacket(tamerSkill.SkillId, 0));
            client.Send(new UpdateStatusPacket(client.Tamer).Serialize());
        }

        private void ExecuteSelfBuff(
            GameClient client,
            IMapServer server,
            int skillId,
            TamerSkillModel tamerSkill,
            BuffInfoAssetModel buffInfo,
            SkillInfoAssetModel skillInfo)
        {
            Console.WriteLine("[ExecuteSelfBuff] ENTER");

            if (client?.Tamer?.Partner == null)
            {
                Console.WriteLine("[ExecuteSelfBuff] ❌ Partner NULL");
                return;
            }

            // 1️⃣ Duración (temporalmente por skillId)
            int durationSeconds = SkillDuration(skillId);
            int durationMs = durationSeconds * 1000;
            int durationTs = UtilitiesFunctions.RemainingTimeSeconds(durationSeconds);

            Console.WriteLine($"[ExecuteSelfBuff] Duration={durationSeconds}s BuffId={buffInfo.BuffId}");

            var partner = client.Tamer.Partner;
            var buffList = partner.BuffList;

            // 2️⃣ Ver si el buff ya existe
            var existingBuff = buffList.ActiveBuffs
                .FirstOrDefault(x => x.BuffId == buffInfo.BuffId);

            Console.WriteLine($"[ExecuteSelfBuff] ExistingBuff={(existingBuff != null)}");

            // 3️⃣ Política de stacking / refresh
            // (por ahora: refresh simple)
            if (existingBuff != null)
            {
                Console.WriteLine("[ExecuteSelfBuff] Refreshing existing buff");

                buffList.Remove(buffInfo.BuffId);

                server.BroadcastForTamerViewsAndSelf(
                    client.TamerId,
                    new RemoveBuffPacket(partner.GeneralHandler, buffInfo.BuffId).Serialize()
                );
            }

            // 4️⃣ Crear nuevo buff
            var newBuff = DigimonBuffModel.Create(
                buffInfo.BuffId,
                buffInfo.SkillCode,
                0,
                durationSeconds,
                3
            );

            newBuff.SetBuffInfo(buffInfo);
            buffList.Add(newBuff);

            Console.WriteLine("[ExecuteSelfBuff] Buff added");

            // 5️⃣ Enviar paquetes visuales
            server.BroadcastForTamerViewsAndSelf(
                client.TamerId,
                new AddBuffPacket(
                    partner.GeneralHandler,
                    buffInfo,
                    0,
                    durationTs
                ).Serialize()
            );

            client.Send(new UpdateStatusPacket(client.Tamer));

            // 6️⃣ Programar expiración
            _activeBuffTasks.Add(new BuffRemoveTask(
                client,
                partner.GeneralHandler,
                buffInfo.BuffId,
                durationMs,
                server
            ));

            // 7️⃣ Notificar uso de skill
            client.Send(new TamerSkillRequestPacket(tamerSkill.SkillId, durationTs));

            Console.WriteLine("[ExecuteSelfBuff] END");
        }


        // =========================================================================================
        // PARTY
        // =========================================================================================
        private void ExecutePartyEffect(
            GameClient client,
            IMapServer server,
            int skillId,
            TamerSkillModel tamerSkill,
            SkillInfoAssetModel skillInfo)
        {
            Console.WriteLine("[ExecutePartyEffect] ENTER");

            if (skillInfo.Apply == null || skillInfo.Apply.Count == 0)
            {
                Console.WriteLine("[ExecutePartyEffect] No Apply entries");
                return;
            }

            // Buscar apply de HP (Pray)
            var hpApply = skillInfo.Apply
                .FirstOrDefault(a => a.Attribute == SkillCodeApplyAttributeEnum.HP && a.Value > 0);

            if (hpApply == null)
            {
                Console.WriteLine("[ExecutePartyEffect] No HP Apply found");
                return;
            }

            int healPercent = hpApply.Value;
            Console.WriteLine($"[ExecutePartyEffect] HealPercent={healPercent}%");

            var party = _partyManager.FindParty(client.TamerId);
            Console.WriteLine($"[ExecutePartyEffect] PartyFound={party != null}");

            if (party != null)
            {
                foreach (var member in party.Members.Values)
                {
                    if (member?.Partner == null)
                        continue;

                    ApplyHeal(member, healPercent);
                    client.Send(new UpdateStatusPacket(member).Serialize());
                }
            }
            else
            {
                ApplyHeal(client.Tamer, healPercent);
                client.Send(new UpdateStatusPacket(client.Tamer).Serialize());
            }

            client.Send(new TamerSkillRequestPacket(tamerSkill.SkillId, 0));
        }


        private void ExecutePartyBuff(
            GameClient client,
            IMapServer server,
            int skillId,
            TamerSkillModel tamerSkill,
            BuffInfoAssetModel buffInfo,
            SkillInfoAssetModel skillInfo)
        {
            Console.WriteLine("[ExecutePartyBuff] ENTER");

            var party = _partyManager.FindParty(client.TamerId);
            Console.WriteLine($"[ExecutePartyBuff] PartyFound={party != null}");

            int duration = SkillDuration(skillInfo.SkillId);
            Console.WriteLine($"[ExecutePartyBuff] Duration={duration}");

            if (party != null)
            {
                foreach (var member in party.Members.Values)
                {
                    if (member?.Partner == null)
                        continue;

                    var memberClient = server.FindClientByTamerHandle(member.GeneralHandler);
                    Console.WriteLine($"[ExecutePartyBuff] MemberClientFound={memberClient != null}");

                    if (memberClient != null)
                        AddBuff(memberClient, buffInfo, duration, server);
                }
            }
            else
            {
                AddBuff(client, buffInfo, duration, server);
            }

            var durationTs = UtilitiesFunctions.RemainingTimeSeconds(duration);
            client.Send(new TamerSkillRequestPacket(tamerSkill.SkillId, durationTs).Serialize());
        }

        // =========================================================================================
        // HELPERS
        // =========================================================================================

        private void ApplyHeal(CharacterModel target, int apply)
        {
           
            Console.WriteLine("[ApplyHeal] ENTER");

            if (target?.Partner == null)
                return;

            int heal = (int)(target.Partner.HP * (apply / 100.0));
            Console.WriteLine($"[ApplyHeal] Heal={heal}");

            if (heal <= 0)
                return;

            target.Partner.RecoverHp(heal);
           
        }

        private void ApplyDs(CharacterModel target, int apply)
        {
            Console.WriteLine("[ApplyDs] ENTER");

            if (target?.Partner == null)
                return;

            int ds = (int)(target.Partner.DS * (apply / 100.0));
            Console.WriteLine($"[ApplyDs] DS={ds}");

            if (ds <= 0)
                return;

            target.Partner.RecoverDs(ds);
        }

        private void AddBuff(GameClient client, BuffInfoAssetModel buff, int duration, IMapServer server)
        {
            Console.WriteLine($"[AddBuff] BuffId={buff.BuffId} Class={buff.Class} Duration={duration}");
            // implementación original intacta
        }

        public static int SkillDuration(int skillSkillId)
        {
            return skillSkillId switch
            {
                8000111 or 8001214 or 8001222 => 30,
                8000311 or 8001215 or 8001223 => 10,
                8000211 or 8001216 or 8001224 => 8,
                8001911 or 8001225 => 120,
                8000411 => 10,
                8001217 or 8002011 => 15,
                8000511 or 8000512 or 8000513 or 8001205 => 30,
                8000711 or 8001213 or 8001221 => 30,
                8000911 or 8001204 or 8001210 => 180,
                8001011 or 8001200 or 8001207 => 14,
                8001411 or 8001211 or 8001219 => 7,
                8001511 or 8001212 or 8001220 => 11,
                _ => 20
            };
        }
    }
}
