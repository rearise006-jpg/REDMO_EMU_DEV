using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Packets.Chat;
using MediatR;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigimonSkillLimitOpenPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigimonSkillDigiCode;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly AssetsLoader _assets;
        private readonly IConfiguration _configuration;

        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        public DigimonSkillLimitOpenPacketProcessor(ILogger logger, ISender sender, AssetsLoader assets, IConfiguration configuration)
        {
            _logger = logger;
            _sender = sender;
            _assets = assets;
            _configuration = configuration;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            _logger.Information($"[DigimonSkillOpen] Packet received from tamer {client.TamerId}");

            var packet = new GamePacketReader(packetData);
            int itemSlot = packet.ReadInt();
            int nItemType = packet.ReadInt();
            int nEvoSlot = packet.ReadInt();

            _logger.Information($"[DigimonSkillOpen] ItemSlot: {itemSlot}, ItemType: {nItemType}, EvoSlot: {nEvoSlot}");

            try
            {
                // Check if player is in a dungeon map
                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));

                if (mapConfig?.Type == MapTypeEnum.Dungeon)
                {
                    _logger.Warning($"[DigimonSkillOpen] Tamer {client.TamerId} attempted to use skill limit item in dungeon map {client.Tamer.Location.MapId}");
                    client.Send(new SystemMessagePacket("You cannot use this item inside a dungeon."));
                    return;
                }

                var nResult = 0;

                // Validate the item exists in inventory
                var inventoryItem = client.Tamer.Inventory.FindItemBySlot(itemSlot);
                if (inventoryItem == null)
                {
                    _logger.Error($"Invalid item at slot {itemSlot} for tamer id {client.TamerId} !! aborting process ...");
                    return;
                }

                // Validate if item is a valid DigiCode
                if (!Enum.IsDefined(typeof(DigimonSkillDigiCodeEnum), inventoryItem.ItemId))
                {
                    _logger.Error($"Item {inventoryItem.ItemId} is not a valid Skill DigiCode");
                    return;
                }

                var skillCode = (DigimonSkillDigiCodeEnum)inventoryItem.ItemId;

                // Determine target max level based on the enum name
                int targetMaxLevel = 0;
                int requiredCurrentLevel = 0;
                string codeName = skillCode.ToString();

                if (codeName.Contains("Lv15"))
                {
                    targetMaxLevel = 15;
                    requiredCurrentLevel = 10;
                }
                else if (codeName.Contains("Lv20"))
                {
                    targetMaxLevel = 20;
                    requiredCurrentLevel = 15;
                }
                else if (codeName.Contains("Lv25"))
                {
                    targetMaxLevel = 25;
                    requiredCurrentLevel = 20;
                }
                else
                {
                    _logger.Error($"Unknown level for Skill DigiCode: {skillCode}");
                    return;
                }

                // Get the correct evolution
                var evoLine = _assets.EvolutionInfo
                    .FirstOrDefault(x => x.Type == client.Tamer.Partner.BaseType)?
                    .Lines.FirstOrDefault(x => x.Type == client.Tamer.Partner.CurrentType);

                if (evoLine == null)
                {
                    _logger.Error($"Failed to find Partner EvoLine !! aborting process ...");
                    return;
                }

                _logger.Information($"EvoSlot: {nEvoSlot}\nEvoLineSlotLevel: {evoLine.SlotLevel - 1}");

                var Evolution = client.Tamer.Partner.Evolutions[evoLine.SlotLevel - 1];

                _logger.Information($"BEFORE - Evolution Skills Max Levels: {string.Join(", ", Evolution.Skills.Select(s => s.MaxLevel))}");
                _logger.Information($"BEFORE - CurrentEvolution Skills Max Levels: {string.Join(", ", client.Tamer.Partner.CurrentEvolution.Skills.Select(s => s.MaxLevel))}");

                if (Evolution == null)
                {
                    _logger.Error($"Evolution not found !! aborting process ...");
                    return;
                }

                // Check if any of the first 5 skills are memory skills (cash skills)
                var hasMemorySkills = Evolution.SkillsMemory?.Any() ?? false;

                if (hasMemorySkills)
                {
                    _logger.Warning($"Cannot use skill limit item on evolution with memory skills. Memory skills are already at max level.");
                    client.Send(new SystemMessagePacket("Cannot use this item. This evolution has memory skills which are already at maximum level."));
                    return;
                }

                _logger.Information($"Processing skill limit increase to level {targetMaxLevel} for tamer {client.TamerId}");
                _logger.Information($"Required current level: {requiredCurrentLevel}");

                // Validate that skills meet the required level
                var currentMaxLevels = Evolution.Skills.Take(5).Select(s => s.MaxLevel).ToList();
                var minCurrentLevel = currentMaxLevels.Min();

                _logger.Information($"Current skill max levels: {string.Join(", ", currentMaxLevels)}");
                _logger.Information($"Minimum current max level: {minCurrentLevel}");

                // Check if all skills are at least at the required level
                if (minCurrentLevel < requiredCurrentLevel)
                {
                    _logger.Warning($"Cannot use this item. Skills must be at level {requiredCurrentLevel} first. Current minimum: {minCurrentLevel}");
                    client.Send(new SystemMessagePacket($"Your skills must be at level {requiredCurrentLevel} before using this item."));
                    return;
                }

                // Check if skills are already at target level
                bool alreadyAtTarget = Evolution.Skills.Take(5).All(s => s.MaxLevel >= targetMaxLevel);

                if (alreadyAtTarget)
                {
                    _logger.Warning($"All skills already at or above target level {targetMaxLevel}. Cannot upgrade further with this item.");
                    client.Send(new SystemMessagePacket($"Your skills are already at or above level {targetMaxLevel}."));
                    return;
                }

                // Update skill max level ONLY if current max is below the target
                for (int i = 0; i < Evolution.Skills.Count && i < 5; i++)
                {
                    var currentSkill = Evolution.Skills[i];

                    if (currentSkill.MaxLevel < targetMaxLevel)
                    {
                        while (currentSkill.MaxLevel < targetMaxLevel)
                        {
                            currentSkill.IncreaseMaxSkillLevel();
                        }
                        _logger.Information($"Skill {i} max level increased to {currentSkill.MaxLevel}");
                    }
                    else
                    {
                        _logger.Information($"Skill {i} already at or above max level {targetMaxLevel} (current: {currentSkill.MaxLevel})");
                    }
                }

                var evolutionIndex = evoLine.SlotLevel - 1;

                _logger.Information($"Evolution reference check - Are they same? {client.Tamer.Partner.CurrentEvolution == Evolution}");

                if (client.Tamer.Partner.CurrentEvolution != null)
                {
                    _logger.Information($"CurrentEvolution MaxLevels BEFORE sync: {string.Join(", ", client.Tamer.Partner.CurrentEvolution.Skills.Select(s => s.MaxLevel))}");

                    for (int i = 0; i < Evolution.Skills.Count && i < client.Tamer.Partner.CurrentEvolution.Skills.Count; i++)
                    {
                        var currentSkill = client.Tamer.Partner.CurrentEvolution.Skills[i];
                        var updatedSkill = Evolution.Skills[i];

                        _logger.Information($"Skill {i} - Current MaxLevel: {currentSkill.MaxLevel}, Target MaxLevel: {updatedSkill.MaxLevel}");

                        while (currentSkill.MaxLevel < updatedSkill.MaxLevel)
                        {
                            currentSkill.IncreaseMaxSkillLevel();
                            _logger.Information($"Skill {i} - Increased to: {currentSkill.MaxLevel}");
                        }
                    }

                    _logger.Information($"CurrentEvolution MaxLevels AFTER sync: {string.Join(", ", client.Tamer.Partner.CurrentEvolution.Skills.Select(s => s.MaxLevel))}");
                }

                client.Tamer.Partner.RefreshStats();
                _logger.Information("Refreshed partner stats");

                // Remove item from Inventory
                client.Tamer.Inventory.RemoveOrReduceItem(inventoryItem, 1, itemSlot);

                // Save to database BEFORE sending client packets
                await _sender.Send(new UpdateEvolutionCommand(Evolution));
                await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

                // Send information to the client AFTER database update
                client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));
                client.Send(new DigimonSkillLimitOpenPacket(nResult, nEvoSlot, itemSlot, nItemType, Evolution));

                _logger.Information($"AFTER - Evolution Skills Max Levels: {string.Join(", ", Evolution.Skills.Select(s => s.MaxLevel))}");
                _logger.Information($"AFTER - CurrentEvolution Skills Max Levels: {string.Join(", ", client.Tamer.Partner.CurrentEvolution.Skills.Select(s => s.MaxLevel))}");

                _logger.Information($"Successfully processed skill limit open for tamer {client.TamerId}");

                // Force map swap to refresh client data
                _logger.Information("Forcing map swap to refresh client data...");

                client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                client.SetGameQuit(false);

                client.Send(new MapSwapPacket(
                    _configuration[GamerServerPublic],
                    _configuration[GameServerPort],
                    client.Tamer.Location.MapId,
                    client.Tamer.Location.X,
                    client.Tamer.Location.Y
                ));
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigimonSkillOpen] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}