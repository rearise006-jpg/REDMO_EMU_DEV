using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ArchiveAcademyExtractionPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArchiveAcademyExtraction;

        private readonly ISender _sender;
        private readonly ILogger _logger;

        public ArchiveAcademyExtractionPacketProcessor(ISender sender, ILogger logger)
        {
            _sender = sender;
            _logger = logger;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var growthSlot = packet.ReadByte();

            try
            {
                var growthToExtraction = client.Tamer.DigimonArchive.DigimonGrowths.FirstOrDefault(d => d.GrowthSlot == growthSlot);

                if (growthToExtraction != null)
                {
                    client.Tamer.DigimonArchive.DigimonGrowths.Remove(growthToExtraction);

                    _ = _sender.Send(new DeleteCharacterDigimonGrowthCommand((int)growthToExtraction.DigimonId));
                }

                client.Send(new DigimonArchiveGrowthExtractionPacket(growthSlot));
            }
            catch (Exception ex)
            {
                _logger.Error($"[ArchiveAcademyExtraction] :: {ex.Message}");
            }
        }
    }
}

