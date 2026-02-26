using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigimonSkillMemoryRemovePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.RemoveSkillMemory;

        private readonly StatusManager _statusManager;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;

        public DigimonSkillMemoryRemovePacketProcessor(
            StatusManager statusManager,
            IMapper mapper,
            ILogger logger,
            ISender sender,
            MapServer mapServer,
            DungeonsServer dungeonServer)
        {
            _statusManager = statusManager;
            _mapper = mapper;
            _logger = logger;
            _sender = sender;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            var skillCodeId = packet.ReadInt();
            IMapServer server = client.DungeonMap ? _dungeonServer : _mapServer;

            _logger.Information($"Skill Memory Remove  |{skillCodeId}");
            if (client.Partner.CurrentEvolution.SkillsMemory.Any(x => x.SkillId == skillCodeId && x.DigimonType == client.Partner.CurrentType))
            {
                if (client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffInfo.SkillId == skillCodeId))
                {
                    var buffToRemove = client.Partner.BuffList.ActiveBuffs.FirstOrDefault(x => x.SkillId == skillCodeId);
                    client.Partner.BuffList.Remove(buffToRemove.BuffId);
                    server.BroadcastForTamerViewsAndSelf(client.TamerId, new RemoveBuffPacket(client.Partner.GeneralHandler, buffToRemove.BuffId).Serialize());
                    _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));
                }
                if (client.Tamer.BuffList.ActiveBuffs.Any(x => x.BuffInfo.SkillId == skillCodeId))
                {
                    var buffToRemove = client.Tamer.BuffList.ActiveBuffs.FirstOrDefault(x => x.SkillId == skillCodeId);
                    client.Tamer.BuffList.Remove(buffToRemove.BuffId);
                    server.BroadcastForTamerViewsAndSelf(client.TamerId, new RemoveBuffPacket(client.Tamer.GeneralHandler, buffToRemove.BuffId).Serialize());
                    _sender.Send(new UpdateCharacterBuffListCommand(client.Tamer.BuffList));
                }
                client.Send(new DigimonSkillMemoryRemovePacket(skillCodeId));
                _sender.Send(new DeleteDigimonSkillMemoryCommand(skillCodeId, client.Partner.CurrentEvolution.Id));

            }
        }
    }
}
