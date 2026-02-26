using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.GameHost;
using Microsoft.Extensions.Configuration;
using Serilog;
using MediatR;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DieConfirmPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DieConfirm;

        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;

        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        private readonly IConfiguration _configuration;
        
        public DieConfirmPacketProcessor(MapServer mapServer, DungeonsServer dungeonsServer,
            ISender sender, IMapper mapper, ILogger logger, IConfiguration configuration)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _sender = sender;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            try
            {
                if (client.DungeonMap)
                {
                    var map = UtilitiesFunctions.MapGroup(client.Tamer.Location.MapId);

                    var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(map));
                    var region = MapRegionAssetManager.Instance.GetByMapId(map);

                    if (mapConfig == null || region == null || !region.Any())
                    {
                        _logger.Error($"[DieConfirm] :: MapRegion XML not found or empty for mapId {map}.");
                        return;
                    }

                    var destiny = region.First(); // coordenada de respawn
                    int oldMapId = client.Tamer.Location.MapId;

                    client.Tamer.Die();

                    await _sender.Send(new UpdateCharacterBasicInfoCommand(client.Tamer));
                    await _sender.Send(new UpdateCharacterActiveEvolutionCommand(client.Tamer.ActiveEvolution));

                    client.Tamer.NewLocation(map, destiny.CenterX, destiny.CenterY);
                    await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                    client.Tamer.Partner.NewLocation(map, destiny.CenterX, destiny.CenterY);
                    await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));


                    client.Tamer.UpdateState(CharacterStateEnum.Loading);
                    await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                    client.Send(new MapSwapPacket(_configuration[GamerServerPublic], _configuration[GameServerPort],
                        client.Tamer.Location.MapId, client.Tamer.Location.X, client.Tamer.Location.Y).Serialize());

                    client.SetGameQuit(false);
                    
                    if (client.Tamer.Location.MapId != oldMapId)
                    {
                        await Task.Delay(1000);

                        _dungeonServer.RemoveClient(client);
                    }
                }
                else
                {
                    var region = MapRegionAssetManager.Instance.GetByMapId(client.Tamer.Location.MapId);

                    if (region == null || !region.Any())
                    {
                        _logger.Error($"[DieConfirm] :: MapRegion XML not found or empty for mapId {client.Tamer.Location.MapId}.");
                        return;
                    }

                    var destiny = region.First(); // punto de respawn elegido
                    int oldMapId = client.Tamer.Location.MapId;

                    client.Tamer.NewLocation(destiny.CenterX, destiny.CenterY);
                    await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                    client.Tamer.Partner.NewLocation(destiny.CenterX, destiny.CenterY);
                    await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                    client.Tamer.UpdateState(CharacterStateEnum.Loading);
                    await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                    client.Send(new MapSwapPacket(_configuration[GamerServerPublic], _configuration[GameServerPort],
                        client.Tamer.Location.MapId, client.Tamer.Location.X, client.Tamer.Location.Y).Serialize());

                    client.SetGameQuit(false);
                    
                    if (client.Tamer.Location.MapId != oldMapId)
                    {
                        await Task.Delay(1000);

                        _mapServer.RemoveClient(client);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[DieConfirm] :: {ex.Message}");
            }
        }
    }
}