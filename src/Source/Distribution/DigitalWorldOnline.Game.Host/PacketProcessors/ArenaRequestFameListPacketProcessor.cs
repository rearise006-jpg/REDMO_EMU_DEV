using AutoMapper;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ArenaRequestFameListPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArenaRequestFameList;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public ArenaRequestFameListPacketProcessor(ILogger logger, ISender sender, IMapper mapper)
        {
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            short seasonSize = 7;
            byte seasonIndex = 0;

            client.Send(new ArenaRequestFameListPacket(seasonSize, seasonIndex));
        }
    }
}