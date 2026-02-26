using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Models.Character;
using Serilog;
using MediatR;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ArchiveAcademyInsertPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArchiveAcademyInsert;

        private readonly AssetsLoader _assets;

        private readonly ISender _sender;
        private readonly ILogger _logger;

        public ArchiveAcademyInsertPacketProcessor(AssetsLoader assets, ISender sender, ILogger logger)
        {
            _assets = assets;
            _sender = sender;
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var growthSlot = packet.ReadByte();         // Slot de Growth
            var archiveSlot = packet.ReadInt() - 1000;  // Slot do Arquivo
            var itemInvSlot = packet.ReadInt();         // Slot do item no inventário

            try
            {
                var archiveItem = client.Tamer.DigimonArchive.DigimonArchives.FirstOrDefault(x => x.Slot == archiveSlot);
                var growthItem = client.Tamer.Inventory.FindItemBySlot(itemInvSlot);

                var existingDigimon = client.Tamer.DigimonArchive.DigimonGrowths.FirstOrDefault(x => x.DigimonId == archiveItem.DigimonId);

                if (existingDigimon != null)
                {
                    client.Send(new DigimonArchiveGrowthErrorPacket());
                    return;
                }

                if (archiveItem == null || growthItem == null)
                {
                    _logger.Warning($"Invalid Growth Process: ArchiveSlot={archiveSlot}, ItemSlot={itemInvSlot} (Item or Archive not found)");
                    return;
                }

                growthItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(growthItem.ItemId));

                if (growthItem.ItemInfo == null)
                {
                    _logger.Warning($"ItemInfo not found for ItemId={growthItem.ItemId}");
                    return;
                }

                int growthRemainTime = UtilitiesFunctions.RemainingTimeMinutes(growthItem.ItemInfo.UsageTimeMinutes);
                DateTime growthEndDate = DateTime.Now.AddMinutes(growthItem.ItemInfo.UsageTimeMinutes);

                var digimonToGrowth = new CharacterDigimonGrowthSystemModel
                {
                    GrowthSlot = growthSlot,
                    ArchiveSlot = archiveSlot,
                    GrowthItemId = growthItem.ItemId,
                    EndDate = growthEndDate,
                    ExperienceAccumulated = 0,
                    IsActive = 1,
                    DigimonId = archiveItem.DigimonId,
                    CharacterId = client.Tamer.Id,
                    DigimonArchiveId = client.Tamer.DigimonArchive.Id

                };

                client.Send(new DigimonArchiveGrowthActivePacket(growthSlot, growthRemainTime));

                client.Tamer.Inventory.RemoveOrReduceItem(growthItem, 1);
                client.Tamer.DigimonArchive.DigimonGrowths.Add(digimonToGrowth);

                _ = _sender.Send(new UpdateCharacterDigimonGrowthCommand(digimonToGrowth));
                _ = _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
            }
            catch (Exception ex)
            {
                _logger.Error($"[ArchiveAcademyInsert] :: {ex.Message}");
            }
        }
    }
}

