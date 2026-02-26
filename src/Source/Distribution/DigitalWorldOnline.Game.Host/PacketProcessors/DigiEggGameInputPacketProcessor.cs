using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    /// <summary>
    /// Handles packet 5006: DigiEgg Mini-Game Button Input/Click Events
    /// This processor tracks the sequence of button clicks ("DIGIMON" sequence) during the minigame
    /// 
    /// The minigame requires players to click buttons in the correct order to spell "DIGIMON"
    /// Each correct click advances the sequence, wrong clicks reset it
    /// </summary>
    public class DigiEggGameInputPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigiEggGameInput; // ID: 5006

        private readonly ILogger _logger;
        private readonly Dictionary<long, GameSequenceTracker> _sequenceTrackers;

        // Target sequence for the minigame
        private const string TARGET_SEQUENCE = "DIGIMON";

        public DigiEggGameInputPacketProcessor(ILogger logger)
        {
            _logger = logger;
            _sequenceTrackers = new Dictionary<long, GameSequenceTracker>();
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            try
            {
                var packet = new GamePacketReader(packetData);

                // ✅ FIXED: Client sends ushort (2 bytes), not int!
                ushort rawButtonIndex = packet.ReadUShort();  // Changed from ReadInt() to ReadUShort()

                // Convert to 0-indexed for string comparison
                int buttonIndex = rawButtonIndex - 1;

                _logger.Information($"[DigiEggGameInput] Player {client.Tamer.Name} clicked button: {rawButtonIndex} (index: {buttonIndex})");

                // Validate button index range
                if (buttonIndex < 0 || buttonIndex >= TARGET_SEQUENCE.Length)
                {
                    _logger.Warning($"[DigiEggGameInput] Invalid button index {buttonIndex} from player {client.Tamer.Name}");
                    SendClickResponse(client, 0);
                    return;
                }

                // Get or create tracker for this player
                if (!_sequenceTrackers.TryGetValue(client.TamerId, out var tracker))
                {
                    tracker = new GameSequenceTracker();
                    _sequenceTrackers[client.TamerId] = tracker;
                }

                if (buttonIndex == tracker.CurrentPosition)
                {
                    // ✅ CORRECT CLICK
                    _logger.Information($"[DigiEggGameInput] ✓ CORRECT! Player {client.Tamer.Name} position {tracker.CurrentPosition + 1}/{TARGET_SEQUENCE.Length}");

                    tracker.CorrectClicks++;
                    tracker.CurrentPosition++;

                    // Send success acknowledgement
                    SendClickResponse(client, 1);

                    // Check if sequence is complete
                    if (tracker.CurrentPosition >= TARGET_SEQUENCE.Length)
                    {
                        _logger.Information($"[DigiEggGameInput] 🎉 SEQUENCE COMPLETE! Player {client.Tamer.Name} finished minigame!");
                        SendSequenceComplete(client);

                        // Clean up session
                        _sequenceTrackers.Remove(client.TamerId);

                        // Send confirmation message
                        client.Send(new SystemMessagePacket("🎉 Minigame sequence complete! Bonus applied!"));
                    }
                }
                else
                {
                    // ❌ WRONG CLICK - Reset sequence
                    _logger.Warning($"[DigiEggGameInput] ✗ WRONG! Player {client.Tamer.Name} clicked wrong button. " +
                        $"Expected position {tracker.CurrentPosition}, but got {buttonIndex}");

                    tracker.WrongClicks++;

                    // Reset to beginning
                    tracker.CurrentPosition = 0;

                    // Send fail acknowledgement
                    SendClickResponse(client, 0);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameInput] Error processing button click for player {client?.Tamer?.Name}: {ex.Message}");
                client?.Send(new SystemMessagePacket("An error occurred. Please try again."));
            }
        }

        /// <summary>
        /// Sends button click response to client using DigiEggGameInputPacket
        /// Status: 1 = Correct, 0 = Wrong
        /// </summary>
        private void SendClickResponse(GameClient client, byte status)
        {
            try
            {
                // ✅ Using new DigiEggGameInputPacket class
                var responsePacket = new DigiEggGameInputPacket(status);
                client.Send(responsePacket.Serialize());

                _logger.Debug($"[DigiEggGameInput] Sent click response to {client.Tamer.Name}: status={status}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameInput] Error sending click response: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends sequence complete notification to client using DigiEggGameInputPacket
        /// Status: 2 = Sequence Complete with 100% completion
        /// </summary>
        private void SendSequenceComplete(GameClient client)
        {
            try
            {
                // ✅ Using new DigiEggGameInputPacket class with completion percentage
                var completePacket = new DigiEggGameInputPacket(2, 100);
                client.Send(completePacket.Serialize());

                _logger.Information($"[DigiEggGameInput] Sent sequence complete to {client.Tamer.Name}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameInput] Error sending sequence complete: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends detailed game input response with progress information
        /// </summary>
        private void SendDetailedClickResponse(GameClient client, byte status, GameSequenceTracker tracker)
        {
            try
            {
                // ✅ Using new DigiEggGameInputPacket class with detailed info
                var detailedPacket = new DigiEggGameInputPacket(
                    status,
                    tracker.CurrentPosition,
                    TARGET_SEQUENCE.Length,
                    30 // Time remaining in seconds
                );
                client.Send(detailedPacket.Serialize());

                _logger.Debug($"[DigiEggGameInput] Sent detailed response to {client.Tamer.Name}: " +
                    $"Status={status}, Position={tracker.CurrentPosition}/{TARGET_SEQUENCE.Length}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameInput] Error sending detailed response: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tracks the state of a minigame sequence for a player
    /// </summary>
    public class GameSequenceTracker
    {
        public long TamerId { get; set; }
        public int CurrentPosition { get; set; } // Current position in TARGET_SEQUENCE
        public DateTime StartTime { get; set; }
        public int ClickCount { get; set; } // Total clicks
        public int CorrectClicks { get; set; } // Correct clicks
        public int WrongClicks { get; set; } // Wrong clicks
        public double SuccessRate => ClickCount > 0 ? (CorrectClicks / (double)ClickCount) * 100 : 0;

        public override string ToString()
        {
            return $"[Tamer:{TamerId} Pos:{CurrentPosition} Clicks:{ClickCount} Correct:{CorrectClicks} Wrong:{WrongClicks} Success:{SuccessRate:F1}%]";
        }
    }
}