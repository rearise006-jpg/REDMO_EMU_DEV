using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ArchiveAcademyPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ArchiveAcademyList;

        private readonly ILogger _logger;
        private readonly ISender _sender;

        public ArchiveAcademyPacketProcessor(ILogger logger, ISender sender)
        {
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packets = new GamePacketReader(packetData);

            try
            {
                var growthDigimons = client.Tamer.DigimonArchive.DigimonGrowths;

                if (growthDigimons.Any())
                {
                    foreach (var digi in growthDigimons)
                    {
                        if (digi.EndDate < DateTime.Now)
                        {
                            await _sender.Send(new DeleteCharacterDigimonGrowthCommand((int)digi.DigimonId));
                        }
                    }
                }

                if (growthDigimons.Count > 0)
                {
                    client.Send(new ArchiveAcademyIniciarPacket(growthDigimons));
                }
                else
                {
                    client.Send(new ArchiveAcademyIniciarPacket());
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ArchiveAcademyPacketProcessor] :: {ex.Message}");
            }
        }
    }
}

