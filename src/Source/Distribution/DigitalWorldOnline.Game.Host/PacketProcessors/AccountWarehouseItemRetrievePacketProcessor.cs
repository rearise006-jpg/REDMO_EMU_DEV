using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class AccountWarehouseItemRetrievePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.RetrivieAccountWarehouseItem;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly AssetsLoader _assets;

        public AccountWarehouseItemRetrievePacketProcessor(ILogger logger, ISender sender, AssetsLoader assets)
        {
            _logger = logger;
            _sender = sender;
            _assets = assets;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var itemSlot = packet.ReadShort();

            var targetItem = client.Tamer.AccountCashWarehouse.FindItemBySlot(itemSlot);

            if (targetItem == null)
            {
                _logger.Error($"Invalid or missing item in slot {itemSlot}.");
                return;
            }

            if (targetItem.Amount <= 0)
            {
                _logger.Warning($"Invalid or missing item in slot {itemSlot}. Potential hacking attempt.");
                client.Tamer.AccountCashWarehouse.RemoveItem(targetItem, itemSlot);
                client.Tamer.AccountCashWarehouse.Sort();
                return;
            }

            var result = 0;

            try
            {
                var newItem = new ItemModel();
                newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(targetItem.ItemId));

                if (newItem.ItemInfo == null)
                {
                    _logger.Warning($"No item info found with ID {targetItem.ItemId} for tamer {client.TamerId}.");
                    return;
                }

                newItem.ItemId = targetItem.ItemId;
                newItem.Amount = targetItem.Amount;

                if (newItem.IsTemporary)
                    newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                if (client.Tamer.Inventory.TotalEmptySlots > 0)
                {
                    // Remove item from CashWarehouse
                    client.Tamer.AccountCashWarehouse.RemoveItem(targetItem, itemSlot);
                    client.Tamer.AccountCashWarehouse.Sort();

                    client.Send(new LoadAccountWarehousePacket(client.Tamer.AccountCashWarehouse));
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.AccountCashWarehouse));

                    // Add item to Inventory
                    client.Tamer.Inventory.AddItem(newItem);

                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

                    // Send Packet to client
                    client.Send(new AccountWarehouseItemRetrievePacket(newItem, itemSlot, InventoryTypeEnum.Inventory, result));
                }
                else
                {
                    result = 20150; // This slot is not available.

                    client.Send(new AccountWarehouseItemRetrievePacket(targetItem, itemSlot, InventoryTypeEnum.Inventory, result));
                    client.Send(new LoadAccountWarehousePacket(client.Tamer.AccountCashWarehouse));

                    _logger.Warning($"Failed to add item in Inventory!! Tamer {client.Tamer.Name} dont have free slots");
                    return;
                }
            }
            catch (Exception ex)
            {
                result = 20150; // This slot is not available.

                client.Send(new AccountWarehouseItemRetrievePacket(targetItem, itemSlot, InventoryTypeEnum.Inventory, result));
                client.Send(new LoadAccountWarehousePacket(client.Tamer.AccountCashWarehouse));

                _logger.Error($"[RetrivieAccountWarehouseItem] :: {ex.Message}");
                _logger.Error($"{ex.InnerException}");
                return;
            }


        }
    }
}