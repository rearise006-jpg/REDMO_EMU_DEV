using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.Items;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class HatchRemoveEggPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.HatchRemoveEgg;

        private readonly AssetsLoader _assets;
        private readonly ISender _sender;
        private readonly ILogger _logger;

        public HatchRemoveEggPacketProcessor(
            AssetsLoader assets,
            ISender sender,
            ILogger logger)
        {
            _assets = assets;
            _sender = sender;
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            if (client.Tamer.Incubator.NotDevelopedEgg)
            {
                var itemInfo = _assets.ItemInfo.GetValueOrDefault(client.Tamer.Incubator.EggId);
                if (itemInfo == null)
                {
                    _logger.Warning($"Egg item info not found for EggId {client.Tamer.Incubator.EggId}.");
                    client.Send(new SystemMessagePacket($"Egg data not found for item {client.Tamer.Incubator.EggId}. The egg was removed from the incubator and lost."));
                    client.Tamer.Incubator.RemoveEgg();
                    await _sender.Send(new UpdateIncubatorCommand(client.Tamer.Incubator));
                    return;
                }

                var newItem = new ItemModel();
                newItem.SetItemInfo(itemInfo);
                newItem.SetItemId(client.Tamer.Incubator.EggId);
                newItem.SetAmount(1);

                var cloneItem = (ItemModel)newItem.Clone();

                if (client.Tamer.Inventory.AddItem(cloneItem))
                {
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                }
                else
                {
                    _logger.Warning($"Inventory full for incubator recovery of item {client.Tamer.Incubator.EggId}.");
                    client.Send(new SystemMessagePacket($"Inventory full for incubator recovery of item {client.Tamer.Incubator.EggId}. The egg was removed from the incubator and lost."));
                    // Optionally, implement a mail/overflow system here instead of losing the egg.
                }

                // Always remove the egg from the incubator to avoid duplication/stuck eggs
                client.Tamer.Incubator.RemoveEgg();
                await _sender.Send(new UpdateIncubatorCommand(client.Tamer.Incubator));
            }
            else
            {
                client.Tamer.Incubator.RemoveEgg();
                await _sender.Send(new UpdateIncubatorCommand(client.Tamer.Incubator));
            }
        }
    }
}
