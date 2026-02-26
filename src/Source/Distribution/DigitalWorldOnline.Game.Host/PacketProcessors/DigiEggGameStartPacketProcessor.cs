using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using Serilog;
using System;
using System.Threading.Tasks;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    /// <summary>
    /// Handles packet 5007: DigiEgg Mini-Game UI Started Response
    /// Confirms to the server that the client's UI has initialized and is ready to play the minigame
    /// 
    /// This is a client→server packet confirming the minigame UI is ready
    /// Server should only start the minigame after receiving this packet
    /// </summary>
    public class DigiEggGameStartPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigiEggGameStart; // ID: 5007

        private readonly ILogger _logger;

        // Game parameters
        private const int GAME_TYPE_DIGIMON_SEQUENCE = 1;
        private const int DEFAULT_TIME_LIMIT = 30; // seconds
        private const string TARGET_SEQUENCE = "DIGIMON";
        private const int SEQUENCE_LENGTH = 7; // "DIGIMON".Length

        public DigiEggGameStartPacketProcessor(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            try
            {
                var packet = new GamePacketReader(packetData);

                // Read acknowledgement status from client
                // Status: 1 = UI Ready, 0 = UI Not Ready / Error
                byte ackStatus = packet.ReadByte();

                if (ackStatus == 1)
                {
                    // ✅ CLIENT UI IS READY - Game can start
                    _logger.Information($"[DigiEggGameStart] ✓ Player {client.Tamer.Name} UI initialized and ready for minigame");

                    // Send confirmation that server is ready to begin
                    SendGameReadyConfirmation(client, true);

                    // Send game initialization data to client
                    SendGameStartInstructions(client);

                    client.Send(new SystemMessagePacket("🎮 Minigame starting! Click the buttons in the correct order to spell 'DIGIMON'"));
                }
                else
                {
                    // ❌ CLIENT UI FAILED TO INITIALIZE
                    _logger.Warning($"[DigiEggGameStart] ✗ Player {client.Tamer.Name} UI failed to initialize (status: {ackStatus})");
                    client.Send(new SystemMessagePacket("❌ Minigame UI failed to load. Please try again."));

                    // Send error response
                    SendGameReadyConfirmation(client, false);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameStart] Error processing packet from player {client?.Tamer?.Name}: {ex.Message}");
                client?.Send(new SystemMessagePacket("Error starting minigame. Please try again."));
            }
        }

        /// <summary>
        /// Sends game ready confirmation to client using DigiEggGameStartPacket
        /// </summary>
        private void SendGameReadyConfirmation(GameClient client, bool isReady)
        {
            try
            {
                // ✅ Using new DigiEggGameStartPacket class - basic confirmation
                byte readyStatus = isReady ? (byte)1 : (byte)0;
                var confirmationPacket = new DigiEggGameStartPacket(readyStatus);

                client.Send(confirmationPacket.Serialize());

                _logger.Debug($"[DigiEggGameStart] Sent ready confirmation to {client.Tamer.Name}: ready={isReady}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameStart] Error sending game ready confirmation: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends game instructions and parameters to client using DigiEggGameStartPacket
        /// </summary>
        private void SendGameStartInstructions(GameClient client)
        {
            try
            {
                // ✅ Using new DigiEggGameStartPacket class - with full game parameters
                var instructionsPacket = new DigiEggGameStartPacket(
                    1, // isReady = true
                    GAME_TYPE_DIGIMON_SEQUENCE,
                    DEFAULT_TIME_LIMIT,
                    SEQUENCE_LENGTH,
                    TARGET_SEQUENCE
                );

                client.Send(instructionsPacket.Serialize());

                _logger.Information($"[DigiEggGameStart] Sent game instructions to {client.Tamer.Name}. " +
                    $"Type: {GAME_TYPE_DIGIMON_SEQUENCE}, Time: {DEFAULT_TIME_LIMIT}s, Sequence: {TARGET_SEQUENCE}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameStart] Error sending game instructions: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends basic game parameters (with time limit and sequence)
        /// </summary>
        private void SendGameParameters(GameClient client)
        {
            try
            {
                // ✅ Using new DigiEggGameStartPacket class - medium detail
                var parametersPacket = new DigiEggGameStartPacket(
                    1, // isReady
                    GAME_TYPE_DIGIMON_SEQUENCE,
                    DEFAULT_TIME_LIMIT,
                    SEQUENCE_LENGTH,
                    TARGET_SEQUENCE
                );

                client.Send(parametersPacket.Serialize());

                _logger.Debug($"[DigiEggGameStart] Sent game parameters to {client.Tamer.Name}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameStart] Error sending game parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends error response to client
        /// </summary>
        private void SendErrorResponse(GameClient client, string errorMessage)
        {
            try
            {
                // ✅ Using new DigiEggGameStartPacket class - error response
                var errorPacket = new DigiEggGameStartPacket(0); // isReady = false
                client.Send(errorPacket.Serialize());

                _logger.Warning($"[DigiEggGameStart] Sent error response to {client.Tamer.Name}: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigiEggGameStart] Error sending error response: {ex.Message}");
            }
        }
    }
}