using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Packets.PersonalShop;
using DigitalWorldOnline.Commons.Writers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ConsignedShopRetrievePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ConsignedShopRetrieve;

        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly EventServer _eventServer;
        private readonly DungeonsServer _dungeonsServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly ISender _sender;

        public ConsignedShopRetrievePacketProcessor(
            AssetsLoader assets,
            MapServer mapServer,
            EventServer eventServer,
            DungeonsServer dungeonsServer,
            PvpServer pvpServer,
            ILogger logger,
            IMapper mapper,
            ISender sender)
        {
            _assets = assets;
            _mapServer = mapServer;
            _eventServer = eventServer;
            _dungeonsServer = dungeonsServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _mapper = mapper;
            _sender = sender;
        }
        //todo
        // fazer tratamento para fechar o consigned apenas quando nao tiver solicitação de compra
        public async Task Process(GameClient client, byte[] packetData)
        {
            var shop = _mapper.Map<ConsignedShop>(await _sender.Send(new ConsignedShopByTamerIdQuery(client.TamerId)));

            if (shop != null)
            {
                var seller = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterAndItemsByIdQuery(shop.CharacterId)));
                List<ItemModel> equippedItems = seller.ConsignedShopItems?.EquippedItems?.ToList() ?? new List<ItemModel>();
                var items = equippedItems.Clone();

                items.ForEach(item =>
                {
                    item.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(item.ItemId));
                });

                // Verifica quantos slots estão disponíveis no armazém
                var warehouse = client.Tamer.ConsignedWarehouse;
                var availableSlots = warehouse.TotalEmptySlots;
                var requiredSlots = items.Count(item =>
                    !warehouse.Items.Any(wItem =>
                        wItem.ItemId == item.ItemId &&
                        wItem.Amount < (wItem.ItemInfo?.Overlap ?? 1)));

                if (requiredSlots > availableSlots)
                {
                    var errorMessage = new PacketWriter();
                    errorMessage.WriteString("Seu armazém não tem espaço suficiente para recolher todos os itens da loja.");
                    client.Send(errorMessage);
                    _logger.Warning($"Falha ao recolher loja de {client.Tamer.Name}: itens excedem o espaço do armazém.");
                    return;
                }

                // Verificação de integridade contra duplicação
                bool hasConflict = false;
                foreach (var item in items)
                {
                    var match = seller.ConsignedShopItems.Items
                        .FirstOrDefault(x => x.ItemId == item.ItemId &&
                                             x.Amount == item.Amount &&
                                             x.TamerShopSellPrice == item.TamerShopSellPrice);

                    if (match == null)
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (hasConflict)
                {
                    var errorMessage = new PacketWriter();
                    errorMessage.WriteString("A loja não pode ser encerrada no momento. Um ou mais itens foram vendidos.");
                    client.Send(errorMessage);
                    _logger.Warning($"[DUPE PREVENT] Tentativa de recolher loja falhou para {client.Tamer.Name}: item ausente ou já vendido.");
                    return;
                }

                // Prossegue com o encerramento seguro da loja
                client.Tamer.ConsignedShopItems.RemoveOrReduceItems(items.Clone());
                await _sender.Send(new DeleteConsignedShopCommand(shop.GeneralHandler));
                warehouse.AddItems(items.Clone());

                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));
                switch (mapConfig?.Type)
                {
                    case MapTypeEnum.Dungeon:
                        _dungeonsServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new UnloadConsignedShopPacket(shop).Serialize());
                        break;
                    case MapTypeEnum.Event:
                        _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new UnloadConsignedShopPacket(shop).Serialize());
                        break;
                    case MapTypeEnum.Pvp:
                        _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new UnloadConsignedShopPacket(shop).Serialize());
                        break;
                    default:
                        _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new UnloadConsignedShopPacket(shop).Serialize());
                        break;
                }

                await _sender.Send(new UpdateItemsCommand(client.Tamer.ConsignedShopItems));
                await _sender.Send(new UpdateItemsCommand(warehouse));
            }

            _logger.Debug($"Sending consigned shop close packet...");
            client.Send(new ConsignedShopClosePacket());
        }

    }
}