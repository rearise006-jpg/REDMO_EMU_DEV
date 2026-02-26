using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.Game.Utils;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using System;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class TamerSkillRequestPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TamerSkillRequest;

        private readonly AssetsLoader _assets;
        private readonly PartyManager _partyManager;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly TamerSkillManager _tamerSkillManager;

        public TamerSkillRequestPacketProcessor(
            ILogger logger,
            ISender sender,
            AssetsLoader assets,
            PartyManager partyManager,
            MapServer mapserver,
            DungeonsServer dungeonServer,
            TamerSkillManager tamerSkillManager)
        {
            _logger = logger;
            _sender = sender;
            _assets = assets;
            _partyManager = partyManager;
            _mapServer = mapserver;
            _dungeonServer = dungeonServer;
            _tamerSkillManager = tamerSkillManager;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[TamerSkillRequest] PACKET RECEIVED");

            if (client == null)
            {
                Console.WriteLine("[TamerSkillRequest] ERROR: client is NULL");
                return;
            }

            Console.WriteLine($"[TamerSkillRequest] TamerId={client.TamerId} DungeonMap={client.DungeonMap}");

            if (packetData == null || packetData.Length == 0)
            {
                Console.WriteLine("[TamerSkillRequest] ERROR: packetData is NULL or empty");
                return;
            }

            Console.WriteLine($"[TamerSkillRequest] RawPacketLength={packetData.Length}");

            var packet = new GamePacketReader(packetData);

            int skillId;
            try
            {
                skillId = packet.ReadInt();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TamerSkillRequest] ERROR reading SkillId: {ex.Message}");
                return;
            }

            Console.WriteLine($"[TamerSkillRequest] SkillId READ = {skillId}");

            try
            {
                IMapServer server = client.DungeonMap ? _dungeonServer : _mapServer;
                Console.WriteLine($"[TamerSkillRequest] Server selected = {server.GetType().Name}");

                // 1) Ejecutar lógica de skill (heal/buff/etc)
                Console.WriteLine("[TamerSkillRequest] Calling TamerSkillManager.Execute(...)");
                _tamerSkillManager.Execute(client, server, skillId);
                Console.WriteLine("[TamerSkillRequest] Returned from TamerSkillManager.Execute");

                // 2) Resolver cooldown desde assets (por ahora sigue usando SkillInfo)
                int cooldownSeconds = 0;

                var tamerSkill = _assets.TamerSkills.FirstOrDefault(x => x.SkillId == skillId);
                if (tamerSkill == null)
                {
                    Console.WriteLine($"[TamerSkillRequest] WARNING: SkillId {skillId} NOT FOUND in TamerSkills");
                }
                else
                {
                    Console.WriteLine($"[TamerSkillRequest] TamerSkill found. SkillCode={tamerSkill.SkillCode}");

                    var skillInfo = _assets.SkillInfo.FirstOrDefault(x => x.SkillId == tamerSkill.SkillCode);
                    if (skillInfo == null)
                    {
                        Console.WriteLine($"[TamerSkillRequest] WARNING: SkillInfo NOT FOUND for SkillCode={tamerSkill.SkillCode}");
                    }
                    else
                    {
                        cooldownSeconds = (int)(skillInfo.Cooldown / 1000.0);
                        Console.WriteLine($"[TamerSkillRequest] Cooldown resolved from SkillInfo = {cooldownSeconds}s");
                    }
                }

                // 3) IMPORTANTÍSIMO: NO sobre-escribir Cash skills (ni resetear EndDate)
                //    - Si ya existe el slot para ese skill → solo SetCooldown
                //    - Si no existe → asignar a slot vacío con Normal
                var equippedSlot = client.Tamer.ActiveSkill.FirstOrDefault(x => x.SkillId == skillId);
                if (equippedSlot != null)
                {
                    Console.WriteLine(
                        $"[TamerSkillRequest] Slot found for SkillId={skillId}. Updating cooldown ONLY. CurrentType={equippedSlot.Type}");

                    equippedSlot.SetCooldown(cooldownSeconds);
                    _ = _sender.Send(new UpdateTamerSkillCooldownByIdCommand(equippedSlot));
                    Console.WriteLine("[TamerSkillRequest] COOLDOWN COMMAND SENT (existing slot)");
                    return;
                }

                var emptySlot = client.Tamer.ActiveSkill.FirstOrDefault(x => x.SkillId == 0);
                if (emptySlot == null)
                {
                    Console.WriteLine("[TamerSkillRequest] WARNING: No empty ActiveSkill slot found (SkillId==0).");
                    return;
                }

                Console.WriteLine($"[TamerSkillRequest] Empty slot found. Setting skill Normal. SkillId={skillId} Cooldown={cooldownSeconds}");
                emptySlot.SetTamerSkill(skillId, cooldownSeconds, TamerSkillTypeEnum.Normal);
                _ = _sender.Send(new UpdateTamerSkillCooldownByIdCommand(emptySlot));
                Console.WriteLine("[TamerSkillRequest] COOLDOWN COMMAND SENT (new slot)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TamerSkillRequest] EXCEPTION: {ex}");
                _logger.Error(ex, "[TamerSkillRequest] Exception SkillId={SkillId}", skillId);
            }
            finally
            {
                Console.WriteLine("[TamerSkillRequest] END PROCESS");
                Console.WriteLine("========================================");
            }
        }
    }
}
