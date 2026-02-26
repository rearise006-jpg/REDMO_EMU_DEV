using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    /// <summary>
    /// Handles packet 5008: DigiEgg Mini-Game Completed
    /// Receives the final game results from the client and processes the score/bonus
    /// </summary>
    public class DigiEggGameCompletePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigiEggGameComplete; // ID: 5008

        private readonly ILogger _logger;
        private readonly ISender _sender;

        // Store active game sessions for validation
        private readonly Dictionary<long, DigiEggGameSessionData> _gameSessions =
            new Dictionary<long, DigiEggGameSessionData>();

        public DigiEggGameCompletePacketProcessor(ILogger logger, ISender sender)
        {
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            try
            {
                var packet = new GamePacketReader(packetData);

                // Read game completion data
                int sessionId = packet.ReadInt();
                int finalScore = packet.ReadInt();      // Total score/clicks completed
                int gameTime = packet.ReadInt();        // Time spent in seconds
                byte completionStatus = packet.ReadByte(); // 1 = Completed, 0 = Cancelled

                _logger.Information($"[DigiEggGameComplete] Player {client.Tamer.Name} submitted minigame results. " +
                    $"Score: {finalScore}, Time: {gameTime}s, Status: {(completionStatus == 1 ? "Completed" : "Cancelled")}");

                // Check if game was cancelled
                if (completionStatus == 0)
                {
                    _logger.Information($"[DigiEggGameComplete] Player {client.Tamer.Name} cancelled minigame - no bonus applied");
                    SendGameCompleteResponse(client, 0, finalScore, 0); // Failed response

                    client.Send(new SystemMessagePacket("Minigame cancelled. No bonus applied."));

                    // Clean up
                    if (_gameSessions.ContainsKey(client.TamerId))
                    {
                        _gameSessions.Remove(client.TamerId);
                    }

                    await Task.CompletedTask;
                    return;
                }

                // Validate score
                if (finalScore < 0 || finalScore > 100)
                {
                    _logger.Warning($"[DigiEggGameComplete] Invalid score {finalScore} from player {client.Tamer.Name}. " +
                        "Score must be between 0-100");

                    SendGameCompleteResponse(client, 0, finalScore, 0); // Failed response
                    client.Send(new SystemMessagePacket("Invalid score received. Please try again."));
                    return;
                }

                // Check if player has active incubator with egg
                if (client.Tamer.Incubator == null || client.Tamer.Incubator.EggId <= 0)
                {
                    _logger.Warning($"[DigiEggGameComplete] Player {client.Tamer.Name} has no active egg to incubate");

                    SendGameCompleteResponse(client, 0, finalScore, 0); // Failed response
                    client.Send(new SystemMessagePacket("No active egg in incubator. Insert an egg first."));
                    return;
                }

                // Calculate hatch level bonus based on score
                double successBonus = CalculateSuccessBonus(finalScore);
                int hatchLevelIncrease = (int)(successBonus / 5);

                _logger.Debug($"[DigiEggGameComplete] Calculated bonus for player {client.Tamer.Name}: " +
                    $"{successBonus:F2}% ({hatchLevelIncrease} levels) from score {finalScore}");

                // Apply bonus to incubator
                bool bonusApplied = ApplyGameBonus(client, hatchLevelIncrease);

                if (bonusApplied)
                {
                    _logger.Information($"[DigiEggGameComplete] Player {client.Tamer.Name} bonus applied successfully. " +
                        $"Hatch increase: {hatchLevelIncrease} level(s)");

                    // Send success response using new packet class
                    SendGameCompleteResponse(client, 1, finalScore, (int)(successBonus * 100), hatchLevelIncrease, gameTime);

                    // Save incubator changes to database
                    await _sender.Send(new UpdateIncubatorCommand(client.Tamer.Incubator));

                    client.Send(new SystemMessagePacket($"✅ Minigame complete! Hatch level increased by {hatchLevelIncrease} level(s)!"));
                }
                else
                {
                    _logger.Error($"[DigiEggGameComplete] Failed to apply bonus for player {client.Tamer.Name}");

                    SendGameCompleteResponse(client, 0, finalScore, 0); // Failed response
                    client.Send(new SystemMessagePacket("❌ Error applying bonus. Please try again."));
                }

                // Clean up session
                if (_gameSessions.ContainsKey(client.TamerId))
                {
                    _gameSessions.Remove(client.TamerId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameComplete] Error processing packet for player {client?.Tamer?.Name}: {ex.Message}");
                client?.Send(new SystemMessagePacket("Error processing minigame results. Please try again."));
            }
        }

        /// <summary>
        /// Sends game completion response to client using DigiEggGameCompletePacket
        /// </summary>
        private void SendGameCompleteResponse(GameClient client, byte success, int finalScore, int bonusPercentage)
        {
            try
            {
                // ✅ Using new DigiEggGameCompletePacket class - basic response
                var responsePacket = new DigiEggGameCompletePacket(success, finalScore, bonusPercentage);
                client.Send(responsePacket.Serialize());

                _logger.Debug($"[DigiEggGameComplete] Sent completion response to {client.Tamer.Name}: " +
                    $"success={success}, score={finalScore}, bonus={bonusPercentage}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameComplete] Error sending game complete response: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends detailed game completion response with hatch level increase info
        /// </summary>
        private void SendGameCompleteResponse(GameClient client, byte success, int finalScore, int bonusPercentage, int hatchLevelIncrease, int totalPlayTime)
        {
            try
            {
                // ✅ Using new DigiEggGameCompletePacket class - detailed response
                var detailedResponsePacket = new DigiEggGameCompletePacket(
                    success,
                    finalScore,
                    bonusPercentage,
                    hatchLevelIncrease,
                    totalPlayTime
                );
                client.Send(detailedResponsePacket.Serialize());

                _logger.Information($"[DigiEggGameComplete] Sent detailed response to {client.Tamer.Name}: " +
                    $"success={success}, score={finalScore}, bonus={bonusPercentage}, hatchIncrease={hatchLevelIncrease}, time={totalPlayTime}s");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameComplete] Error sending detailed game complete response: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates success bonus based on game score
        /// Score represents percentage of sequence completed (0-100)
        /// </summary>
        private double CalculateSuccessBonus(int totalScore)
        {
            // Convert score percentage (0-100) to hatch level increment
            // 0-20%: -5 levels
            // 20-40%: 0 levels
            // 40-60%: +5 levels
            // 60-80%: +10 levels
            // 80-100%: +15 levels

            return totalScore switch
            {
                >= 80 => 15.0,  // Excellent
                >= 60 => 10.0,  // Good
                >= 40 => 5.0,   // Average
                >= 20 => 0.0,   // Below Average
                _ => -5.0       // Poor
            };
        }

        /// <summary>
        /// ✅ FIXED: Applies the game bonus to the incubator using SetHatchLevel method
        /// </summary>
        // ... Önceki kod ...

        /// <summary>
        /// ✅ FIXED: Başarı oranını decimal olarak güncelle
        /// </summary>
        private bool ApplyGameBonus(GameClient client, int hatchLevelIncrease)
        {
            try
            {
                if (client.Tamer.Incubator == null)
                    return false;

                // Hatch level'i güncelle
                int oldLevel = client.Tamer.Incubator.HatchLevel;
                int newLevel = oldLevel + hatchLevelIncrease;
                client.Tamer.Incubator.SetHatchLevel(newLevel);

                // ✅ FIXED: Başarı oranını decimal olarak güncelle
                double successBonus = CalculateSuccessBonus(finalScore);
                decimal bonusDecimal = (decimal)successBonus;

                client.Tamer.Incubator.AddSuccessBonus(successBonus);

                _logger.Debug($"[DigiEggGameComplete] Applied hatch bonus to {client.Tamer.Name}. " +
                    $"Old Level: {oldLevel}, New Level: {newLevel}, " +
                    $"Success Rate: {client.Tamer.Incubator.CurrentSuccessRate:F2}%");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameComplete] Error applying game bonus: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Represents a minigame session
        /// </summary>
        public class DigiEggGameSessionData
        {
            public int SessionId { get; set; }
            public long TamerId { get; set; }
            public DateTime StartTime { get; set; }
            public int EggId { get; set; }
            public int Score { get; set; }
            public int Duration { get; set; } // In seconds
            public byte Status { get; set; } // 0 = Active, 1 = Completed, 2 = Cancelled

            public override string ToString()
            {
                return $"[Session:{SessionId} Tamer:{TamerId} Egg:{EggId} Score:{Score} Duration:{Duration}s Status:{Status}]";
            }
        }
    }
}