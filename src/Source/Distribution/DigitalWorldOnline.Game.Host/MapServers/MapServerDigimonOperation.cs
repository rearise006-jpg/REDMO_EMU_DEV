using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Models.Map;
using System.Diagnostics;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Application.Separar.Commands.Update;
namespace DigitalWorldOnline.GameHost
{
    // summary>
    /// Handles periodic Digimon operations on the map, such as auto-regeneration,
    /// buff effects, and synchronization of Digimon status with connected clients.
    // /summary>

    public sealed partial class MapServer
    {
        public void DigimonOperation(GameMap map)
        {
            // Igual que TamerOperation → early exit
            if (!map.ConnectedTamers.Any())
                return;

            var sw = Stopwatch.StartNew();

            foreach (var tamer in map.ConnectedTamers)
            {
                var client = map.Clients.FirstOrDefault(x => x.TamerId == tamer.Id);
                if (client?.IsConnected != true || client.Partner == null)
                    continue;

                var partner = client.Partner;

                ProcessDigimonNarrowEscape(partner, client, tamer, map);
                //ProcessDigimonBuffs(partner, client, tamer, map); -- not implemented yet
                ProcessDigimonEvolutionReset(partner, tamer, client, map);
            }

            sw.Stop();

            if (sw.ElapsedMilliseconds >= 1000)
                Console.WriteLine($"DigimonOperation ({map.ConnectedTamers.Count}): {sw.Elapsed.TotalMilliseconds}ms");
        }


        private void ProcessDigimonNarrowEscape(DigimonModel partner, GameClient client, CharacterModel tamer, GameMap map)
        {
            var narrowBuff = partner.BuffList.ActiveBuffs
                .FirstOrDefault(b => b.BuffInfo?.Class == 534);

            if (narrowBuff == null)
                return;

            double threshold = partner.HP * 0.30;
            if (partner.CurrentHp > threshold)
                return;

            int healAmount = (int)(partner.HP * 0.70);

            Console.WriteLine($"[DigimonTick] NarrowEscape triggered → Heal {healAmount}");

            partner.RecoverHp(healAmount);

            client.Send(new UpdateCurrentResourcesPacket(
                partner.GeneralHandler,
                (short)partner.CurrentHp,
                (short)partner.CurrentDs,
                0));

            map.BroadcastForTamerViewsAndSelf(
                tamer.Id,
                new DigimonSkillMemoryEffectSync(
                    narrowBuff.BuffInfo.SkillCode,
                    partner.GeneralHandler
                ).Serialize()
            );

            partner.BuffList.Remove(narrowBuff.BuffId);

            map.BroadcastForTamerViewsAndSelf(
                tamer.Id,
                new RemoveBuffPacket(
                    partner.GeneralHandler,
                    narrowBuff.BuffId
                ).Serialize()
            );
        }

        private void ProcessDigimonBuffs(DigimonModel partner,GameClient client,CharacterModel tamer,GameMap map)
        {
            // Aquí irán futuros buff ticks, DoT, HoT, etc.
        }

        private void ProcessDigimonEvolutionReset(
            DigimonModel partner,
            CharacterModel tamer,
            GameClient client,
            GameMap map)
        {
            // Solo ocurre si la evolución se rompió
            if (!tamer.BreakEvolution)
                return;

            // Reset recursos de la evolución
            tamer.ActiveEvolution.SetDs(0);
            tamer.ActiveEvolution.SetXg(0);

            // Si estaba montado bajarlo
            if (tamer.Riding)
            {
                tamer.StopRideMode();

                BroadcastForTamerViewsAndSelf(
                    tamer.Id,
                    new UpdateMovementSpeedPacket(tamer).Serialize());

                BroadcastForTamerViewsAndSelf(
                    tamer.Id,
                    new RideModeStopPacket(
                        tamer.GeneralHandler,
                        partner.GeneralHandler
                    ).Serialize());
            }

            // Remover buff base del tamer
            var baseBuff = partner.BuffList.TamerBaseSkill();
            if (baseBuff != null)
            {
                BroadcastForTamerViewsAndSelf(
                    tamer.Id,
                    new RemoveBuffPacket(partner.GeneralHandler, baseBuff.BuffId).Serialize());
            }

            // Remover passive buff
            client.Tamer.RemovePartnerPassiveBuff();

            // Notificar evolución revertida (efecto visual)
            map.BroadcastForTamerViewsAndSelf(
                tamer.Id,
                new DigimonEvolutionSucessPacket(
                    tamer.GeneralHandler,
                    partner.GeneralHandler,
                    partner.BaseType,
                    DigimonEvolutionEffectEnum.Back
                ).Serialize());

            // Guardar stats actuales antes del reset
            int oldHp = partner.CurrentHp;
            int oldMaxHp = partner.HP;
            int oldDs = partner.CurrentDs;
            int oldMaxDs = partner.DS;

            // Resetear a la forma base
            partner.UpdateCurrentType(partner.BaseType);

            partner.SetBaseInfo(_statusManager.GetDigimonBaseInfo(partner.CurrentType));

            partner.SetBaseStatus(_statusManager.GetDigimonBaseStatus(
                partner.CurrentType,
                partner.Level,
                partner.Size));

            // Reaplicar passive buff
            client.Tamer.SetPartnerPassiveBuff();

            // Ajustar HP/DS manteniendo porcentaje
            partner.AdjustHpAndDs(oldHp, oldMaxHp, oldDs, oldMaxDs);

            // Restaurar info de buff
            foreach (var buff in partner.BuffList.ActiveBuffs)
            {
                buff.SetBuffInfo(
                    _assets.BuffInfo.FirstOrDefault(x =>
                        (x.SkillCode == buff.SkillId || x.DigimonSkillCode == buff.SkillId)
                        && buff.BuffInfo == null));
            }

            // Important:   
            // Do not send UpdateStatusPacket here
            // ProcessSyncResources process it

            // Reaplicar buff base (si existe)
            var zeroDur = partner.BuffList.Buffs.Where(x => x.Duration == 0).ToList();
            zeroDur.ForEach(b =>
            {
                BroadcastForTamerViewsAndSelf(
                    tamer.Id,
                    new AddBuffPacket(
                        partner.GeneralHandler,
                        b.BuffId,
                        b.SkillId,
                        (short)b.TypeN,
                        0
                    ).Serialize());
            });

            // Actualizar info del party visualmente
            var party = _partyManager.FindParty(client.TamerId);
            if (party != null)
            {
                party.UpdateMember(party[client.TamerId], client.Tamer);

                BroadcastForTargetTamers(
                    party.GetMembersIdList(),
                    new PartyMemberInfoPacket(party[client.TamerId]).Serialize());
            }

            // Persistencia DB
            _sender.Send(new UpdatePartnerCurrentTypeCommand(partner));
            _sender.Send(new UpdateCharacterActiveEvolutionCommand(tamer.ActiveEvolution));
            _sender.Send(new UpdateDigimonBuffListCommand(partner.BuffList));
        }

    }
}


