using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigiEggGameLimitPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MiniGameLimit;

        private readonly ILogger _logger;

        public DigiEggGameLimitPacketProcessor(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            int limit = 0;
            try
            {
                limit = packet.ReadInt();
            }
            catch { }

            _logger.Information($"[DigiEggGameLimit] Player {client.Tamer.Name} reported limit {limit}.");

            client.Send(new HatchMiniGameLimitPacket(limit));

            await Task.CompletedTask;
        }
    }
}