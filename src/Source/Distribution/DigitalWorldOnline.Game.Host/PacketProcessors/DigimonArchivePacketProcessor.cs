using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Game.Managers;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigimonArchivePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigimonArchive;

        private readonly StatusManager _statusManager;

        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public DigimonArchivePacketProcessor(StatusManager statusManager, IMapper mapper, ILogger logger, ISender sender)
        {
            _statusManager = statusManager;
            _mapper = mapper;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var isVip = packet.ReadByte();
            var nInvenIdx = packet.ReadInt();
            var nNpcId = packet.ReadInt();
            var nNpcType = packet.ReadByte();

            try
            {
                var growthDigimonsDto = await _sender.Send(new CharacterDigimonsGrowthByIdQuery(client.Tamer.Id));
                var growthDigimons = _mapper.Map<List<CharacterDigimonGrowthSystemModel>>(growthDigimonsDto);

                if (growthDigimons.Any())
                {
                    foreach (var digi in growthDigimons)
                    {
                        if (digi.EndDate < DateTime.Now)
                            await _sender.Send(new DeleteCharacterDigimonGrowthCommand((int)digi.DigimonId));
                    }
                }

                var digimonIds = client.Tamer.DigimonArchive.DigimonArchives.Where(x => x.DigimonId > 0).Select(x => x.DigimonId).ToList();

                if (digimonIds.Count > 0)
                {
                    var digimons = await _sender.Send(new GetDigimonsByIdsQuery(digimonIds));
                    var digimonDictionary = digimons.ToDictionary(d => d.Id);

                    foreach (var digimonArchive in client.Tamer.DigimonArchive.DigimonArchives)
                    {
                        if (digimonDictionary.TryGetValue(digimonArchive.DigimonId, out var digimon))
                        {
                            var mappedDigimon = _mapper.Map<DigimonModel>(digimon);

                            digimonArchive.SetDigimonInfo(mappedDigimon);

                            mappedDigimon.SetBaseInfo(_statusManager.GetDigimonBaseInfo(mappedDigimon.BaseType));
                            mappedDigimon.SetBaseStatus(_statusManager.GetDigimonBaseStatus(mappedDigimon.BaseType, mappedDigimon.Level, mappedDigimon.Size));
                        }
                    }
                }

                client.Send(new DigimonArchiveLoadPacket(client.Tamer.DigimonArchive, growthDigimons));
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigimonArchive] :: {ex.Message}");
            }
        }
    }
}