using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class WarpGateDungeonPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.WarpGateDungeon;

        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly PartyManager _partyManager;
        private readonly IConfiguration _configuration;
        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ISender _sender;
        private readonly ILogger _logger;

        public WarpGateDungeonPacketProcessor(
            PartyManager partyManager,
            IConfiguration configuration,
            AssetsLoader assets,
            MapServer mapServer,
            ISender sender,
            ILogger logger,
            DungeonsServer dungeonServer)
        {
            _partyManager = partyManager;
            _configuration = configuration;
            _assets = assets;
            _mapServer = mapServer;
            _sender = sender;
            _logger = logger;
            _dungeonServer = dungeonServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            var portalId = packet.ReadInt();
            var portal = _assets.Portal.FirstOrDefault(x => x.Id == portalId);

            if (portal == null)
            {
                client.Send(new SystemMessagePacket($"Portal {portalId} not found."));
                var mapId = client.Tamer.Location.MapId;
                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(mapId));
                var waypoints = await _sender.Send(new MapRegionListAssetsByMapIdQuery(mapId));

                if (mapConfig == null || waypoints == null || !waypoints.Regions.Any())
                {
                    client.Send(new SystemMessagePacket($"Map information not found for {mapId}"));
                    return;
                }

                if (client.DungeonMap)
                    _dungeonServer.RemoveClient(client);
                else
                    _mapServer.RemoveClient(client);

                var destination = waypoints.Regions.First();

                client.Tamer.NewLocation(mapId, destination.X, destination.Y);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
                client.Tamer.Partner.NewLocation(mapId, destination.X, destination.Y);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));
                client.Tamer.UpdateState(CharacterStateEnum.Loading);

                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                client.Send(
                    new MapSwapPacket(
                        _configuration[GamerServerPublic],
                        _configuration[GameServerPort],
                        mapId,
                        destination.X,
                        destination.Y
                    )
                );
                return;
            }

            // Handle resource requirements if any
            var portalRequestInfo = _assets.Npcs.FirstOrDefault(x => x.NpcId == portal.NpcId)?.Portals.ToList();
            if (portalRequestInfo != null)
            {
                var Request = portalRequestInfo.SelectMany(x => x.PortalsAsset).ToList();
                var RemoveInfo = Request[portal.PortalIndex];
                for (int i = 0; i < 3; i++)
                {
                    switch (RemoveInfo.npcPortalsAsset[i].Type)
                    {
                        case NpcResourceTypeEnum.Money:
                            client.Tamer.Inventory.RemoveBits(RemoveInfo.npcPortalsAsset[i].ItemId);
                            await _sender.Send(new UpdateItemListBitsCommand(client.Tamer.Inventory));
                            break;
                        case NpcResourceTypeEnum.Item:
                            var targeItem = client.Tamer.Inventory.FindItemById(RemoveInfo.npcPortalsAsset[i].ItemId);
                            if (targeItem != null)
                            {
                                client.Tamer.Inventory.RemoveOrReduceItem(targeItem, 1);
                                await _sender.Send(new UpdateItemCommand(targeItem));
                            }
                            break;
                    }
                }
            }

            // If portal is local, handle as before
            if (portal.IsLocal)
            {
                client.Tamer.NewLocation(portal.DestinationMapId, portal.DestinationX, portal.DestinationY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
                client.Tamer.Partner.NewLocation(portal.DestinationMapId, portal.DestinationX, portal.DestinationY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));
                client.Send(new LocalMapSwapPacket(client.Tamer.GeneralHandler, client.Tamer.Partner.GeneralHandler,
                    portal.DestinationX, portal.DestinationY, portal.DestinationX, portal.DestinationY));
                return;
            }

            // DUNGEON LOGIC: Party or Solo
            var party = _partyManager.FindParty(client.TamerId);
            SetChannelToZero(client);

           // bool isExit = IsPortalExit(client, portal);

            if (party != null)
            {
                // Party logic
               // {

                        // Entrada al dungeon → validar si ya fue completado
                    //  var completedInstance = _dungeonServer.Maps
                        //   .FirstOrDefault(m => m.DungeonId == party.Id &&
                    //                           m.MapId == portal.DestinationMapId &&
                    //                           m.IsDungeonCompleted);
                    //
                    //    if (completedInstance != null)
                    //    {
                            //Console.WriteLine($"[BLOCK ENTRY] {client.Tamer.Name} tried to re-enter completed dungeon instance {completedInstance.Id}");
                //         client.Send(new SystemMessagePacket("Dungeon already completed. Cannot enter again."));
                //         return;
                //      }

                    await _dungeonServer.SearchNewMaps(true, client);
              //  }
            }
           // else
          //  {
                // Solo player
              //  if (!isExit)
           //         await _dungeonServer.SearchNewMaps(false, client);
           // }


            await MoveClientToDungeon(client, portal);
        }

        private void SetChannelToZero(GameClient client)
        {
            // Adjust this if your property/method is different
            if (client.Tamer != null)
                client.Tamer.SetCurrentChannel(0);
        }

        private async Task MoveClientToDungeon(GameClient client, PortalAssetModel portal)
        {
            if (client.DungeonMap)
                _dungeonServer.RemoveClient(client);
            else
                _mapServer.RemoveClient(client);

            client.Tamer.NewLocation(portal.DestinationMapId, portal.DestinationX, portal.DestinationY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
            client.Tamer.Partner.NewLocation(portal.DestinationMapId, portal.DestinationX, portal.DestinationY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));
            client.Tamer.UpdateState(CharacterStateEnum.Loading);
            await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

            client.SetGameQuit(false);

            client.Send(new MapSwapPacket(
                _configuration[GamerServerPublic],
                _configuration[GameServerPort],
                client.Tamer.Location.MapId,
                client.Tamer.Location.X,
                client.Tamer.Location.Y
            ));

        }


        private bool IsPortalExit(GameClient client, PortalAssetModel portal)
        {
            short currentMap = client.Tamer.Location.MapId;
            int destination = portal.DestinationMapId;

            bool isCurrentDungeon = UtilitiesFunctions.DungeonMapIds.Contains(currentMap);
            bool isDestinationDungeon = UtilitiesFunctions.DungeonMapIds.Contains((short)destination);

            // Si estoy dentro de un dungeon y salgo hacia un mapa NO dungeon → salida
            if (isCurrentDungeon && !isDestinationDungeon)
                return true;

            // Si no estoy dentro de un dungeon → nunca es salida
            return false;
        }

    }
}