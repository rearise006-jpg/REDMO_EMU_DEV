using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class CashShopBuyPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.CashShopBuy;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly AssetsLoader _assets;

        public CashShopBuyPacketProcessor(AssetsLoader assets, ISender sender, ILogger logger)
        {
            _assets = assets;
            _sender = sender;
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            int totalAmount = packet.ReadByte();
            int totalPrice = packet.ReadInt();
            int type = packet.ReadInt();
            int u1 = packet.ReadInt();

            bool comprado = false;
            short Result = 1;
            byte TotalSuccess = 0;
            byte TotalFail = 0;

            int[] unique_id = new int[totalAmount];
            List<int> success_id = new List<int>();
            List<int> fail_id = new List<int>();

            if (client.Premium >= totalPrice)
            {
                for (int u = 0; u < totalAmount; u++)
                {
                    unique_id[u] = packet.ReadInt();

                    var Quexi = _assets.CashShopAssets.FirstOrDefault(x => x.Unique_Id == unique_id[u]);

                    if (Quexi != null && client.Premium >= Quexi.Price && Quexi.Activated == 1)
                    {
                        var itemId = Quexi.Item_Id;

                        var newItem = new ItemModel();
                        newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(itemId));

                        if (newItem.ItemInfo == null)
                        {
                            comprado = false;
                            Result = 31017;

                            client.Send(new CashShopReturnPacket(Result, client.Premium, client.Silk, (sbyte)success_id.Count, (sbyte)fail_id.Count, success_id, fail_id));
                            client.Send(new SystemMessagePacket($"No item info found with ID {itemId}."));
                            break;
                        }

                        newItem.ItemId = itemId;
                        newItem.Amount = Quexi.Quanty;

                        if (newItem.IsTemporary)
                            newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                        // Add item on CashWarehouse
                        if (client.Tamer.AccountCashWarehouse.AddItem(newItem))
                        {
                            client.Send(new LoadAccountWarehousePacket(client.Tamer.AccountCashWarehouse));
                            await _sender.Send(new UpdateItemsCommand(client.Tamer.AccountCashWarehouse));
                        }
                        else
                        {
                            client.Tamer.Inventory.AddItem(newItem);

                            client.Send(new ReceiveItemPacket(newItem, InventoryTypeEnum.Inventory));
                            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));

                            client.Send(new SystemMessagePacket($"No CashWarehouse space, sended to Inventory"));
                        }

                        Result = 0;
                        comprado = true;
                        success_id.Add(Quexi.Item_Id);
                        client.Premium -= Quexi.Price;
                        TotalSuccess++;
                    }
                    else
                    {
                        TotalFail++;

                        comprado = false;
                        Result = 31011;     // 31010 cash not enought

                        client.Send(new CashShopReturnPacket(Result, client.Premium, client.Silk, (sbyte)success_id.Count, (sbyte)fail_id.Count, success_id, fail_id));
                    }

                    if (Result == 1)
                    {
                        client.Send(new CashShopReturnPacket(Result, client.Premium, client.Silk, (sbyte)success_id.Count, (sbyte)fail_id.Count, success_id, fail_id));
                    }
                }

                await _sender.Send(new UpdatePremiumAndSilkCommand(client.Premium, client.Silk, client.AccountId));

                if (comprado == true)
                {
                    client.Send(new CashShopReturnPacket(Result, client.Premium, client.Silk, (sbyte)success_id.Count, (sbyte)fail_id.Count, success_id, fail_id));
                }
            }
            else
            {
                client.Send(new CashShopReturnPacket(Result, client.Premium, client.Silk, (sbyte)success_id.Count, (sbyte)fail_id.Count, success_id, fail_id));
            }
        }
    }
}

