using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.GameServer;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigiEggGameStartPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MiniGameStart;

        private readonly ILogger _logger;

        public DigiEggGameStartPacketProcessor(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            // Örnek parse - client'tan gelen başlatma isteği (structure client tarafına göre değişebilir)
            byte stage = 0;
            try { stage = packet.ReadByte(); } catch { }
            int reserved = 0;
            try { reserved = packet.ReadInt(); } catch { }
            try
            {
                //reserved = packet.ReadInt();
            }
            catch { /* varsa oku, yoksa ignore */ }

            _logger.Information($"[DigiEggGameStart] Player {client.Tamer.Name} requested mini-game start (stage={stage}, reserved={reserved}).");

            // Başlatma: incubator oturumunu yarat ve başlat.
            // Hatch minigame için genelde 7 bar kullanılıyor; eğer asset/egg tipine göre değişiyorsa o değeri kullanın.
            int totalBars = 7; // eğer assets'den gelecekseniz orayı okuyup setleyin
            int barDurationMs = 2000; // istenirse assets/config'tan alın

            client.Tamer.Incubator.MiniGameSession = new DigiEggMiniGameSession();
            client.Tamer.Incubator.MiniGameSession.Start(totalBars, barDurationMs);

            // Send Start according to client protocol: u2 nBarTime, u1 nStage
            client.Send(new HatchMiniGameStartPacket((ushort)barDurationMs, stage));

            // İhtiyarsa diğer init/timer işlemleri...
            await Task.CompletedTask;
        }
    }
}