using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigiEggGameTimeoutPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MiniGameTimeOut;

        private readonly ILogger _logger;

        public DigiEggGameTimeoutPacketProcessor(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            _logger.Information($"[DigiEggGameTimeout] Player {client.Tamer.Name} mini-game timeout.");

            // Gerekirse client'a timeout packet'ı gönder
            ushort barTime = 2000; // varsayılan
            var session = client.Tamer.Incubator.MiniGameSession;
            if (session != null)
            {
                barTime = (ushort)session.BarDurationMs;
            }

            // Yeni imzaya göre gönder
            client.Send(new HatchMiniGameTimeOutPacket(barTime)); ;

            await Task.CompletedTask;

            // örnek yerleştirme
            if (session != null && session.IsActive)
            {
                _logger.Information($"[DigiEggGameTimeout] Advancing session for {client.Tamer.Name} (Expected was {session.ExpectedIndex})");
                session.Advance();
            }
        }
    }
}