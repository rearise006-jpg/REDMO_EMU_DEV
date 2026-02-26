using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
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
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.PersonalShop;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ConsignedShopPurchaseItemPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ConsignedShopPurchaseItem;

        private readonly AssetsLoader _assets;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly ISender _sender;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;


        public ConsignedShopPurchaseItemPacketProcessor(AssetsLoader assets, ILogger logger, IMapper mapper, ISender sender, MapServer mapServer, DungeonsServer dungeonServer)
        {
            _assets = assets;
            _logger = logger;
            _mapper = mapper;
            _sender = sender;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);


            var shopHandler = packet.ReadInt();
            var shopSlot = packet.ReadInt();
            var shopSlotInDatabase = shopSlot - 1;
            var boughtItemId = packet.ReadInt();
            var boughtAmount = packet.ReadInt();
            packet.Skip(60);
            var boughtUnitPrice = packet.ReadInt64();


            var shop = _mapper.Map<ConsignedShop>(await _sender.Send(new ConsignedShopByHandlerQuery(shopHandler)));

            if (shop == null)
            {
                _logger.Error($"Consigned shop {shopHandler} not found...");
                client.Send(new UnloadConsignedShopPacket(shopHandler));
                return;
            }

            var seller =
                _mapper.Map<CharacterModel>(await _sender.Send(new CharacterAndItemsByIdQuery(shop.CharacterId)));

            if (seller == null)
            {
                _logger.Error($"Deleting consigned shop {shopHandler}...");
                await _sender.Send(new DeleteConsignedShopCommand(shopHandler));

                _logger.Error($"Consigned shop owner {shop.CharacterId} not found...");
                client.Send(new UnloadConsignedShopPacket(shopHandler));
                return;
            }

            if (seller.Name == client.Tamer.Name)
            {
                client.Send(new NoticeMessagePacket($"You cannot buy from the store itself!"));
                return;
            }

            var boughtItem = seller.ConsignedShopItems.Items.FirstOrDefault(x => x.Slot == shopSlotInDatabase);

            if (boughtItem == null)
            {
                client.Send(new ConsignedShopBoughtItemPacket(TamerShopActionEnum.NoPartFound, shopSlot, boughtAmount)
                    .Serialize());
                return;
            }

            if (boughtItem.Amount < boughtAmount)
            {
                client.Send(new ConsignedShopBoughtItemPacket(true));
                return;
            }

            var totalValue = boughtItem.TamerShopSellPrice * boughtAmount;

            if (client.Tamer.Inventory.Bits < totalValue)
            {
                ////sistema de banimento permanente
                //var banProcessor = SingletonResolver.GetService<BanForCheating>();
                //var banMessage = banProcessor.BanAccountWithMessage(client.AccountId, client.Tamer.Name,
                //    AccountBlockEnum.Permanent, "Cheating", client,
                //    "You tried to buy an item with an invalid amount of bits using a cheat method, So be happy with ban!");

                //var chatPacket = new NoticeMessagePacket(banMessage).Serialize();
                //client.SendToAll(chatPacket);
                client.SetGameQuit(true);
                client.Disconnect();
                return;
            }

            client.Tamer.Inventory.RemoveBits(totalValue);
            await _sender.Send(new UpdateItemListBitsCommand(client.Tamer.Inventory));

            var newItem = new ItemModel(boughtItem.ItemId, boughtAmount);
            newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(boughtItem.ItemId));

            client.Tamer.Inventory.AddItems(((ItemModel)newItem.Clone()).GetList());
            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

            client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));

            // ----------------------------------------------------------------------

            var sellerClient = client.Server.FindByTamerId(shop.CharacterId);

            if (sellerClient is { IsConnected: true })
            {
                var itemName = _assets.ItemInfo.GetValueOrDefault(boughtItem.ItemId)?.Name ?? "item";
                sellerClient.Send(
                    new NoticeMessagePacket($"You sold {boughtAmount}x {itemName} for {client.Tamer.Name}!"));

                sellerClient.Tamer.ConsignedWarehouse.AddBits(totalValue);

                await _sender.Send(new UpdateItemListBitsCommand(sellerClient.Tamer.ConsignedWarehouse));

                seller.ConsignedShopItems.RemoveOrReduceItems(((ItemModel)newItem.Clone()).GetList(), true);

                await _sender.Send(new UpdateItemsCommand(seller.ConsignedShopItems));

                sellerClient.Send(new LoadInventoryPacket(sellerClient.Tamer.ConsignedWarehouse, InventoryTypeEnum.ConsignedWarehouse));

            }
            else
            {
                seller.ConsignedWarehouse.AddBits(totalValue);

                seller.ConsignedShopItems.RemoveOrReduceItems(((ItemModel)newItem.Clone()).GetList(), true);

                await _sender.Send(new UpdateItemListBitsCommand(seller.ConsignedWarehouse));

                await _sender.Send(new UpdateItemsCommand(seller.ConsignedShopItems));
            }

            if (seller.ConsignedShopItems.Count == 0)
            {
                await _sender.Send(new DeleteConsignedShopCommand(shopHandler));

                await _sender.Send(new UpdateItemsCommand(seller.ConsignedShopItems));

                // 📢 TÜM PLAYERLARA SHOP KAPATILDI HABER VER
                _mapServer.BroadcastForTamerViewsAndSelf(
                    client.Tamer.Id,
                    new UnloadConsignedShopPacket(shopHandler).Serialize());

                _mapServer.BroadcastForTamerViewsAndSelf(
                    client.Tamer.Id,
                    new ConsignedShopClosePacket().Serialize());

                // ⭐ EKLE: Server-side de shop'u hemen sil
                var map = _mapServer.Maps.FirstOrDefault(m =>
                    m.MapId == client.Tamer.Location.MapId &&
                    m.Channel == client.Tamer.Channel);

                if (map != null)
                {
                    map.ConsignedShops.RemoveAll(s => s.GeneralHandler == shopHandler);
                    map.ConsignedShopsToRemove.RemoveAll(s => s.GeneralHandler == shopHandler);
                }
            }
        }
    }
}