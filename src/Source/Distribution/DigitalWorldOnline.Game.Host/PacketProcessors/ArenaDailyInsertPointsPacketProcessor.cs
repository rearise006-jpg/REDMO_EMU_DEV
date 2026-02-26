using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Packets.Items;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ArenaDailyInsertPointsPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArenaDailyInsertPoints;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly AssetsLoader _assets;

        public ArenaDailyInsertPointsPacketProcessor(ILogger logger, ISender sender, IMapper mapper, AssetsLoader assets)
        {
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
            _assets = assets;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var nListSize = packet.ReadShort();
            var itemSlot = packet.ReadShort();
            var itemAmount = packet.ReadShort();
            var itemId = packet.ReadInt();

            _logger.Information($"ListSize: {nListSize}");
            _logger.Information($"itemSlot: {itemSlot} | itemAmount: {itemAmount} | ItemId: {itemId}");

            // Daily Ranking ------------------------------------------------------------------------------------

            var previousPoints = client.Tamer.DailyPoints.Points;
            var currentPoints = previousPoints + itemAmount;

            client.Tamer.AddPoints(itemAmount);

            var todayRewards = _assets.ArenaRankingDailyItemRewards.FirstOrDefault(x => x.WeekDay == DateTime.Now.DayOfWeek);

            var rewardsToReceive = todayRewards.GetRewards(previousPoints, currentPoints);

            var removeItem = client.Tamer.Inventory.FindItemBySlot(itemSlot);
            client.Tamer.Inventory.RemoveOrReduceItem(removeItem, itemAmount);

            client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));

            foreach (var item in rewardsToReceive)
            {
                var targetItem = new ItemModel();

                targetItem.ItemId = item.ItemId;
                targetItem.Amount = item.Amount;
                targetItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(item.ItemId));

                if (client.Tamer.Inventory.AddItem(targetItem))
                {
                    client.Send(new ReceiveItemPacket(targetItem, InventoryTypeEnum.Inventory));
                }
                else
                {
                    client.Tamer.GiftWarehouse.AddItem(targetItem);
                    client.Send(new LoadGiftStoragePacket(client.Tamer.GiftWarehouse));
                }
            }

            client.Send(new ArenaRankingDailyUpdatePointsPacket(client.Tamer.DailyPoints.Points, nListSize, itemSlot, itemAmount, itemId, rewardsToReceive));

            await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
            await _sender.Send(new UpdateItemsCommand(client.Tamer.GiftWarehouse));

            await _sender.Send(new UpdateCharacterArenaDailyPointsCommand(client.Tamer.DailyPoints));

            // Weekly Ranking -----------------------------------------------------------------------------------

            var weeklyRankingInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetArenaRankingQuery(ArenaRankingEnum.Weekly)));

            if (weeklyRankingInfo == null)
            {
                return;
            }

            var weeklyRanking = weeklyRankingInfo.Competitors.FirstOrDefault(x => x.TamerId == client.TamerId);

            if (weeklyRanking == null)
            {
                weeklyRankingInfo.JoinRanking(client.TamerId, itemAmount);
                weeklyRankingInfo.GetRank(client.TamerId);
                weeklyRanking = weeklyRankingInfo.Competitors.FirstOrDefault(x => x.TamerId == client.TamerId);
            }
            else
            {
                weeklyRanking.AddPoints(itemAmount);
                weeklyRankingInfo.GetRank(client.TamerId);
            }

            await _sender.Send(new UpdateArenaRankingCommand(weeklyRankingInfo));

            // Monthly Ranking ----------------------------------------------------------------------------------

            var monthlyRankingInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetArenaRankingQuery(ArenaRankingEnum.Monthly)));

            if (monthlyRankingInfo == null)
            {
                return;
            }

            var monthlyRanking = monthlyRankingInfo.Competitors.FirstOrDefault(x => x.TamerId == client.TamerId);

            if (monthlyRanking == null)
            {
                monthlyRankingInfo.JoinRanking(client.TamerId, itemAmount);
                monthlyRankingInfo.GetRank(client.TamerId);
                monthlyRanking = monthlyRankingInfo.Competitors.FirstOrDefault(x => x.TamerId == client.TamerId);
            }
            else
            {
                monthlyRanking.AddPoints(itemAmount);
                monthlyRankingInfo.GetRank(client.TamerId);
            }

            await _sender.Send(new UpdateArenaRankingCommand(monthlyRankingInfo));

            // Seasonal Ranking ----------------------------------------------------------------------------------

            var seasonalRankingInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetArenaRankingQuery(ArenaRankingEnum.Seasonal)));

            if (seasonalRankingInfo == null)
            {
                return;
            }

            var seasonalRanking = seasonalRankingInfo.Competitors.FirstOrDefault(x => x.TamerId == client.TamerId);

            if (seasonalRanking == null)
            {
                seasonalRankingInfo.JoinRanking(client.TamerId, itemAmount);
                seasonalRankingInfo.GetRank(client.TamerId);
                seasonalRanking = seasonalRankingInfo.Competitors.FirstOrDefault(x => x.TamerId == client.TamerId);
            }
            else
            {
                seasonalRanking.AddPoints(itemAmount);
                seasonalRankingInfo.GetRank(client.TamerId);
            }

            await _sender.Send(new UpdateArenaRankingCommand(seasonalRankingInfo));
        }
    }
}
