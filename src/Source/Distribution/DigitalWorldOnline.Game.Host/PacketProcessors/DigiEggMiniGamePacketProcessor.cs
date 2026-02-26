using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Game;
using MediatR;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    /// <summary>
    /// Handles packet 5005: DigiEgg Mini-Game Interaction
    /// This processor manages the mini-game played during incubation
    /// </summary>
    public class DigiEggMiniGamePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigiEggMiniGame; // ID: 5005

        private readonly AssetsLoader _assets;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly Dictionary<long, DigiEggGameSession> _activeSessions;

        public DigiEggMiniGamePacketProcessor(AssetsLoader assets, ILogger logger, ISender sender)
        {
            _assets = assets;
            _logger = logger;
            _sender = sender;
            _activeSessions = new Dictionary<long, DigiEggGameSession>();
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            try
            {
                var packet = new GamePacketReader(packetData);
                byte actionType = packet.ReadByte();

                switch (actionType)
                {
                    case 1: // Start mini-game
                        await HandleStartMiniGame(client);
                        break;
                    case 2: // Button Click Result
                        // ✅ FIXED: Use ReadUShort extension method
                        ushort barIndex = packet.ReadUShort();
                        await HandleButtonClick(client, barIndex);
                        break;
                    case 3: // Timeout
                        await HandleTimeout(client);
                        break;
                    case 4: // Game End / Submit Result
                        // ✅ FIXED: Use ReadUInt32 extension method
                        uint finalScore = packet.ReadUInt32();
                        await HandleGameEnd(client, finalScore);
                        break;
                    case 6: // Game Start Ready (Client Response)
                        await HandleGameStartReady(client);
                        break;
                    default:
                        _logger.Warning($"Unknown DigiEgg mini-game action type: {actionType} from player {client.Tamer.Name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggMiniGame] Error processing packet for player {client?.Tamer?.Name}: {ex.Message}");
                client?.Send(new SystemMessagePacket("An error occurred during the mini-game. Please try again."));
            }
        }

        /// <summary>
        /// Handles the mini-game start request
        /// </summary>
        private async Task HandleStartMiniGame(GameClient client)
        {
            // Check if player has active incubator with egg
            if (client.Tamer.Incubator == null || client.Tamer.Incubator.EggId <= 0)
            {
                client.Send(new HatchMiniGameErrorPacket(HatchMiniGameErrorEnum.PreviousGameNotReset));
                _logger.Warning($"Player {client.Tamer.Name} attempted mini-game without egg in incubator");
                return;
            }

            // Check if already in a game session
            if (_activeSessions.ContainsKey(client.TamerId))
            {
                client.Send(new HatchMiniGameErrorPacket(HatchMiniGameErrorEnum.PreviousGameNotReset));
                return;
            }

            try
            {
                // Create new game session
                var gameSession = new DigiEggGameSession
                {
                    TamerId = client.TamerId,
                    EggId = client.Tamer.Incubator.EggId,
                    SessionStartTime = DateTime.UtcNow,
                    MaxDuration = TimeSpan.FromSeconds(30),
                    CurrentBarIndex = 0,
                    SuccessCount = 0,
                    BarTimings = new List<ushort> { 3500, 3500, 3500, 3500, 3500, 3500, 3500 }
                };

                _activeSessions[client.TamerId] = gameSession;

                // Send game start packet with first bar timing
                SendGameStart(client, gameSession, stage: 0);

                _logger.Information($"Mini-game started for player {client.Tamer.Name}, egg ID: {gameSession.EggId}");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error starting mini-game for {client.Tamer.Name}: {ex.Message}");
                client.Send(new HatchMiniGameErrorPacket(HatchMiniGameErrorEnum.PreviousGameNotReset));
            }
        }

        /// <summary>
        /// Handles button click from client
        /// </summary>
        private async Task HandleButtonClick(GameClient client, ushort barIndex)
        {
            if (!_activeSessions.TryGetValue(client.TamerId, out var session))
            {
                _logger.Warning($"Player {client.Tamer.Name} clicked button without active game session");
                return;
            }

            // Validate bar index
            if (barIndex >= 7)
            {
                _logger.Warning($"Player {client.Tamer.Name} clicked invalid bar index: {barIndex}");
                return;
            }

            // Calculate if click was successful
            var result = CalculateClickResult(session, barIndex);
            session.ClickResults.Add((barIndex, result));

            // If successful, increment success count
            if (result == HatchMiniGameResultEnum.Success)
            {
                session.SuccessCount++;
            }

            // Prepare next bar
            ushort nextBarIndex = (ushort)(barIndex + 1);

            // ✅ Check if this is the last bar BEFORE calculating nextBarTime
            if (barIndex >= 6) // Last bar (index 6 is the 7th bar)
            {
                // Game complete after last click
                session.IsComplete = true;
                await HandleGameEnd(client, session.SuccessCount);
                return;
            }

            // ✅ FIXED: Explicit cast to int to avoid ambiguity
            ushort nextBarTime = session.BarTimings[(int)Math.Min((int)nextBarIndex, 6)];

            // Send click result with next bar info
            client.Send(new HatchMiniGameClickResultPacket(result, nextBarIndex, nextBarTime).Serialize());

            // Update session
            session.CurrentBarIndex = nextBarIndex;

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles timeout event
        /// </summary>
        private async Task HandleTimeout(GameClient client)
        {
            if (!_activeSessions.TryGetValue(client.TamerId, out var session))
            {
                return;
            }

            // Record timeout
            session.ClickResults.Add((session.CurrentBarIndex, HatchMiniGameResultEnum.Break));

            // Move to next bar or end game
            ushort nextBarIndex = (ushort)(session.CurrentBarIndex + 1);

            if (nextBarIndex >= 7) // Game complete
            {
                session.IsComplete = true;
                await HandleGameEnd(client, session.SuccessCount);
                return;
            }

            // Move to next bar
            session.CurrentBarIndex = nextBarIndex;
            // ✅ FIXED: Explicit cast to int
            ushort nextBarTime = session.BarTimings[(int)Math.Min((int)nextBarIndex, 6)];

            // Send timeout and next bar info
            client.Send(new HatchMiniGameClickResultPacket(HatchMiniGameResultEnum.Break, nextBarIndex, nextBarTime).Serialize());

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles game end/completion
        /// </summary>
        private async Task HandleGameEnd(GameClient client, uint finalSuccessCount)
        {
            if (!_activeSessions.Remove(client.TamerId, out var session))
            {
                _logger.Warning($"Game end called but no session found for {client.Tamer.Name}");
                return;
            }

            try
            {
                // Send game end packet with final result
                client.Send(new HatchMiniGameEndPacket(finalSuccessCount).Serialize());

                // Calculate hatch rate bonus based on success count
                double bonus = CalculateHatchBonus(finalSuccessCount);

                // Apply bonus to incubator
                if (client.Tamer.Incubator != null)
                {
                    const int BASE_HATCH_INCREMENT = 5;
                    // ✅ FIXED: Cast to int for calculation
                    int hatchIncrement = (int)(BASE_HATCH_INCREMENT + (finalSuccessCount * 1.5));

                    // ✅ FIXED: Use IncreaseLevelBy method with proper type
                    client.Tamer.Incubator.IncreaseLevelBy(hatchIncrement);

                    await _sender.Send(new UpdateIncubatorCommand(client.Tamer.Incubator));
                }

                _logger.Information($"Mini-game ended for {client.Tamer.Name}. Success count: {finalSuccessCount}");

                client.Send(new SystemMessagePacket($"Mini-game complete! Hatch rate increased!"));
            }
            catch (Exception ex)
            {
                _logger.Error($"Error ending mini-game for {client.Tamer.Name}: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles game start ready response from client
        /// </summary>
        private async Task HandleGameStartReady(GameClient client)
        {
            if (_activeSessions.TryGetValue(client.TamerId, out var session))
            {
                session.IsClientReady = true;
                _logger.Information($"Client ready for minigame: {client.Tamer.Name}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Sends the game start packet with first bar timing
        /// </summary>
        private void SendGameStart(GameClient client, DigiEggGameSession session, byte stage = 0)
        {
            try
            {
                // ✅ FIXED: Null check for session
                if (session == null)
                {
                    _logger.Error($"SendGameStart: Session is null for player {client.Tamer.Name}");
                    return;
                }

                // ✅ FIXED: Validate bar timings exist
                if (session.BarTimings == null || session.BarTimings.Count == 0)
                {
                    _logger.Error($"SendGameStart: BarTimings is empty for player {client.Tamer.Name}");
                    return;
                }

                client.Send(new HatchMiniGameStartPacket(session.BarTimings[0], stage).Serialize());

                _logger.Information($"Sent HatchMiniGameStart packet to player {client.Tamer.Name} with stage {stage}, first bar time: {session.BarTimings[0]}ms");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error sending game start packet to {client.Tamer.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates if click was successful based on timing
        /// </summary>
        private HatchMiniGameResultEnum CalculateClickResult(DigiEggGameSession session, ushort barIndex)
        {
            var random = new Random();

            // Success rate: 70% for early bars, decreases for later bars
            double successRate = 70.0 - (barIndex * 5.0);
            successRate = Math.Max(30.0, successRate); // Minimum 30%

            return random.NextDouble() * 100 < successRate
                ? HatchMiniGameResultEnum.Success
                : HatchMiniGameResultEnum.Fail;
        }

        /// <summary>
        /// Calculates hatch rate bonus based on success count
        /// </summary>
        private double CalculateHatchBonus(uint successCount)
        {
            return successCount switch
            {
                0 => -4.0,
                1 => -3.0,
                2 => -2.0,
                3 => -1.0,
                4 => 1.0,
                5 => 2.0,
                6 => 3.0,
                7 => 4.0,
                _ => 0.0
            };
        }

        /// <summary>
        /// Represents an active DigiEgg mini-game session
        /// </summary>
        public class DigiEggGameSession
        {
            public long TamerId { get; set; }
            public int EggId { get; set; }
            public DateTime SessionStartTime { get; set; }
            public TimeSpan MaxDuration { get; set; }
            public ushort CurrentBarIndex { get; set; }
            public uint SuccessCount { get; set; }
            public bool IsComplete { get; set; }
            public bool IsClientReady { get; set; }
            public List<ushort> BarTimings { get; set; } = new();
            public List<(ushort BarIndex, HatchMiniGameResultEnum Result)> ClickResults { get; set; } = new();
        }
    }
}