using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigiEggGameCompletePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MiniGameEnd;

        private readonly ILogger _logger;
        private readonly ISender _sender;

        // NOTE: ISender is injected so we can persist incubator changes.
        public DigiEggGameCompletePacketProcessor(ILogger logger, ISender sender)
        {
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            // Parse client-submitted values (we'll not trust the score sent by client;
            // we'll use server-side session data to compute real score).
            int clientScore = 0;
            long clientElapsed = 0;
            byte clientStatus = 0;
            try
            {
                clientScore = packet.ReadInt();
                clientElapsed = packet.ReadInt64();
                clientStatus = packet.ReadByte();
            }
            catch
            {
                // If structure differs, ignore - we'll fallback to session based result
            }

            var session = client.Tamer.Incubator.MiniGameSession;

            int serverScore;
            byte finalStatus = clientStatus;
            long elapsedToReport = clientElapsed;

            if (session != null && session.IsActive)
            {
                // Compute server-side score from session clicked flags
                serverScore = session.Clicked?.Count(x => x) ?? 0;

                // Determine final status server-side:
                // - If all bars clicked -> Success
                // - If none clicked -> Break
                // - Otherwise -> Fail
                if (session.TotalBars > 0 && serverScore >= session.TotalBars)
                    finalStatus = (byte)HatchMiniGameResultEnum.Success;
                else if ((session.Clicked == null || serverScore == 0))
                    finalStatus = (byte)HatchMiniGameResultEnum.Break;
                else
                    finalStatus = (byte)HatchMiniGameResultEnum.Fail;

                // If we have no reliable elapsed on session, prefer clientElapsed; keep as-is.
                elapsedToReport = clientElapsed;

                // Update incubator model: increase mini-games count and add success bonus according to serverScore
                try
                {
                    client.Tamer.Incubator.MiniGamesPlayed++;

                    // Simple bonus mapping: example 1% per success (adjust to your design).
                    double bonusPercent = serverScore * 1.0;
                    client.Tamer.Incubator.AddSuccessBonus(bonusPercent);

                    // Persist incubator changes to DB via command (if available)
                    if (_sender != null)
                    {
                        await _sender.Send(new UpdateIncubatorCommand(client.Tamer.Incubator));
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.Warning(ex, $"[DigiEggGameComplete] Failed update incubator for player {client.Tamer.Name}.");
                }

                // End the transient session so further clicks won't affect this finished game
                session.End();
                client.Tamer.Incubator.MiniGameSession = null;

                _logger.Information($"[DigiEggGameComplete] Player {client.Tamer.Name} completed mini-game. ServerScore={serverScore}, ClientScore={clientScore}, Status(server)={finalStatus}");
            }
            else
            {
                // No server session - fallback: use client provided values (but log warning)
                _logger.Warning($"[DigiEggGameComplete] No active mini-game session for {client.Tamer.Name}. Using client-provided score ({clientScore}).");
                serverScore = clientScore;
            }

            // Send end packet: use serverScore and finalStatus
            try
            {
                client.Send(new HatchMiniGameEndPacket(serverScore, elapsedToReport, finalStatus));
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, $"[DigiEggGameComplete] Failed to send HatchMiniGameEndPacket to {client.Tamer.Name}.");
            }
        }
    }
}