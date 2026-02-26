/*using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.Game.Utils;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class MemorySkillUsePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MemorySkillUse;

        private readonly AssetsLoader _assets;
        private readonly ISender _sender;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly List<BuffRemoveTask> _activeBuffTasks = new();

        // Dictionary to track cooldowns per client and skill ID
        private readonly Dictionary<long, Dictionary<int, DateTime>> _cooldowns = new();

        public MemorySkillUsePacketProcessor(
            AssetsLoader assets,
            ISender sender,
            ILogger logger,
            IMapper mapper,
            MapServer mapServer,
            DungeonsServer dungeonServer)
        {
            _assets = assets;
            _sender = sender;
            _logger = logger;
            _mapper = mapper;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            int digimonUID = packet.ReadInt();
            byte uk0 = packet.ReadByte();
            int skillId = packet.ReadInt();
            int targetUID = packet.ReadInt();

            try
            {
                IMapServer server = client.DungeonMap ? _dungeonServer : _mapServer;

                // Find the memory skill info from DigimonSkill (memory skills are stored there)
                var memorySkill = _assets.DigimonSkillInfo.FirstOrDefault(x => x.SkillId == skillId);
                if (memorySkill == null)
                {
                    _logger.Warning($"[MemorySkillUse] Memory skill not found: {skillId}");
                    return;
                }

                var skillInfo = _assets.SkillInfo.FirstOrDefault(x => x.SkillId == memorySkill.SkillId);
                if (skillInfo == null)
                {
                    _logger.Warning($"[MemorySkillUse] Skill info not found for code: {memorySkill.SkillId}");
                    return;
                }

                // Find the buff info
                var buffInfo = _assets.BuffInfo.FirstOrDefault(x => x.DigimonSkillCode == memorySkill.SkillId && x.Class != 450)
                            ?? _assets.BuffInfo.FirstOrDefault(x => x.SkillCode == memorySkill.SkillId && x.Class != 450)
                            ?? _assets.BuffInfo.FirstOrDefault(x => x.BuffId == memorySkill.SkillId && x.Class != 450);

                if (buffInfo == null)
                {
                    _logger.Warning($"[MemorySkillUse] Buff info not found for skill: {skillId}");
                    return;
                }

                // Check cooldown for THIS specific skill
                if (IsOnCooldown(client.TamerId, skillId))
                {
                    var remainingCooldown = GetRemainingCooldown(client.TamerId, skillId);
                    var cooldownTime = $"{remainingCooldown.TotalSeconds:F0}s";
                    _logger.Information($"[MemorySkillUse] Skill {skillId} on cooldown. Remaining: {cooldownTime}");

                    client.Send(new SystemMessagePacket($"Skills in cooldown " + cooldownTime));
                    return;
                }

                // Get buff duration and cooldown based on skill type
                var (buffDuration, cooldownDuration) = GetDurationsBySkillType(skillId);

                // Apply the memory skill buff
                ApplyMemorySkillBuff(client, buffInfo, server, buffDuration);

                // Set cooldown for THIS specific skill
                SetCooldown(client.TamerId, skillId, cooldownDuration);

                _logger.Information($"[MemorySkillUse] Applied memory skill {skillId} to digimon {digimonUID}. Buff: {buffDuration}s, Cooldown: {cooldownDuration}s");
            }
            catch (Exception ex)
            {
                _logger.Error($"[MemorySkillUse] Error: {ex.Message}");
                _logger.Error($"[MemorySkillUse] Stack: {ex.StackTrace}");
            }
        }

        private (int buffDuration, int cooldownDuration) GetDurationsBySkillType(int skillId)
        {
            // You can categorize skills here based on their ID ranges or specific IDs
            // Example: Skills 1000-1999 = HP/Damage, 2000-2999 = Defense, 3000-3999 = Healing/Speed

            // For now, returning default values - you can customize this based on your skill IDs
            // Default: 5min buff, 500s cooldown
            return (300, 500);

            // Example of how to customize per skill type:
            /*
            if (skillId >= 1000 && skillId < 2000) // HP/Damage skills
                return (300, 500); // 5min buff, 500s cooldown
            else if (skillId >= 2000 && skillId < 3000) // Defense skills
                return (180, 300); // 3min buff, 5min cooldown
            else if (skillId >= 3000 && skillId < 4000) // Healing/Speed skills
                return (180, 300); // 3min buff, 5min cooldown
            else
                return (300, 500); // Default
            */
       /* }

        private bool IsOnCooldown(long tamerId, int skillId)
        {
            if (!_cooldowns.ContainsKey(tamerId))
                return false;

            if (!_cooldowns[tamerId].ContainsKey(skillId))
                return false;

            return DateTime.UtcNow < _cooldowns[tamerId][skillId];
        }

        private TimeSpan GetRemainingCooldown(long tamerId, int skillId)
        {
            if (!_cooldowns.ContainsKey(tamerId) || !_cooldowns[tamerId].ContainsKey(skillId))
                return TimeSpan.Zero;

            var remaining = _cooldowns[tamerId][skillId] - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private void SetCooldown(long tamerId, int skillId, int cooldownSeconds)
        {
            if (!_cooldowns.ContainsKey(tamerId))
                _cooldowns[tamerId] = new Dictionary<int, DateTime>();

            _cooldowns[tamerId][skillId] = DateTime.UtcNow.AddSeconds(cooldownSeconds);
        }

        private void ApplyMemorySkillBuff(GameClient client, BuffInfoAssetModel buff, IMapServer server, int duration)
        {
            // Check if buff already exists and remove it
            if (client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffId == buff.BuffId))
            {
                client.Partner.BuffList.Remove(buff.BuffId);
                server.BroadcastForTamerViewsAndSelf(
                    client.TamerId,
                    new RemoveBuffPacket(client.Partner.GeneralHandler, buff.BuffId).Serialize()
                );
            }

            var durationMs = duration * 1000;
            var ts = UtilitiesFunctions.RemainingTimeSeconds(duration);

            // Create new buff
            var newBuff = DigimonBuffModel.Create(buff.BuffId, buff.SkillCode, 0, duration, duration);
            newBuff.SetBuffInfo(buff);

            // Add buff to partner
            client.Tamer.Partner.BuffList.Add(newBuff);

            // Broadcast the buff to nearby players
            server.BroadcastForTamerViewsAndSelf(
                client.TamerId,
                new UpdateStatusPacket(client.Tamer).Serialize()
            );
            server.BroadcastForTamerViewsAndSelf(
                client.TamerId,
                new AddBuffPacket(client.Tamer.Partner.GeneralHandler, buff, 0, ts).Serialize()
            );

            // Add buff removal task
            _activeBuffTasks.Add(new BuffRemoveTask(
                client,
                client.Partner.GeneralHandler,
                buff.BuffId,
                durationMs,
                server
            ));
        }
    }
}*/