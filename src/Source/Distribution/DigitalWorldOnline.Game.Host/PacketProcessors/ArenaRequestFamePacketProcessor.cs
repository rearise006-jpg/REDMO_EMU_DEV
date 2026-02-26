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
    public class ArenaRequestFamePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArenaRequestFame;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public ArenaRequestFamePacketProcessor(ILogger logger, ISender sender, IMapper mapper)
        {
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            short season = 0;

            var arena = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetArenaOldRankingQuery(ArenaRankingEnum.Seasonal)));

            client.Send(new ArenaRequestFamePacket(season, arena));
        }
    }
}