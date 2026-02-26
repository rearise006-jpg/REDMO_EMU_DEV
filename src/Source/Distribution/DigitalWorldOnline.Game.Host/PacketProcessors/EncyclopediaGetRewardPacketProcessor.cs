using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class EncyclopediaGetRewardPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.EncyclopediaGetReward;

        private readonly AssetsLoader _assets;
        private readonly StatusManager _statusManager;

        private readonly ISender _sender;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        public EncyclopediaGetRewardPacketProcessor(AssetsLoader assets, StatusManager statusManager, ISender sender, ILogger logger, IMapper mapper)
        {
            _assets = assets;
            _statusManager = statusManager;
            _sender = sender;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var digimonRewardBaseType = packet.ReadInt();

            try
            {
                List<DigimonModel> allDigimons = new List<DigimonModel>();

                allDigimons.AddRange(client.Tamer.Digimons);

                foreach (var digimonArchive in client.Tamer.DigimonArchive.DigimonArchives.Where(x => x.DigimonId > 0))
                {
                    try
                    {
                        var digimon = _mapper.Map<DigimonModel>(await _sender.Send(new GetDigimonByIdQuery(digimonArchive.DigimonId)));

                        digimon.SetBaseInfo(_statusManager.GetDigimonBaseInfo(digimon.BaseType));
                        digimon.SetBaseStatus(_statusManager.GetDigimonBaseStatus(digimon.BaseType, digimon.Level, digimon.Size));

                        allDigimons.Add(digimon);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Erro ao buscar Digimon no arquivo: {ex.Message}");
                    }
                }

                var digimonToUpdate = allDigimons.FirstOrDefault(x => x.BaseType == digimonRewardBaseType);

                if (digimonToUpdate != null)
                {
                    digimonToUpdate.SetDigimonDeckReward(1);

                    byte result = 1;

                    var itemId = UtilitiesFunctions.DeckRewardItemId;

                    var newItem = new ItemModel();

                    newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(itemId));

                    if (newItem.ItemInfo != null)
                    {
                        newItem.ItemId = itemId;
                        newItem.Amount = UtilitiesFunctions.DeckRewardItemAmount;

                        if (newItem.IsTemporary)
                            newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                        if (client.Tamer.Inventory.AddItem(newItem))
                        {
                            client.Send(new EncyclopediaReceiveRewardItemPacket(newItem));

                            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                            await _sender.Send(new UpdateDigimonDeckRewardCommand(digimonToUpdate));
                        }
                        else
                        {
                            if (client.Tamer.GiftWarehouse.AddItem(newItem))
                            {
                                client.Send(new EncyclopediaReceiveRewardItemPacket(newItem));

                                await _sender.Send(new UpdateItemsCommand(client.Tamer.GiftWarehouse));
                                await _sender.Send(new UpdateDigimonDeckRewardCommand(digimonToUpdate));
                            }
                            else
                            {
                                client.Send(new PickItemFailPacket(PickItemFailReasonEnum.InventoryFull));
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[EncyclopediaGetReward] :: {ex.Message}");
            }
        }
    }
}