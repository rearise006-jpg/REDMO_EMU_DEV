using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigiEggGameInputPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MiniGameClickBtn;

        private readonly ILogger _logger;

        public DigiEggGameInputPacketProcessor(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            // client -> server input: guess 1 byte action + int rawButton (loglardan çıkarım)
            int clickAction = 0;
            int rawButton = 0;
            try
            {
                clickAction = packet.ReadInt(); // bazı clientler int yolluyor
                rawButton = packet.ReadInt();
            }
            catch
            {
                // fallback: try reading smaller types
                try { clickAction = packet.ReadByte(); } catch { }
                try { rawButton = packet.ReadInt(); } catch { }
            }

            var session = client.Tamer.Incubator.MiniGameSession;
            int totalBars = session?.TotalBars ?? 7;
            int clickedIndex = CalculateIndexFromRaw(rawButton, totalBars);

            _logger.Debug($"[DigiEggGameInput] Player {client.Tamer.Name} clicked button: {rawButton} (index: {clickedIndex})");

            if (session == null || !session.IsActive)
            {
                _logger.Warning($"[DigiEggGameInput] No active mini-game session for player {client.Tamer.Name} - ignoring click.");
                return;
            }

            // Save current expected before attempt (for logs)
            int expectedBefore = session.ExpectedIndex;

            if (!session.RegisterClick(clickedIndex))
            {
                _logger.Warning($"[DigiEggGameInput] Click REJECTED for player {client.Tamer.Name}. ClickedIndex={clickedIndex} Expected={expectedBefore}");
                // Send ClickResult with failure (nResult = 0), and keep nBarIndex as current expected (server side will not advance)
                ushort barTime = (ushort)session.BarDurationMs;
                ushort nextIndex = (ushort)session.ExpectedIndex; // still expected
                client.Send(new HatchMiniGameClickPacket(false, nextIndex, barTime));
            }
            else
            {
                _logger.Information($"[DigiEggGameInput] Click ACCEPTED for player {client.Tamer.Name}. Index={clickedIndex}");

                // After RegisterClick, session.ExpectedIndex has advanced to next expected index (or session ended)
                ushort nextIndex = (ushort)(session.IsActive ? session.ExpectedIndex : session.TotalBars); // if ended give totalBars
                ushort barTime = (ushort)session.BarDurationMs;

                // Send ClickResult success -> client will mark clicked bar and start next bar using nBarIndex/nBarTime
                client.Send(new HatchMiniGameClickPacket(true, nextIndex, barTime));

                // If session finished just after this click, you could send End (5008) later when client notifies completion.
            }

            await Task.CompletedTask;
        }

        private int CalculateIndexFromRaw(int rawButton, int totalBars)
        {
            if (totalBars <= 0) totalBars = 7;
            if (rawButton >= 0 && rawButton < totalBars) return rawButton;
            try { return Math.Abs(rawButton) % totalBars; } catch { return 0; }
        }
    }
}