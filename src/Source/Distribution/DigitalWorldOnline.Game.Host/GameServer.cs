using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Text;
using System.Text.Json;

namespace DigitalWorldOnline.Game
{
    public sealed class GameServer : Commons.Entities.GameServer, IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IConfiguration _configuration;
        private readonly IProcessor _processor;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;
        private readonly ISender _sender;
        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly PvpServer _pvpServer;
        private readonly DungeonsServer _dungeonsServer;
        private readonly EventServer _eventServer;
        private readonly PartyManager _partyManager;

        private const int OnConnectEventHandshakeHandler = 65535;

        public GameServer(
            IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration,
            IProcessor processor,
            ILogger logger,
            IMapper mapper,
            ISender sender,
            AssetsLoader assets,
            MapServer mapServer,
            PvpServer pvpServer,
            DungeonsServer dungeonsServer,
            EventServer eventServer,
            PartyManager partyManager)
        {
            OnConnect += OnConnectEvent;
            OnDisconnect += OnDisconnectEvent;
            DataReceived += OnDataReceivedEvent;

            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            _processor = processor;
            _logger = logger;
            _mapper = mapper;
            _sender = sender;
            _assets = assets.Load();
            _mapServer = mapServer;
            _pvpServer = pvpServer;
            _dungeonsServer = dungeonsServer;
            _eventServer = eventServer;
            _partyManager = partyManager;
        }

        /// <summary>
        /// Event triggered everytime that a game client connects to the server.
        /// </summary>
        /// <param name="sender">The object itself</param>
        /// <param name="gameClientEvent">Game client who connected</param>
        private void OnConnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            try
            {
                var clientIpAddress = gameClientEvent.Client.ClientAddress.Split(':')?.FirstOrDefault();

                try
                {
                    gameClientEvent.Client.SetHandshake((short)(DateTimeOffset.Now.ToUnixTimeSeconds() & OnConnectEventHandshakeHandler));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error setting handshake for {gameClientEvent.Client.ClientAddress}.");
                }

                try
                {
                    if (gameClientEvent.Client.IsConnected)
                    {
                        gameClientEvent.Client.Send(new OnConnectEventConnectionPacket(gameClientEvent.Client.Handshake));
                    }
                    else
                    {
                        _logger.Warning($"Request source {gameClientEvent.Client.ClientAddress} has been disconnected.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error sending connection packet to {gameClientEvent.Client.ClientAddress}.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Unexpected error in OnConnectEvent for Client {gameClientEvent.Client.ClientAddress}.");
            }
        }

        /// <summary>
        /// Event triggered everytime the game client disconnects from the server.
        /// </summary>
        /// <param name="sender">The object itself</param>
        /// <param name="gameClientEvent">Game client who disconnected</param>
        private async void OnDisconnectEvent(object sender, GameClientEvent gameClientEvent)
        {
            try
            {
                if (gameClientEvent.Client.TamerId > 0)
                {
                    _logger.Information(
                        $"Received disconnection event for {gameClientEvent.Client.Tamer.Name} {gameClientEvent.Client.TamerId} {gameClientEvent.Client.HiddenAddress}.");

                    if (gameClientEvent.Client.DungeonMap)
                    {
                        _logger.Information($"Removing the tamer {gameClientEvent.Client.Tamer.Name} . {gameClientEvent.Client.HiddenAddress}.");
                        _dungeonsServer.RemoveClient(gameClientEvent.Client);
                    }
                    else if (gameClientEvent.Client.EventMap)
                    {
                        _logger.Information($"Removing the tamer {gameClientEvent.Client.Tamer.Name} . {gameClientEvent.Client.HiddenAddress}.");
                        _eventServer.RemoveClient(gameClientEvent.Client);
                    }
                    else if (gameClientEvent.Client.PvpMap)
                    {
                        _logger.Information($"Removing the tamer {gameClientEvent.Client.Tamer.Name} . {gameClientEvent.Client.HiddenAddress}.");
                        _pvpServer.RemoveClient(gameClientEvent.Client);
                    }
                    else
                    {
                        _logger.Information($"Removing the tamer {gameClientEvent.Client.Tamer.Name} {gameClientEvent.Client.TamerId}. {gameClientEvent.Client.HiddenAddress}.");
                        _mapServer.RemoveClient(gameClientEvent.Client);
                    }

                    if (gameClientEvent.Client.GameQuit)
                    {
                        gameClientEvent.Client.Tamer.UpdateState(CharacterStateEnum.Disconnected);
                        _logger.Information($"Updating character {gameClientEvent.Client.Tamer.Name} {gameClientEvent.Client.TamerId} state upon disconnect...");

                        await _sender
                            .Send(new UpdateCharacterStateCommand(gameClientEvent.Client.TamerId, CharacterStateEnum.Disconnected))
                            .ConfigureAwait(false);

                        CharacterFriendsNotification(gameClientEvent);
                        CharacterGuildNotification(gameClientEvent);
                        await PartyNotification(gameClientEvent).ConfigureAwait(false);
                        CharacterTargetTraderNotification(gameClientEvent);

                        if (gameClientEvent.Client.DungeonMap)
                        {
                            await DungeonWarpGate(gameClientEvent).ConfigureAwait(false);
                        }

                        _sender.Send(new UpdateAccountStatusCommand(gameClientEvent.Client.AccountId, 0));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    "OnDisconnectEvent fault for tamerId {TamerId} addr {Addr}",
                    gameClientEvent?.Client?.TamerId,
                    gameClientEvent?.Client?.HiddenAddress);
            }
        }


        private async Task PartyNotification(GameClientEvent gameClientEvent)
        {
            var party = _partyManager.FindParty(gameClientEvent.Client.TamerId);

            if (party != null)
            {
                var member = party.Members.FirstOrDefault(x => x.Value.Id == gameClientEvent.Client.TamerId);

                foreach (var target in party.Members.Values)
                {
                    var targetClient = _mapServer.FindClientByTamerId(target.Id);

                    if (targetClient == null) targetClient = _dungeonsServer.FindClientByTamerId(target.Id);

                    if (targetClient == null) continue;

                    targetClient.Send(new PartyMemberDisconnectedPacket(party[gameClientEvent.Client.TamerId].Key)
                        .Serialize());
                }

                if (member.Key == party.LeaderId && party.Members.Count >= 3)
                {
                    party.RemoveMember(party[gameClientEvent.Client.TamerId].Key);

                    var randomIndex = Random.Shared.Next(party.Members.Count);
                    var sortedPlayer = party.Members.ElementAt(randomIndex).Key;

                    foreach (var target in party.Members.Values)
                    {
                        var targetClient = _mapServer.FindClientByTamerId(target.Id);

                        if (targetClient == null) targetClient = _dungeonsServer.FindClientByTamerId(target.Id);

                        if (targetClient == null) continue;

                        targetClient.Send(new PartyLeaderChangedPacket(sortedPlayer).Serialize());
                    }
                }
                else
                {
                    if (party.Members.Count == 2)
                    {
                        var map = UtilitiesFunctions.MapGroup(gameClientEvent.Client.Tamer.Location.MapId);

                        var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(map));
                        var waypoints = await _sender.Send(new MapRegionListAssetsByMapIdQuery(map));

                        if (mapConfig == null || waypoints == null || !waypoints.Regions.Any())
                        {
                            // gameClientEvent.Client.Send(new SystemMessagePacket($"Map information not found for map Id {map}."));
                            //_logger.Warning( $"Map information not found for map Id {map} on character {gameClientEvent.Client.TamerId}.");
                            _partyManager.RemoveParty(party.Id);
                            return;
                        }

                        var destination = waypoints.Regions.First();

                        foreach (var pmember in party.Members.Values.Where(x => x.Id != gameClientEvent.Client.Tamer.Id)
                                     .ToList())
                        {
                            var dungeonClient = _dungeonsServer.FindClientByTamerId(pmember.Id);

                            if (dungeonClient == null) continue;

                            if (dungeonClient.DungeonMap)
                            {
                                _dungeonsServer.RemoveClient(dungeonClient);

                                dungeonClient.Tamer.NewLocation(map, destination.X, destination.Y);
                                await _sender.Send(new UpdateCharacterLocationCommand(dungeonClient.Tamer.Location));

                                dungeonClient.Tamer.Partner.NewLocation(map, destination.X, destination.Y);
                                await _sender.Send(
                                    new UpdateDigimonLocationCommand(dungeonClient.Tamer.Partner.Location));

                                dungeonClient.Tamer.UpdateState(CharacterStateEnum.Loading);
                                await _sender.Send(new UpdateCharacterStateCommand(dungeonClient.TamerId,
                                    CharacterStateEnum.Loading));

                                foreach (var memberId in party.GetMembersIdList())
                                {
                                    var targetDungeon = _dungeonsServer.FindClientByTamerId(memberId);
                                    if (targetDungeon != null)
                                        targetDungeon.Send(new PartyMemberWarpGatePacket(party[dungeonClient.TamerId],
                                                gameClientEvent.Client.Tamer)
                                            .Serialize());
                                }

                                dungeonClient?.SetGameQuit(false);

                                dungeonClient?.Send(new MapSwapPacket(_configuration[GamerServerPublic],
                                    _configuration[GameServerPort],
                                    dungeonClient.Tamer.Location.MapId, dungeonClient.Tamer.Location.X,
                                    dungeonClient.Tamer.Location.Y));
                            }
                        }
                    }

                    party.RemoveMember(party[gameClientEvent.Client.TamerId].Key);
                }

                if (party.Members.Count <= 1)
                    _partyManager.RemoveParty(party.Id);
            }
        }

        private void CharacterGuildNotification(GameClientEvent gameClientEvent)
        {
            if (gameClientEvent.Client.Tamer.Guild != null)
            {
                foreach (var guildMember in gameClientEvent.Client.Tamer.Guild.Members)
                {
                    if (guildMember.CharacterInfo == null)
                    {
                        var guildMemberClient = _mapServer.FindClientByTamerId(guildMember.CharacterId);

                        if (guildMemberClient != null)
                        {
                            guildMember.SetCharacterInfo(guildMemberClient.Tamer);
                        }
                        else
                        {
                            guildMember.SetCharacterInfo(_mapper.Map<CharacterModel>(_sender
                                .Send(new CharacterByIdQuery(guildMember.CharacterId)).ConfigureAwait(false).GetAwaiter().GetResult()));

                        }
                    }
                }

                foreach (var guildMember in gameClientEvent.Client.Tamer.Guild.Members)
                {
                    _logger.Debug(
                        $"Sending guild member disconnection packet for character {guildMember.CharacterId}...");

                    _logger.Debug(
                        $"Sending guild information packet for character {gameClientEvent.Client.TamerId}...");

                    _mapServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildMemberDisconnectPacket(gameClientEvent.Client.Tamer.Name).Serialize());

                    _mapServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildInformationPacket(gameClientEvent.Client.Tamer.Guild).Serialize());

                    _dungeonsServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildMemberDisconnectPacket(gameClientEvent.Client.Tamer.Name).Serialize());

                    _dungeonsServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildInformationPacket(gameClientEvent.Client.Tamer.Guild).Serialize());

                    _eventServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildMemberDisconnectPacket(gameClientEvent.Client.Tamer.Name).Serialize());

                    _eventServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildInformationPacket(gameClientEvent.Client.Tamer.Guild).Serialize());

                    _pvpServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildMemberDisconnectPacket(gameClientEvent.Client.Tamer.Name).Serialize());

                    _pvpServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                        new GuildInformationPacket(gameClientEvent.Client.Tamer.Guild).Serialize());
                }
            }
        }

        private async void CharacterFriendsNotification(GameClientEvent gameClientEvent)
        {
            try
            {
                var client = gameClientEvent?.Client;
                var tamer = client?.Tamer;
                if (tamer == null)
                {
                    _logger.Warning("CharacterFriendsNotification called with null Tamer (addr: {Addr})",
                        client?.HiddenAddress);
                    return;
                }

                var name = tamer.Name;
                var friended = tamer.Friended ?? new List<CharacterFriendModel>(); // tipagem correta
                var payload = new FriendDisconnectPacket(name).Serialize();

                foreach (var friend in friended)
                {
                    var friendId = friend?.FriendId ?? 0;
                    if (friendId <= 0) continue;

                    try { _mapServer.BroadcastForUniqueTamer(friendId, payload); }
                    catch (Exception ex)
                    { _logger.Error(ex, "FriendDisconnect map broadcast failed: {Name}->{FriendId}", name, friendId); }

                    try { _dungeonsServer.BroadcastForUniqueTamer(friendId, payload); }
                    catch (Exception ex)
                    { _logger.Error(ex, "FriendDisconnect dungeons broadcast failed: {Name}->{FriendId}", name, friendId); }

                    try { _eventServer.BroadcastForUniqueTamer(friendId, payload); }
                    catch (Exception ex)
                    { _logger.Error(ex, "FriendDisconnect event broadcast failed: {Name}->{FriendId}", name, friendId); }

                    try { _pvpServer.BroadcastForUniqueTamer(friendId, payload); }
                    catch (Exception ex)
                    { _logger.Error(ex, "FriendDisconnect pvp broadcast failed: {Name}->{FriendId}", name, friendId); }
                }

                await _sender.Send(new UpdateCharacterFriendsCommand(tamer, false)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "CharacterFriendsNotification fault for tamerId {TamerId} addr {Addr}",
                    gameClientEvent?.Client?.TamerId,
                    gameClientEvent?.Client?.HiddenAddress);
            }
        }



        private void CharacterTargetTraderNotification(GameClientEvent gameClientEvent)
        {
            if (gameClientEvent.Client.Tamer.TargetTradeGeneralHandle != 0)
            {
                if (gameClientEvent.Client.DungeonMap)
                {
                    var targetClient =
                        _dungeonsServer.FindClientByTamerHandle(gameClientEvent.Client.Tamer.TargetTradeGeneralHandle);

                    if (targetClient != null)
                    {
                        targetClient.Send(new TradeCancelPacket(gameClientEvent.Client.Tamer.GeneralHandler));
                        targetClient.Tamer.ClearTrade();
                    }
                }
                else
                {
                    var targetClient = _mapServer.FindClientByTamerHandleAndChannel(
                        gameClientEvent.Client.Tamer.TargetTradeGeneralHandle, gameClientEvent.Client.TamerId);

                    if (targetClient != null)
                    {
                        targetClient.Send(new TradeCancelPacket(gameClientEvent.Client.Tamer.GeneralHandler));
                        targetClient.Tamer.ClearTrade();
                    }
                }
            }
        }

        private async Task DungeonWarpGate(GameClientEvent gameClientEvent)
        {
            if (gameClientEvent.Client.DungeonMap)
            {
                var map = UtilitiesFunctions.MapGroup(gameClientEvent.Client.Tamer.Location.MapId);

                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(map));
                var waypoints = await _sender.Send(new MapRegionListAssetsByMapIdQuery(map));

                if (mapConfig == null || waypoints == null || !waypoints.Regions.Any())
                {
                    _logger.Warning($"Map information not found for map Id {map} on character {gameClientEvent.Client.TamerId} Dungeon Portal");
                    return;
                }

                var destination = waypoints.Regions.First();

                gameClientEvent.Client.Tamer.NewLocation(map, destination.X, destination.Y);
                await _sender.Send(new UpdateCharacterLocationCommand(gameClientEvent.Client.Tamer.Location));

                gameClientEvent.Client.Tamer.Partner.NewLocation(map, destination.X, destination.Y);
                await _sender.Send(new UpdateDigimonLocationCommand(gameClientEvent.Client.Tamer.Partner.Location));

                gameClientEvent.Client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(gameClientEvent.Client.TamerId, CharacterStateEnum.Loading));
            }
        }

        /// <summary>
        /// Event triggered everytime the game client sends a TCP packet.
        /// </summary>
        /// <param name="sender">The object itself</param>
        /// <param name="gameClientEvent">Game client who sent the packet</param>
        /// <param name="data">The packet content, in byte array</param>
        private void OnDataReceivedEvent(object sender, GameClientEvent gameClientEvent, byte[] data)
        {
            try
            {
                _logger.Debug($"Received {data.Length} bytes from {gameClientEvent.Client.ClientAddress}.");
                _processor.ProcessPacketAsync(gameClientEvent.Client, data);
            }
            catch (NotImplementedException)
            {
                gameClientEvent.Client.Send(new SystemMessagePacket($"Feature under development."));
            }
            catch (Exception ex)
            {
                gameClientEvent.Client.SetGameQuit(true);
                gameClientEvent.Client.Disconnect();

                _logger.Error($"Process packet error: {ex.Message} {ex.InnerException} {ex.StackTrace}.");

                try
                {
                    var filePath = $"PacketErrors/{gameClientEvent.Client.ClientAddress}_{DateTime.Now}.txt";

                    using var fs = File.Create(filePath);
                    fs.Write(data, 0, data.Length);
                }
                catch
                {
                }

                //TODO: Salvar no banco com os parametros
            }
        }

        /// <summary>
        /// The default hosted service "starting" method.
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _hostApplicationLifetime.ApplicationStarted.Register(OnStarted);
            _hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
            _hostApplicationLifetime.ApplicationStopped.Register(OnStopped);

            // 1. Wait for the assets to load
            var waitSw = System.Diagnostics.Stopwatch.StartNew();
            while (_assets.Loading)
            {
                await Task.Delay(2000, cancellationToken);
                if (waitSw.Elapsed > TimeSpan.FromSeconds(30))
                {
                    _logger.Warning("Assets still loading after {sec}s...", (int)waitSw.Elapsed.TotalSeconds);
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Warning("GameServer startup cancelled while waiting for assets.");
                    return;
                }
            }

            // Inicia as operações de background do MapServer e DungeonsServer
            _ = Task.Run(() => _mapServer.StartAsync(cancellationToken))
         .ContinueWith(t => _logger.Error(t.Exception, "MapServer.StartAsync faulted"),
                       TaskContinuationOptions.OnlyOnFaulted);

            _ = Task.Run(() => _dungeonsServer.StartAsync(cancellationToken))
        .ContinueWith(t => _logger.Error(t.Exception, "DungeonsServer.StartAsync faulted"),
                      TaskContinuationOptions.OnlyOnFaulted);

            _ = Task.Run(CheckAllDigimonEvolutions)
         .ContinueWith(t => _logger.Error(t.Exception, "CheckAllDigimonEvolutions faulted"),
                       TaskContinuationOptions.OnlyOnFaulted);

            _ = _sender.Send(new UpdateCharacterFriendsCommand(null, false))
        .ContinueWith(t => _logger.Error(t.Exception, "UpdateCharacterFriendsCommand faulted"),
                       TaskContinuationOptions.OnlyOnFaulted);

        }

        /// <summary>
        /// The default hosted service "stopping" method
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// The default hosted service "started" method action
        /// </summary>
        private void OnStarted()
        {
            LogTitleMessage(ConsoleColor.Magenta, $"SERVIDOR ONLINE: [{_configuration[GameServerAddress]}]");

            if (!Listen(_configuration[GameServerAddress], _configuration[GameServerPort], _configuration[GameServerBacklog]))
            {
                LogTitleMessage(ConsoleColor.Magenta, $"SERVIDOR OFFLINE: [{_configuration[GameServerAddress]}]");

                _hostApplicationLifetime.StopApplication();
                return;
            }

            LogTitleMessage(ConsoleColor.Magenta, "SERVIDOR DE MUNDODIGITAL ON !!");
            Console.WriteLine();

            _sender.Send(new UpdateCharactersStateCommand(CharacterStateEnum.Disconnected));

        }

        /// <summary>
        /// The default hosted service "stopping" method action
        /// </summary>
        private void OnStopping()
        {
            try
            {
                LogTitleMessage(ConsoleColor.Magenta, "SERVIDOR MUNDODIGITAL OFF");

                Task.Run(async () => await _sender.Send(new UpdateCharacterFriendsCommand(null, false)));

                //_ = _mapServer.CallDiscordWarnings("Server Offline", "fc0303", "1307467492888805476", "1280948869739450438");

                Shutdown();
                return;
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw;
            }
        }

        /// <summary>
        /// The default hosted service "stopped" method action
        /// </summary>
        private void OnStopped()
        {
            LogTitleMessage(ConsoleColor.Magenta, "SERVIDOR DE MUNDODIGITAL DESLIGADO");
        }

        // ------------------------------------------------------------------------

        private async Task CheckAllDigimonEvolutions()
        {
            List<DigimonModel> digimons = _mapper.Map<List<DigimonModel>>(await _sender.Send(new GetAllCharactersDigimonQuery()));

            int digimonEvolutionsAddedCount = 0;
            int digimonEvolutionsRemovedCount = 0;

            int encyclopediaEntriesAddedCount = 0;
            int encyclopediaEvolutionsAddedCount = 0;
            int encyclopediaEvolutionsRemovedCount = 0;

            var tasks = digimons.Select(async digimon =>
            {
                try
                {
                    var digimonEvolutionInfo = _mapper.Map<EvolutionAssetModel>(await _sender.Send(new DigimonEvolutionAssetsByTypeQuery(digimon.BaseType)));

                    if (digimonEvolutionInfo == null)
                    {
                        _logger.Warning($"EvolutionInfo is null for digimon {digimon.BaseType}. Skipping.");
                        return;
                    }

                    // --- 1. Sincroniza as evoluções do Digimon do jogador

                    var digimonEvolutionResults = await SynchronizePlayerDigimonEvolutions(digimon, digimonEvolutionInfo);
                    Interlocked.Add(ref digimonEvolutionsAddedCount, digimonEvolutionResults.Added);
                    Interlocked.Add(ref digimonEvolutionsRemovedCount, digimonEvolutionResults.Removed);

                    if (digimon.Character?.Encyclopedia == null)
                    {
                        _logger.Warning($"Tamer {digimon.Character?.Id} for Digimon {digimon.BaseType} has no encyclopedia. Skipping encyclopedia sync.");
                        return;
                    }

                    // --- 2. Sincroniza a enciclopédia do jogador
                    var encyclopediaResults = await SynchronizePlayerEncyclopedia(digimon, digimonEvolutionInfo);
                    Interlocked.Add(ref encyclopediaEntriesAddedCount, encyclopediaResults.EntriesAdded);
                    Interlocked.Add(ref encyclopediaEvolutionsAddedCount, encyclopediaResults.EvolutionsAdded);
                    Interlocked.Add(ref encyclopediaEvolutionsRemovedCount, encyclopediaResults.EvolutionsRemoved);
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Error processing digimon evolutions for {digimon?.BaseType}.");
                }
            });

            await Task.WhenAll(tasks);

            //_logger.Information($"Finished checking all Digimon evolutions. " +
            //    $"Digimon evolutions added to player's Digimon: {digimonEvolutionsAddedCount}, " +
            //    $"Digimon evolutions removed from player's Digimon: {digimonEvolutionsRemovedCount}");

            //_logger.Information($"Finished checking all Digimon evolutions. " +
            //    $"New Encyclopedia entries added: {encyclopediaEntriesAddedCount}, " +
            //    $"New Encyclopedia Evolutions added: {encyclopediaEvolutionsAddedCount}, " +
            //    $"Encyclopedia Evolutions removed: {encyclopediaEvolutionsRemovedCount}");
        }

        // ### Função 1: Sincronizar Evoluções do Digimon do Jogador
        private async Task<(int Added, int Removed)> SynchronizePlayerDigimonEvolutions(DigimonModel digimon, EvolutionAssetModel digimonEvolutionInfo)
        {
            int addedCount = 0;
            int removedCount = 0;
            bool changed = false;

            // --- Adiciona novas evoluções ao Digimon do jogador
            foreach (var evolutionLine in digimonEvolutionInfo.Lines)
            {
                if (!digimon.Evolutions.Exists(x => x.Type == evolutionLine.Type))
                {
                    digimon.Evolutions.Add(new DigimonEvolutionModel(evolutionLine.Type));
                    addedCount++;
                    changed = true;

                    _logger.Information($"Added new evolution {evolutionLine.Type} to player's Digimon {digimon.Id} {digimon.BaseType}.");
                }
            }

            // --- Remove evoluções do Digimon do jogador que não existem mais no jogo
            var evolutionsToRemove = digimon.Evolutions.Where(playerEvo => !digimonEvolutionInfo.Lines.Any(gameEvo => gameEvo.Type == playerEvo.Type)).ToList();

            foreach (var evoToRemove in evolutionsToRemove)
            {
                digimon.Evolutions.Remove(evoToRemove);
                removedCount++;
                changed = true;

                _logger.Information($"Removed non-existent evolution '{evoToRemove.Type}' from player's Digimon {digimon.Id} ({digimon.BaseType}).");
            }

            if (changed)
            {
                await _sender.Send(new UpdateDigimonEvolutionsCommand(digimon.Id, digimon.Evolutions));
            }

            return (addedCount, removedCount);
        }

        // ### Função 2: Sincronizar Encyclopedia do Digimon do Jogador
        private async Task<(int EntriesAdded, int EvolutionsAdded, int EvolutionsRemoved)> SynchronizePlayerEncyclopedia(DigimonModel digimon, EvolutionAssetModel digimonEvolutionInfo)
        {
            int entriesAddedCount = 0;
            int evolutionsAddedCount = 0;
            int evolutionsRemovedCount = 0;

            bool encyclopediaNeedsUpdate = false;

            var characterEncyclopedia = digimon.Character.Encyclopedia.FirstOrDefault(x => x.DigimonEvolutionId == digimonEvolutionInfo.Id);

            // --- Adicionar nova entrada na enciclopédia se não existir
            if (characterEncyclopedia == null)
            {
                entriesAddedCount++;

                characterEncyclopedia = CharacterEncyclopediaModel.Create(digimon.Character.Id, digimonEvolutionInfo.Id,
                    digimon.Level, digimon.Size, digimon.Digiclone.ATLevel, digimon.Digiclone.BLLevel, digimon.Digiclone.CTLevel, digimon.Digiclone.EVLevel,
                    digimon.Digiclone.HPLevel, false, false);

                // Adiciona todas as evoluções do jogo a esta nova entrada de enciclopédia
                foreach (var evolutionLine in digimonEvolutionInfo.Lines)
                {
                    evolutionsAddedCount++;

                    var encyclopediaEvo = CharacterEncyclopediaEvolutionsModel.Create(evolutionLine.Type, evolutionLine.SlotLevel,
                        digimon.Evolutions.Exists(x => x.Type == evolutionLine.Type && Convert.ToBoolean(x.Unlocked)));

                    characterEncyclopedia.Evolutions.Add(encyclopediaEvo);
                }

                var encyclopediaAdded = await _sender.Send(new CreateCharacterEncyclopediaCommand(characterEncyclopedia));

                digimon.Character.Encyclopedia.Add(encyclopediaAdded);

                //_logger.Information($"Added new encyclopedia entry for CharacterId: {digimon.Character.Id}, DigimonBaseType: {digimon.BaseType}");
            }
            else
            {
                // --- Adiciona novas evoluções a uma entrada de enciclopédia existente
                var evolutionsToAdd = digimonEvolutionInfo.Lines
                    .Where(gameEvolution => !characterEncyclopedia.Evolutions.Any(encEvolution => encEvolution.DigimonBaseType == gameEvolution.Type)).ToList();

                foreach (var evolutionLine in evolutionsToAdd)
                {
                    evolutionsAddedCount++;

                    var encyclopediaEvo = CharacterEncyclopediaEvolutionsModel.Create(evolutionLine.Type, evolutionLine.SlotLevel, digimon.Evolutions
                        .Exists(x => x.Type == evolutionLine.Type && Convert.ToBoolean(x.Unlocked)));

                    characterEncyclopedia.Evolutions.Add(encyclopediaEvo);

                    encyclopediaNeedsUpdate = true;

                    //_logger.Information($"Added new evolution '{evolutionLine.Type}' to encyclopedia for CharacterId: {digimon.Character.Id}, DigimonBaseType: {digimon.BaseType}.");
                }

                // --- Remove evoluções que não existem mais nos dados do jogo da enciclopédia ---
                var evolutionsToRemove = characterEncyclopedia.Evolutions
                    .Where(encEvolution => !digimonEvolutionInfo.Lines.Any(gameEvolution => gameEvolution.Type == encEvolution.DigimonBaseType)).ToList();

                foreach (var evoToRemove in evolutionsToRemove)
                {
                    evolutionsRemovedCount++;

                    characterEncyclopedia.Evolutions.Remove(evoToRemove);

                    encyclopediaNeedsUpdate = true;

                    //_logger.Information($"Removed non-existent evolution '{evoToRemove.DigimonBaseType}' from encyclopedia for CharacterId: {digimon.Character.Id}, DigimonBaseType: {digimon.BaseType}.");
                }

                // Se alguma alteração foi feita (adições ou remoções), atualiza a enciclopédia
                if (encyclopediaNeedsUpdate)
                {
                    // Reavalia RewardAllowed e RewardReceived após as alterações
                    characterEncyclopedia.SetRewardAllowed(characterEncyclopedia.Evolutions.All(x => x.IsUnlocked));

                    // Só define rewardReceived como false se rewardAllowed e ainda não recebido
                    if (characterEncyclopedia.IsRewardAllowed && !characterEncyclopedia.IsRewardReceived)
                    {
                        characterEncyclopedia.SetRewardReceived(false);
                    }

                    await _sender.Send(new UpdateCharacterEncyclopediaCommand(characterEncyclopedia));
                    //_logger.Information($"Updated encyclopedia for CharacterId: {digimon.Character.Id}, DigimonBaseType: {digimon.BaseType} due to evolution changes.");
                }
            }

            return (entriesAddedCount, evolutionsAddedCount, evolutionsRemovedCount);
        }

        // ------------------------------------------------------------------------
        private void LogMessage(ConsoleColor color, string message)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.WriteLine("|----------------------------------------------------|");
            Console.WriteLine("|                                                    |");
            Console.WriteLine("|        ████   ████   ██████                        |");
            Console.WriteLine("|        ██ ██ ██ ██   ██   ██                       |");
            Console.WriteLine("|        ██  ███  ██   ██   ██                       |");
            Console.WriteLine("|        ██       ██   ██   ██                       |");
            Console.WriteLine("|        ██       ██   ██████                        |");
            Console.WriteLine("|                                                    |");
            Console.WriteLine("|----------------------------------------------------|");
            // Exibe a história (centralizada dentro de 52 caracteres)
            PrintCenteredLine("Um novo desafio se aproxima...");
            PrintCenteredLine("As trevas emergem das profundezas digitais.");
            PrintCenteredLine("Somente os mais fortes sobreviverão.");
            PrintCenteredLine("A jornada começa agora.");
            Console.WriteLine("|                                                    |");
            Console.WriteLine("|----------------------------------------------------|");

            // Mensagem personalizada
            PrintCenteredLine(message.ToUpper());

            Console.WriteLine("|                                                    |");

            // Assinatura final
            PrintCenteredLine("MD");

            Console.WriteLine("|----------------------------------------------------|");
            Console.ResetColor();
        }

        // Função auxiliar para centralizar texto dentro da borda
        private void PrintCenteredLine(string text)
        {
            int totalWidth = 52;
            int padding = (totalWidth - text.Length) / 2;
            string line = "|" + new string(' ', padding) + text + new string(' ', totalWidth - text.Length - padding) + "|";
            Console.WriteLine(line);
        }
        // ------------------------------------------------------------------------

        private void LogTitleMessage(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"|----------------------------------------------------|");
            Console.WriteLine($"|---------  {message.ToUpper()}");
            Console.WriteLine($"|----------------------------------------------------|");
            Console.ResetColor();
        }
    }
}