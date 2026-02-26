using AutoMapper;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost.EventsServer;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Commands;
using DigitalWorldOnline.Commons.Enums;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class MastersMatchInsertPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MastersMatchInsert;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;

        public MastersMatchInsertPacketProcessor(ILogger logger, ISender sender, IMapper mapper, MapServer mapServer, DungeonsServer dungeonServer, EventServer eventServer, PvpServer pvpServer)
        {
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var userID = packet.ReadInt();
            var npcId = packet.ReadInt();
            var itemSlot = packet.ReadInt();
            var itemAmount = packet.ReadInt();
            var nFTNpcIdx = packet.ReadByte();

            _logger.Warning($"userID: {userID} | npcId: {npcId}");
            _logger.Warning($"itemSlot: {itemSlot} | itemAmount: {itemAmount} | nFTNpcIdx: {nFTNpcIdx}");

            var tamerName = client.Tamer.Name;
            long characterId = client.TamerId;

            MastersMatchRankerDTO masterMatchRanker = await _sender.Send(new GetMasterMatchRankerDataQuery(characterId));

            if (masterMatchRanker == null)
            {
                _logger.Error($"[MastersMatchInsert] :: CharacterId {characterId} ({tamerName}) tried to donate but has no Masters Match ranker record. Aborting donation.");
                return;
            }

            var tamerTeam = masterMatchRanker.Team;

            var removeItem = client.Tamer.Inventory.FindItemBySlot(itemSlot);

            if (removeItem != null)
            {
                client.Tamer.Inventory.RemoveOrReduceItem(removeItem, itemAmount);
                
                await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
            }
            else
            {
                _logger.Error($"[MastersMatchInsert] :: Item not found to remove !!");
                return;
            }

            await _sender.Send(new UpdateMastersMatchRankerCommand(characterId, tamerName, tamerTeam, itemAmount));

            client.Send(new MastersMatchInsertPacket(itemSlot, itemAmount));
        }
    }
}