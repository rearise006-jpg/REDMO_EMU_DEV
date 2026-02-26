using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.PersonalShop;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ConsignedShopOpenPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ConsignedShopOpen;

        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly EventServer _eventServer;
        private readonly DungeonsServer _dungeonsServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public ConsignedShopOpenPacketProcessor(
            MapServer mapServer,
            EventServer eventServer,
            DungeonsServer dungeonsServer,
            PvpServer pvpServer,
            AssetsLoader assets,
            ILogger logger,
            ISender sender)
        {
            _mapServer = mapServer;
            _eventServer = eventServer;
            _dungeonsServer = dungeonsServer;
            _pvpServer = pvpServer;
            _assets = assets;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);


            var posX = packet.ReadInt();
            var posY = packet.ReadInt();
            packet.Skip(4);// ← Correction: jumps only nmoney (int64)
            var shopName = packet.ReadString();
            packet.Skip(9);
            var sellQuantity = packet.ReadInt();

            List<ItemModel> sellList = new(sellQuantity);

            client.Tamer.ConsignedShopItems.Clear();



            for (int i = 0; i < sellQuantity; i++)
            {
                var itemId = packet.ReadInt();
                var itemAmount = packet.ReadInt();

                var sellItem = new ItemModel(itemId, itemAmount);

                packet.Skip(64);

                var price = packet.ReadInt64();
                sellItem.SetSellPrice(price);

                packet.Skip(8);
                sellList.Add(sellItem);

                //Console.WriteLine($"Item Index: {i} | Price: {price}\n");
                _logger.Debug($"[Consigned] Item Index: {i} | Price: {price}");
            }

            foreach (var item in sellList)
            {
                var itemInfo = _assets.ItemInfo.GetValueOrDefault(item.ItemId);
                if (itemInfo == null)
                {
                    client.Send(new DisconnectUserPacket($"Invalid item: {item.ItemId}").Serialize());
                    client.Disconnect();
                    _logger.Warning($"[Consigned] :: {client.Tamer.Name} tried to consign an unknown item (ItemId: {item.ItemId})");
                    return;
                }
                item.SetItemInfo(itemInfo);

                var duplicate = sellList.GroupBy(x => x.ItemId).Any(g => g.Select(x => x.TamerShopSellPrice).Distinct().Count() > 1);

                if (duplicate)
                {
                    client.Send(new DisconnectUserPacket("You can't add 2 items of the same id with different prices!").Serialize());
                    client.Disconnect();
                    _logger.Warning($"[Consigned] :: {client.Tamer.Name} tried to consign duplicate items with different prices.");
                    return;
                }

                var HasQuanty = client.Tamer.Inventory.CountItensById(item.ItemId);

                if (item.Amount > HasQuanty)
                {
                    client.Send(new DisconnectUserPacket("You tried to consign more items than you own!").Serialize());
                    client.Disconnect();
                    _logger.Warning($"[Consigned] :: {client.Tamer.Name} tried to open consigned shop with more items than owned (ItemId: {item.ItemId}, Amount: {item.Amount}, Owned: {HasQuanty})");
                    return;
                }

                // Verification for Pack03
                if (item.ItemInfo!.BoundType == 2)
                {
                    client.Send(new DisconnectUserPacket("This item cannot be consigned.").Serialize());
                    client.Disconnect();
                    _logger.Warning($"[Consigned] :: {client.Tamer.Name} tried to consign a Pack03-bound item (ItemId: {item.ItemId})");
                    return;
                }
                if (item.ItemInfo!.BoundType == 1 && item.Power > 0)
                {
                    client.Send(new DisconnectUserPacket("This item cannot be consigned.").Serialize());
                    client.Disconnect();
                    _logger.Warning($"[Consigned] :: {client.Tamer.Name} tried to consign a Pack03-bound item with power (ItemId: {item.ItemId})");
                    return;
                }
            }
            client.Tamer.ConsignedShopItems.AddItems(sellList.Clone(), true);
            await _sender.Send(new UpdateItemsCommand(client.Tamer.ConsignedShopItems));

            client.Tamer.Inventory.RemoveOrReduceItems(sellList.Clone());
            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

            var newShop = ConsignedShop.Create(client.TamerId, shopName, posX, posY, client.Tamer.Location.MapId,
                client.Tamer.Channel, client.Tamer.ShopItemId);

            var Id = await _sender.Send(new CreateConsignedShopCommand(newShop));

            newShop.SetId(Id.Id);
            newShop.SetGeneralHandler(Id.GeneralHandler);


            var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));
            switch (mapConfig?.Type)
            {
                case MapTypeEnum.Dungeon:
                    _dungeonsServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new LoadConsignedShopPacket(newShop).Serialize());
                    break;

                case MapTypeEnum.Event:
                    _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new LoadConsignedShopPacket(newShop).Serialize());
                    break;

                case MapTypeEnum.Pvp:
                    _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new LoadConsignedShopPacket(newShop).Serialize());
                    break;

                default:
                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new LoadConsignedShopPacket(newShop).Serialize());
                    break;
            }


            client.Tamer.UpdateShopItemId(0);
            client.Send(new PersonalShopPacket(TamerShopActionEnum.CloseWindow, client.Tamer.ShopItemId));
            client.Tamer.RestorePreviousCondition();


            switch (mapConfig?.Type)
            {
                case MapTypeEnum.Dungeon:
                    _dungeonsServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition)
                            .Serialize());
                    break;

                case MapTypeEnum.Event:
                    _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition)
                            .Serialize());
                    break;

                case MapTypeEnum.Pvp:
                    _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition)
                            .Serialize());
                    break;

                default:
                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition)
                            .Serialize());
                    break;
            }
        }
    }
}