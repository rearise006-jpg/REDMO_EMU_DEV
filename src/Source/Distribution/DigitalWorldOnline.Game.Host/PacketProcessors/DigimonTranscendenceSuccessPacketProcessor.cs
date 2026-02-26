using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigimonTranscendenceSuccessPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TranscendenceSuccess;

        private const string GameServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IConfiguration _configuration;
        private readonly MapServer _mapServer;
        private readonly StatusManager _statusManager;
        private readonly Random _random;

        public DigimonTranscendenceSuccessPacketProcessor(
            ILogger logger,
            ISender sender,
            IConfiguration configuration,
            MapServer mapServer,
            StatusManager statusManager)
        {
            _logger = logger;
            _sender = sender;
            _configuration = configuration;
            _mapServer = mapServer;
            _statusManager = statusManager;
            _random = new Random();
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            var isVip = packet.ReadByte();
            var targetSlot = packet.ReadInt();
            var NpcId = packet.ReadInt();
            var targetAcademySlot = packet.ReadByte();
            long price = packet.ReadInt64();

            try
            {
                var result = 0;
                var targetPartner = client.Tamer.Digimons.FirstOrDefault(x => x.Slot == targetAcademySlot);

                if (targetPartner == null)
                    return;

                var exp = targetPartner.TranscendenceExperience;

                if (targetPartner.PossibleTranscendence)
                {
                    // Remove bits and perform transcendence
                    client.Tamer.Inventory.RemoveBits(price);
                    targetPartner.Transcend();

                    // Calculate new size based on grade
                    int minSize, maxSize;
                    switch (targetPartner.HatchGrade)
                    {
                        case DigimonHatchGradeEnum.Lv6:
                            minSize = 14000;
                            maxSize = 14000;
                            break;
                        case DigimonHatchGradeEnum.Lv7:
                            minSize = 15500;
                            maxSize = 16000;
                            break;
                        case DigimonHatchGradeEnum.Lv8:
                            minSize = 17500;
                            maxSize = 18000;
                            break;
                        case DigimonHatchGradeEnum.Lv9:
                            minSize = 18500;
                            maxSize = 19000;
                            break;
                        case DigimonHatchGradeEnum.Lv10:
                            minSize = 20000;
                            maxSize = 20000;
                            break;
                        default:
                            _logger.Warning($"Unexpected grade after transcend: {targetPartner.HatchGrade}");
                            minSize = 13500;
                            maxSize = 13900;
                            break;
                    }

                    // Apply new size (cast to short)
                    int randomSize = _random.Next(minSize, maxSize + 1);
                    targetPartner.SetSize((short)randomSize);

                    // Update base stats
                    targetPartner.SetBaseStatus(
                        _statusManager.GetDigimonBaseStatus(
                            targetPartner.CurrentType,
                            targetPartner.Level,
                            targetPartner.Size));

                    _logger.Information($"Digimon {targetPartner.Id} transcended to grade {targetPartner.HatchGrade} with size {randomSize}");

                    targetPartner.ResetTranscendenceExp();

                    // ✅ Send result=1 to skip animation, then reload to show changes
                    result = 1;
                    client.Send(new DigimonTranscendenceSuccessPacket(
                        result,
                        targetAcademySlot,
                        targetPartner.HatchGrade,
                        price,
                        client.Tamer.Inventory.Bits,
                        targetPartner.TranscendenceExperience));

                    // Update database
                    await _sender.Send(new UpdateItemListBitsCommand(client.Tamer.Inventory));
                    await _sender.Send(new UpdateDigimonSizeCommand(targetPartner.Id, targetPartner.Size));
                    await _sender.Send(new UpdateDigimonGradeCommand(targetPartner.Id, targetPartner.HatchGrade));
                    await _sender.Send(new UpdateDigimonExperienceCommand(targetPartner));

                    // If this is the active partner, reload the map instantly
                    if (targetPartner == client.Tamer.Partner)
                    {
                        _logger.Verbose("Reloading map for client after transcendence size change");
                        client.Tamer.UpdateState(CharacterStateEnum.Loading);
                        await _sender.Send(new UpdateCharacterStateCommand(client.Tamer.Id, CharacterStateEnum.Loading));

                        _mapServer.RemoveClient(client);
                        client.SetGameQuit(false);
                        client.Tamer.UpdateSlots();

                        // ✅ Instant reload - no animation delay
                        client.Send(new MapSwapPacket(
                            _configuration[GameServerPublic],
                            _configuration[GameServerPort],
                            client.Tamer.Location.MapId,
                            client.Tamer.Location.X,
                            client.Tamer.Location.Y));

                        _logger.Verbose("MapSwapPacket sent to client successfully after transcendence");

                        // Restore normal state
                        client.Tamer.UpdateState(CharacterStateEnum.Ready);
                        await _sender.Send(new UpdateCharacterStateCommand(client.Tamer.Id, CharacterStateEnum.Ready));

                        _logger.Verbose("Character state restored to Normal after transcendence reload");
                    }
                }
                else
                {
                    result = 1;
                    client.Send(new DigimonTranscendenceSuccessPacket(
                        result,
                        targetAcademySlot,
                        targetPartner.HatchGrade,
                        price,
                        client.Tamer.Inventory.Bits,
                        exp));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigimonTranscendenceSuccessPacketProcessor] :: {ex.Message}");
            }
        }
    }
}