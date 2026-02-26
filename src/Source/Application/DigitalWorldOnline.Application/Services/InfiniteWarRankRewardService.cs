using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Models.Assets.XML.InfiniteWar;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Enums;
using Serilog;
using MediatR;
using AutoMapper;

namespace DigitalWorldOnline.Application.Services
{
    public class InfiniteWarRankRewardService
    {
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly ISender _sender;

        private readonly AssetsLoader _assets;
        private List<InfiniteWar_RankRewardItemsXmlModel> _rankRewardItemsData;

        public InfiniteWarRankRewardService(ILogger logger, IMapper mapper, ISender sender, AssetsLoader assets)
        {
            _logger = logger;
            _mapper = mapper;
            _sender = sender;
            _assets = assets;
        }

        public void InitializeRankRewardData()
        {
            if (_rankRewardItemsData == null) // Apenas para evitar re-inicialização
            {
                _rankRewardItemsData = _assets.InfiniteWar_RankRewardItems ?? throw new ArgumentNullException(nameof(_assets.InfiniteWar_RankRewardItems),
                                                                       "InfiniteWar Rank Reward data from AssetsLoader cannot be null.");
                if (!_rankRewardItemsData.Any())
                {
                    _logger.Warning("InfiniteWar_RankRewardItems data is empty. Rewards might not be distributed correctly.");
                }
            }
        }

        /// <summary>
        /// Busca a lista de itens de recompensa para um determinado rank, tipo de recompensa (nKeyValue) e categoria de rank (s_nRankType).
        /// </summary>
        /// <param name="tamerRank">O rank atual do tamer.</param>
        /// <param name="rewardTypeKeyValue">O valor de nKeyValue (1: Semanal, 2: Mensal, 3: Seasonal).</param>
        /// <param name="rankCategoryType">O valor de s_nRankType (0: Ranks individuais/faixas, 1: Ranks em percentual).</param>
        /// <returns>Uma lista de objetos InfiniteWar_RankRewardItemXmlModel, ou uma lista vazia se nenhuma recompensa for encontrada.</returns>
        public List<InfiniteWar_RankRewardItemXmlModel> GetRewardsForRank(int tamerRank, int rewardTypeKeyValue, int rankCategoryType, int totalPlayersForPercentageCalculation)
        {
            if (_rankRewardItemsData == null || !_rankRewardItemsData.Any())
            {
                _logger.Warning($"No InfiniteWar Rank Reward data available for lookup (Rank: {tamerRank}, KeyValue: {rewardTypeKeyValue}, RankType: {rankCategoryType}).");
                return new List<InfiniteWar_RankRewardItemXmlModel>();
            }

            // 1. Encontra o RankRewardItemsContanier que corresponde ao nKeyValue (rewardTypeKeyValue)
            var targetContainer = _rankRewardItemsData.FirstOrDefault(c => c.RankRewardType == rewardTypeKeyValue);

            if (targetContainer == null)
            {
                _logger.Information($"No RankRewardItemsContanier found for reward type (nKeyValue): {rewardTypeKeyValue}.");
                return new List<InfiniteWar_RankRewardItemXmlModel>();
            }

            if (targetContainer.InfiniteWar_RankRewardInfos?.InfiniteWar_RankRewardInfo == null || !targetContainer.InfiniteWar_RankRewardInfos.InfiniteWar_RankRewardInfo.Any())
            {
                _logger.Warning($"RankRewardInfos or RankRewardInfo list is empty/null for nKeyValue: {rewardTypeKeyValue}.");
                return new List<InfiniteWar_RankRewardItemXmlModel>();
            }

            // 2. Acessa a lista de RankRewardInfo dentro do container encontrado
            var rankInfosForType = targetContainer.InfiniteWar_RankRewardInfos.InfiniteWar_RankRewardInfo;

            // 3. Encontra a recompensa que corresponde ao rank e s_nRankType
            InfiniteWar_RankRewardInfoModel? matchingRewardInfo = null;

            if (rankCategoryType == 0) // Para ranks absolutos (Tipo 0: Rank 1-10)
            {
                matchingRewardInfo = rankInfosForType.FirstOrDefault(r =>
                    r.RankType == rankCategoryType && tamerRank >= r.RankMin && tamerRank <= r.RankMax);
            }
            else if (rankCategoryType == 1)
            {
                if (totalPlayersForPercentageCalculation == 0)
                {
                    _logger.Information($"No competitors found for RankType 1 (Rank >= 11) to apply percentage-based rewards.");
                    return new List<InfiniteWar_RankRewardItemXmlModel>();
                }

                double tamerPercentile = ((double)(tamerRank - 10) / totalPlayersForPercentageCalculation) * 100.0;

                if (tamerPercentile < 0) tamerPercentile = 0;

                matchingRewardInfo = rankInfosForType.FirstOrDefault(r =>
                    r.RankType == rankCategoryType && tamerPercentile >= r.RankMin && tamerPercentile <= r.RankMax);

                if (matchingRewardInfo == null)
                {
                    _logger.Information($"No percentage-based reward found for Rank: {tamerRank}, Percentile: {tamerPercentile:F2}%, KeyValue: {rewardTypeKeyValue}, RankType: {rankCategoryType}.");
                }
            }

            if (matchingRewardInfo != null && matchingRewardInfo.InfiniteWar_RankRewardItemsInfo?.InfiniteWar_RankRewardItem != null)
            {
                _logger.Information($"Found {matchingRewardInfo.InfiniteWar_RankRewardItemsInfo.InfiniteWar_RankRewardItem.Count} items for Rank: {tamerRank}, KeyValue: {rewardTypeKeyValue}, RankType: {rankCategoryType}.");
                return matchingRewardInfo.InfiniteWar_RankRewardItemsInfo.InfiniteWar_RankRewardItem;
            }

            return new List<InfiniteWar_RankRewardItemXmlModel>();
        }

        /// <summary>
        /// Sends reward for arena monthly rank to players.
        /// </summary>
        /// <param name="rankingMonthlyInfo">Information of arena month rank.</param>
        public async Task DistributeMonthRankRewards(ArenaRankingModel rankingMonthlyInfo)
        {
            const int monthRankKeyValue = (int)ArenaRankingEnum.Monthly;

            if (rankingMonthlyInfo?.Competitors == null || !rankingMonthlyInfo.Competitors.Any())
            {
                _logger.Information("No competitors found in monthly ranking. No rewards to distribute.");
                return;
            }

            int totalPlayersOfType1Monthly = rankingMonthlyInfo.Competitors.Count(c => c.Position > 10);

            foreach (var targetTamer in rankingMonthlyInfo.Competitors.OrderBy(c => c.Position))
            {
                var tamerRank = targetTamer.Position;
                var tamerId = targetTamer.TamerId;

                int sNRankTypeForMonthly = tamerRank <= 10 ? 0 : 1;

                CharacterModel? character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByIdQuery(tamerId)));

                if (character == null || character.Partner == null)
                {
                    _logger.Error($"Invalid character information for tamer id {tamerId}.");
                    continue;
                }

                List<InfiniteWar_RankRewardItemXmlModel> rewardsToGive = GetRewardsForRank(tamerRank, monthRankKeyValue, sNRankTypeForMonthly, totalPlayersOfType1Monthly);

                if (rewardsToGive.Any())
                {
                    _logger.Information($"Distributing rewards for TamerId: [{tamerId}:{character.Name}], Rank: {tamerRank}:");

                    foreach (var rewardItem in rewardsToGive)
                    {
                        var newItem = new ItemModel();

                        newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(rewardItem.ItemId));

                        if (newItem.ItemInfo == null)
                        {
                            _logger.Error($"No item info found with ID {rewardItem.ItemId}.");
                            continue;
                        }

                        newItem.ItemId = rewardItem.ItemId;
                        newItem.Amount = rewardItem.ItemAmount;

                        if (newItem.IsTemporary)
                            newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                        newItem.EndDate = DateTime.Now.AddDays(7);

                        if (character.GiftWarehouse.AddItem(newItem))
                        {
                            await _sender.Send(new UpdateItemsCommand(character.GiftWarehouse));

                            _logger.Information($"ItemID {newItem.ItemId} x{newItem.Amount} added to [{tamerId}:{character.Name}] GiftWarehouse.");
                        }
                        else
                        {
                            _logger.Error($"Failed to send item {newItem.ItemId} to [{tamerId}:{character.Name}] GiftWarehouse.");
                        }
                    }
                }
                else
                {
                    _logger.Warning($"No configured rewards in XML for TamerId: {targetTamer.TamerId} at Rank: {tamerRank} (Type: {monthRankKeyValue}, Category: {sNRankTypeForMonthly}).");
                }
            }
        }

        /// <summary>
        /// Distribui as recompensas de rank semanal da Arena aos competidores.
        /// </summary>
        /// <param name="rankingWeeklyInfo">Informações do ranking semanal da Arena.</param>
        public async Task DistributeWeeklyRankRewards(ArenaRankingModel rankingWeeklyInfo)
        {
            const int weekRankKeyValue = (int)ArenaRankingEnum.Weekly;

            if (rankingWeeklyInfo?.Competitors == null || !rankingWeeklyInfo.Competitors.Any())
            {
                _logger.Information("No competitors found in weekly ranking. No rewards to distribute.");
                return;
            }

            int totalPlayersOfType1Weekly = rankingWeeklyInfo.Competitors.Count(c => c.Position > 10);

            foreach (var targetTamer in rankingWeeklyInfo.Competitors.OrderBy(c => c.Position))
            {
                var tamerRank = targetTamer.Position;
                var tamerId = targetTamer.TamerId;

                int sNRankTypeForWeekly = tamerRank <= 10 ? 0 : 1;

                CharacterModel? character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByIdQuery(tamerId)));

                if (character == null || character.Partner == null)
                {
                    _logger.Error($"Invalid character information for tamer id {tamerId}.");
                    continue;
                }

                List<InfiniteWar_RankRewardItemXmlModel> rewardsToGive = GetRewardsForRank(tamerRank, weekRankKeyValue, sNRankTypeForWeekly, totalPlayersOfType1Weekly);

                if (rewardsToGive.Any())
                {
                    _logger.Information($"Distributing rewards for Tamer: [{tamerId}:{character.Name}], Rank: {tamerRank} (Weekly):");

                    foreach (var rewardItem in rewardsToGive)
                    {
                        var newItem = new ItemModel();

                        newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(rewardItem.ItemId));


                        if (newItem.ItemInfo == null)
                        {
                            _logger.Error($"No item info found with ID {rewardItem.ItemId}.");
                            continue;
                        }

                        newItem.ItemId = rewardItem.ItemId;
                        newItem.Amount = rewardItem.ItemAmount;

                        if (newItem.IsTemporary)
                            newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                        newItem.EndDate = DateTime.Now.AddDays(7);

                        if (character.GiftWarehouse.AddItem(newItem))
                        {
                            await _sender.Send(new UpdateItemsCommand(character.GiftWarehouse));

                            _logger.Information($"ItemID {newItem.ItemId} x{newItem.Amount} added to [{tamerId}:{character.Name}] GiftWarehouse.");
                        }
                        else
                        {
                            _logger.Error($"Failed to send item {newItem.ItemId} to [{tamerId}:{character.Name}] GiftWarehouse.");
                        }
                    }
                }
                else
                {
                    _logger.Information($"No configured weekly rewards in XML for TamerId: {targetTamer.TamerId} at Rank: {tamerRank}.");
                }
            }
        }

        /// <summary>
        /// Distribui as recompensas de rank sazonal da Arena aos competidores.
        /// </summary>
        /// <param name="rankingSeasonalInfo">Informações do ranking sazonal da Arena.</param>
        public async Task DistributeSeasonalRankRewards(ArenaRankingModel rankingSeasonalInfo)
        {
            const int seasonalRankKeyValue = (int)ArenaRankingEnum.Seasonal;

            if (rankingSeasonalInfo?.Competitors == null || !rankingSeasonalInfo.Competitors.Any())
            {
                _logger.Information("No competitors found in seasonal ranking. No rewards to distribute.");
                return;
            }

            int totalPlayersOfType1Seasonal = rankingSeasonalInfo.Competitors.Count(c => c.Position > 10);

            foreach (var targetTamer in rankingSeasonalInfo.Competitors.OrderBy(c => c.Position))
            {
                var tamerRank = targetTamer.Position;
                var tamerId = targetTamer.TamerId;

                int sNRankTypeForSeasonal = tamerRank <= 10 ? 0 : 1;

                CharacterModel? character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByIdQuery(tamerId)));

                if (character == null || character.Partner == null)
                {
                    _logger.Error($"Invalid character information for tamer id {tamerId}.");
                    continue;
                }

                List<InfiniteWar_RankRewardItemXmlModel> rewardsToGive = GetRewardsForRank(tamerRank, seasonalRankKeyValue, sNRankTypeForSeasonal, totalPlayersOfType1Seasonal);

                if (rewardsToGive.Any())
                {
                    _logger.Information($"Distributing rewards for TamerId: [{tamerId}:{character.Name}], Rank: {tamerRank} (Seasonal):");

                    foreach (var rewardItem in rewardsToGive)
                    {
                        var newItem = new ItemModel();
                        
                        newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(rewardItem.ItemId));

                        if (newItem.ItemInfo == null)
                        {
                            _logger.Error($"No item info found with ID {rewardItem.ItemId}.");
                            continue;
                        }

                        newItem.ItemId = rewardItem.ItemId;
                        newItem.Amount = rewardItem.ItemAmount;

                        if (newItem.IsTemporary)
                            newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                        newItem.EndDate = DateTime.Now.AddDays(7);

                        if (character.GiftWarehouse.AddItem(newItem))
                        {
                            await _sender.Send(new UpdateItemsCommand(character.GiftWarehouse));

                            _logger.Information($"ItemID {newItem.ItemId} x{newItem.Amount} added to [{tamerId}:{character.Name}] GiftWarehouse.");
                        }
                        else
                        {
                            _logger.Error($"Failed to send item {newItem.ItemId} to [{tamerId}:{character.Name}] GiftWarehouse.");
                        }
                    }
                }
                else
                {
                    _logger.Warning($"No configured seasonal rewards in XML for TamerId: {targetTamer.TamerId} at Rank: {tamerRank}.");
                }
            }
        }
    }
}
