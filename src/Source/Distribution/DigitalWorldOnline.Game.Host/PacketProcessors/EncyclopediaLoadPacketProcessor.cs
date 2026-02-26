using AutoMapper;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class EncyclopediaLoadPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.EncyclopediaLoad;

        private readonly ISender _sender;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        public EncyclopediaLoadPacketProcessor(ISender sender,ILogger logger, IMapper mapper)
        {
            _sender = sender;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            try
            {
                List<DigimonModel> allDigimons = new List<DigimonModel>();

                allDigimons.AddRange(client.Tamer.Digimons);

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

                            allDigimons.Add(mappedDigimon);
                        }
                    }
                }

                var bestDigimons = allDigimons.GroupBy(d => d.BaseType).Select(group => group.OrderByDescending(d =>
                d.Digiclone.ATLevel + d.Digiclone.CTLevel + d.Digiclone.BLLevel + d.Digiclone.EVLevel + d.Digiclone.HPLevel)
                .ThenByDescending(d => d.Size).ThenByDescending(d => d.Level).First()).ToList();

                client.Send(new EncyclopediaLoadPacket(bestDigimons));
            }
            catch (Exception ex)
            {
                _logger.Error($"[EncyclopediaLoad] :: {ex.Message}");
            }
        }
    }
}