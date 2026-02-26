using AutoMapper;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ArenaRequestOldRankPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArenaRequestOldRank;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public ArenaRequestOldRankPacketProcessor(ILogger logger, ISender sender, IMapper mapper)
        {
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var nType = packet.ReadByte();

            var rankingSeasonalInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetArenaOldRankingQuery(ArenaRankingEnum.Seasonal)));

            if (rankingSeasonalInfo != null)
            {
                byte result = 0;

                foreach (var targetTamer in rankingSeasonalInfo.Competitors)
                {
                    var targetInfo = await _sender.Send(new GetCharacterNameAndGuildByIdQuery(targetTamer.TamerId));

                    targetTamer.SetTamerAndGuildName(targetInfo.TamerName, targetInfo.GuildName);
                }

                client.Send(new ArenaRequestOldRankPacket(result, (int)client.TamerId, rankingSeasonalInfo, nType, ArenaRankingStatusEnum.End, ArenaRankingPositionTypeEnum.Absolut));
            }
            else
            {
                _logger.Error($"[ArenaRequestOldRank] :: No Seasonal Rank info found on database !!");
            }
        }
    }
}