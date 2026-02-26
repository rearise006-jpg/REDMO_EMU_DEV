using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace DigitalWorldOnline.GameHost
{
    public sealed partial class MapServer
    {
        private DateTime _lastMapsSearch = DateTime.Now;
        private DateTime _lastMobsSearch = DateTime.Now;
        private DateTime _lastConsignedShopsSearch = DateTime.Now;

        //TODO: externalizar
        private readonly int _startToSee = 14000;
        private readonly int _stopSeeing = 14000;

        // --------------------------------------------------------------------------

        /// <summary>
        /// Cleans unused running maps.
        /// </summary>
        public Task CleanMaps()
        {
            var mapsToRemove = new List<GameMap>();

            lock (_mapsLock) // Protege o acesso à coleção Maps
            {
                mapsToRemove.AddRange(Maps.Where(x => x.CloseMap));
            }

            foreach (var map in mapsToRemove)
            {
                lock (_mapsLock) // Protege a remoção individual
                {
                    Maps.Remove(map);
                }
            }

            return Task.CompletedTask;
        }

        // --------------------------------------------------------------------------

        /// <summary>
        /// Search for new maps to instance and load mobs.
        /// </summary>
        public async Task SearchNewMaps(CancellationToken cancellationToken, GameClient client)
        {
            if (DateTime.Now > _lastMapsSearch)
            {
                var mapsToLoad =
                    _mapper.Map<List<GameMap>>(await _sender.Send(new GameMapsConfigQuery(MapTypeEnum.Default), cancellationToken));

                foreach (var newMap in mapsToLoad)
                {
                    // 🆕 MapIsOpen kontrolü ekle
                    if (!newMap.MapIsOpen)
                    {
                        _logger.Information($"[SearchNewMaps] Skipping closed map: MapId={newMap.MapId}, Name={newMap.Name}");
                        continue;
                    }

                    if (!Maps.Any(x => x.Id == newMap.Id && x.Channel == client.Tamer.Channel))
                    {
                        newMap.Channel = client.Tamer.Channel;

                        Maps.Add(newMap);
                    }
                }

                _lastMapsSearch = DateTime.Now.AddSeconds(5);
            }
        }


        public async Task SearchNewMaps(GameClient client)
        {
            var mapsToLoad = _mapper.Map<List<GameMap>>(await _sender.Send(new GameMapConfigsQuery()));

            foreach (var newMap in mapsToLoad)
            {
                // 🆕 MapIsOpen kontrolü ekle
                if (!newMap.MapIsOpen)
                {
                    _logger.Information($"[SearchNewMaps] Skipping closed map: MapId={newMap.MapId}");
                    continue;
                }

                if (newMap.MapId == client.Tamer.Location.MapId)
                {
                    if (!Maps.Any(x => x.MapId == client.Tamer.Location.MapId && x.Channel == client.Tamer.Channel))
                    {
                        if (newMap.Type == MapTypeEnum.Default)
                        {
                            newMap.Channel = client.Tamer.Channel;

                            Maps.Add(newMap);
                        }
                    }
                }
            }

            _lastMapsSearch = DateTime.Now.AddSeconds(5);
        }


        public async Task SearchMaps(CancellationToken cancellationToken)
        {
            if (DateTime.Now > _lastMapsSearch)
            {
                var activeMapConfigsFromDb = await _sender.Send(new GameMapsConfigQuery(MapTypeEnum.Default), cancellationToken);

                foreach (var mapConfigDTO in activeMapConfigsFromDb)
                {
                    // 🆕 MapIsOpen kontrolü ekle
                    if (!mapConfigDTO.MapIsOpen)
                    {
                        _logger.Information($"[SearchMaps] Skipping closed map: MapId={mapConfigDTO.MapId}, Name={mapConfigDTO.Name}");
                        continue;
                    }

                    MapTypeEnum mapType;

                    if (UtilitiesFunctions.DungeonMapIds.Contains((short)mapConfigDTO.MapId))
                    {
                        mapType = MapTypeEnum.Dungeon;
                    }
                    else if (UtilitiesFunctions.EventMapIds.Contains((short)mapConfigDTO.MapId))
                    {
                        mapType = MapTypeEnum.Event;
                    }
                    else if (UtilitiesFunctions.PvpMapIds.Contains((short)mapConfigDTO.MapId))
                    {
                        mapType = MapTypeEnum.Pvp;
                    }
                    else
                    {
                        mapType = MapTypeEnum.Default;
                    }

                    // Carrega todos os canais para este mapa de uma vez, se for um mapa de múltiplos canais
                    // 📌 **AYARLA**: Dinamik kanal sayısı kullan
                    int numberOfChannelsToLoad = (mapType == MapTypeEnum.Dungeon) ? 1 : mapConfigDTO.Channels;

                    for (byte channelId = 0; channelId < numberOfChannelsToLoad; channelId++)
                    {
                        if (!Maps.Any(x => x.MapId == mapConfigDTO.MapId && x.Channel == channelId))
                        {
                            var newGameMap = _mapper.Map<GameMap>(mapConfigDTO);
                            newGameMap.Channel = channelId;
                            newGameMap.Type = mapType;
                            newGameMap.Initialize();
                            Maps.Add(newGameMap);
                            _logger.Information($"[LazyLoad] :: MapId: {mapConfigDTO.MapId}, Name: {mapConfigDTO.Name}, Ch: {channelId}");
                        }
                    }
                }

                _lastMapsSearch = DateTime.Now.AddSeconds(5);
            }
        }


        // --------------------------------------------------------------------------

        /// <summary>
        /// Gets the map latest mobs.
        /// </summary>
        /// <returns>The mobs collection</returns>
        private async Task GetMapMobs(CancellationToken cancellationToken)
        {
            if (DateTime.Now > _lastMobsSearch)
            {
                List<GameMap> initializedMaps;

                lock (_mapsLock)
                {
                    initializedMaps = Maps.Where(x => x.Initialized).ToList();
                }

                foreach (var map in initializedMaps)
                {
                    var mapMobs = _mapper.Map<IList<MobConfigModel>>(
                        await _sender.Send(new MapMobConfigsQuery(map.Id), cancellationToken));

                    if (map.RequestMobsUpdate(mapMobs))
                        map.UpdateMobsList();
                }

                _lastMobsSearch = DateTime.Now.AddSeconds(30);
            }
        }

        /// <summary>
        /// Gets the consigned shops latest list.
        /// </summary>
        /// <returns>The consigned shops collection</returns>
        private async Task GetMapConsignedShops(CancellationToken cancellationToken)
        {
            if (DateTime.Now > _lastConsignedShopsSearch)
            {
                List<GameMap> initializedMaps;

                lock (_mapsLock)
                {
                    initializedMaps = Maps.Where(x => x.Initialized).ToList();
                }

                foreach (var map in initializedMaps)
                {
                    if (map.Operating)
                        continue;

                    var consignedShops =
                        _mapper.Map<List<ConsignedShop>>(await _sender.Send(new ConsignedShopsQuery((int)map.Id),
                            cancellationToken));

                    map.UpdateConsignedShops(consignedShops);
                }

                _lastConsignedShopsSearch = DateTime.Now.AddSeconds(15);
            }
        }

        /// <summary>
        /// The default hosted service "starting" method.
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CleanMaps();                              // Remove o mapa se nao tiver players
                    await SearchMaps(cancellationToken);            // Carrega os Mapas e os Mobs
                    await GetMapConsignedShops(cancellationToken);  // Carrega as ConsignedShops
                    await GetMapMobs(cancellationToken);            // Carrega os mobs do mapa

                    var tasks = new List<Task>();

                    Maps.ForEach(map => { tasks.Add(RunMap(map)); });

                    await Task.WhenAll(tasks);

                    await Task.Delay(500, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[StartAsync] :: {ex.Message}");
                    _logger.Error($"[StartAsync] :: {ex.StackTrace}");

                    await Task.Delay(1500, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Runs the target map operations.
        /// </summary>
        /// <param name="map">the target map</param>
        private async Task RunMap(GameMap map)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                map.Initialize();
                map.ManageHandlers();

                await Task.Run(() => TamerOperation(map));
                await Task.Run(() => MonsterOperation(map));
                await Task.Run(() => DropsOperation(map));
                await Task.Run(() => DigimonOperation(map));

                stopwatch.Stop();

                var totalTime = stopwatch.Elapsed.TotalMilliseconds;
                var delayTime = (int)Math.Max(500 - totalTime, 10);

                await Task.Delay(delayTime);
            }
            catch (Exception ex)
            {
                _logger.Error($"[RunMap] :: Unexpected error at map running for MapId {map.MapId}: {ex.Message}");
                _logger.Error($"[RunMap] :: {ex.StackTrace}");
            }
        }

        private async Task<bool> ValidateMapIsOpen(GameClient client, MapConfigDTO mapConfig)
        {
            try
            {
                if (mapConfig == null)
                {
                    _logger.Warning($"[ValidateMapIsOpen] Map not found for MapId={client.Tamer.Location.MapId}");
                    return false;
                }

                if (!mapConfig.MapIsOpen)
                {
                    _logger.Warning($"[ValidateMapIsOpen] Player {client.Tamer.Name} tried to enter closed map: {mapConfig.MapId}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[ValidateMapIsOpen] Exception: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Adds a new gameclient to the target map.
        /// </summary>
        /// <param name="client">The game client to be added.</param>
        public async Task AddClient(GameClient client)
        {
            try
            {
                // STEP 1: Harita kontrol
                var currentMapConfig = await _sender.Send(
                    new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId),
                    CancellationToken.None);

                if (currentMapConfig == null)
                {
                    _logger.Warning($"[AddClient] Map config not found for MapId={client.Tamer.Location.MapId}");
                    client.Disconnect();
                    return;
                }

                // STEP 2: KAPAL HARITA KONTROLÜ
                if (!currentMapConfig.MapIsOpen)
                {
                    _logger.Warning($"[AddClient] Map {client.Tamer.Location.MapId} is CLOSED!");

                    // Konumu güncelle
                    short lastMapId = (short)(client.Tamer.LastOpenMapId > 0 ? client.Tamer.LastOpenMapId : 3);
                    int lastX = client.Tamer.LastOpenMapX > 0 ? client.Tamer.LastOpenMapX : 20836;
                    int lastY = client.Tamer.LastOpenMapY > 0 ? client.Tamer.LastOpenMapY : 30517;

                    client.Tamer.Location.SetMapId(lastMapId);
                    client.Tamer.Location.SetX(lastX);
                    client.Tamer.Location.SetY(lastY);

                    client.Tamer.Partner.Location.SetMapId(lastMapId);
                    client.Tamer.Partner.Location.SetX(lastX);
                    client.Tamer.Partner.Location.SetY(lastY);

                    // ✅ DATABASE SAVE (BACKGROUND)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
                            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));
                            _logger.Information($"[AddClient] Saved redirected location");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[AddClient] Save error: {ex.Message}");
                        }
                    });

                    // ✅ RECURSIVE CALL - Yeni konumla harita yüklemesini yeniden dene
                    // ⚠️ SEÇENEK 1: DISCONNECT (Reconnect gerek)
                    client.SetGameQuit(true);
                    client.Disconnect(raiseEvent: true);  // ✅ raiseEvent: true 
                    return;
                }

                // STEP 3: Açık harita - SON AÇIK HARITA KAYDET (BACKGROUND'DA)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _sender.Send(new UpdateCharacterLastOpenMapCommand(
                            client.TamerId,
                            (short)client.Tamer.Location.MapId,
                            client.Tamer.Location.X,
                            client.Tamer.Location.Y
                        ));
                        _logger.Information($"[AddClient] Saved last open map");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[AddClient] LastOpenMap save error: {ex.Message}");
                    }
                });

                // Rama especial: teleport al target (TargetTamerIdTP)
                if (client.Tamer.TargetTamerIdTP > 0)
            {
                var map = Maps.FirstOrDefault(x =>
                    x.Clients.Exists(gameClient => gameClient.TamerId == client.Tamer.TargetTamerIdTP));

                client.SetLoading();
                client.Tamer.MobsInView.Clear();

                // 🔴 Protección: si no se encontró el mapa del target, hacemos fallback
                if (map == null)
                {
                    _logger.Warning(
                        $"[AddClient] No map found for TargetTamerIdTP={client.Tamer.TargetTamerIdTP} " +
                        $"(TamerId={client.TamerId}, Name={client.Tamer.Name}). Resetting TargetTamerIdTP and using Location MapId={client.Tamer.Location.MapId}, CH={client.Tamer.Channel}.");

                    // Limpia el TP objetivo para no quedar “corrupto”
                    client.Tamer.TargetTamerIdTP = 0;

                    // Fallback: rehacer el flujo normal (rama de abajo) y salir
                    await AddClient(client);
                    return;
                }

                // ✅ Desde aquí en adelante map NUNCA es null
                map.AddClient(client);
                client.Tamer.Revive();

                // Cargar tiendas consignadas sólo si hace falta
                if (map.ConsignedShops == null || map.ConsignedShops.Count == 0)
                {
                    var consignedShops =
                        _mapper.Map<List<ConsignedShop>>(
                            await _sender.Send(
                                new ConsignedShopsQuery((int)map.Id),
                                CancellationToken.None));

                    map.UpdateConsignedShops(consignedShops);
                }

                return;
            }

            // Rama normal: entrar al mapa según Location/Channel
            var normalMap = Maps.FirstOrDefault(x =>
                x.Initialized &&
                x.MapId == client.Tamer.Location.MapId &&
                x.Channel == client.Tamer.Channel);

            client.SetLoading();

            if (normalMap != null)
            {
                client.Tamer.MobsInView.Clear();
                normalMap.AddClient(client);
                client.Tamer.Revive();

                if (normalMap.ConsignedShops == null || normalMap.ConsignedShops.Count == 0)
                {
                    var consignedShops =
                        _mapper.Map<List<ConsignedShop>>(
                            await _sender.Send(
                                new ConsignedShopsQuery((int)normalMap.Id),
                                CancellationToken.None));

                    normalMap.UpdateConsignedShops(consignedShops);
                }
            }
            else
            {
                await Task.Run(async () =>
                {
                    var stopWatch = Stopwatch.StartNew();
                    var timeLimit = 15000;

                    var map = normalMap;

                    while (map == null)
                    {
                        await Task.Delay(2500);

                        map = Maps.FirstOrDefault(x =>
                            x.Initialized &&
                            x.MapId == client.Tamer.Location.MapId &&
                            x.Channel == client.Tamer.Channel);

                        _logger.Warning(
                            $"Waiting map {client.Tamer.Location.MapId} CH {client.Tamer.Channel} initialization.");

                        if (map == null)
                            await SearchNewMaps(client);

                        if (stopWatch.ElapsedMilliseconds >= timeLimit)
                        {
                            _logger.Warning(
                                $"The map instance {client.Tamer.Location.MapId} CH {client.Tamer.Channel} " +
                                $"has not been started, aborting process...");
                            break;
                        }
                    }

                    if (map == null)
                    {
                        client.Disconnect();
                    }
                    else
                    {
                        client.Tamer.MobsInView.Clear();
                        map.AddClient(client);
                        client.Tamer.Revive();

                        if (map.ConsignedShops == null || map.ConsignedShops.Count == 0)
                        {
                            var consignedShops =
                                _mapper.Map<List<ConsignedShop>>(
                                    await _sender.Send(
                                        new ConsignedShopsQuery((int)map.Id),
                                        CancellationToken.None));

                            map.UpdateConsignedShops(consignedShops);
                        }
                    }
                });
            }
            }
            catch (Exception ex)
            {
                _logger.Error($"[AddClient] Exception: {ex.Message}");
            }
        }


        public async Task EnsureConsignedShopsLoaded(int mapId, byte channel)
        {
            try
            {
                // ✅ FIX: Null check'i düzgün yap
                var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

                // ✅ map != null AND (konsinyeli dükkanlar yüklenmemişse)
                if (map != null && (map.ConsignedShops == null || map.ConsignedShops.Count == 0))
                {
                    var consignedShops = _mapper.Map<List<ConsignedShop>>(
                        await _sender.Send(
                            new ConsignedShopsQuery((int)map.Id),
                            CancellationToken.None));

                    map.UpdateConsignedShops(consignedShops);
                    _logger.Information($"[EnsureConsignedShopsLoaded] Shops loaded for MapId: {mapId}, Channel: {channel}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[EnsureConsignedShopsLoaded] Exception for MapId: {mapId}, Channel: {channel}: {ex.Message}");
                _logger.Error($"[EnsureConsignedShopsLoaded] StackTrace: {ex.StackTrace}");
            }
        }
        /// <summary>
        /// Removes the gameclient from the target map.
        /// </summary>
        /// <param name="client">The gameclient to be removed.</param>
        public void RemoveClient(GameClient client)
        {
            //_sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
            //_sender.Send(new UpdateDigimonLocationCommand(client.Partner.Location));

            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == client.TamerId));

            map?.BroadcastForTargetTamers(client.TamerId,
                new LocalMapSwapPacket(
                    client.Tamer.GeneralHandler,
                    client.Tamer.Partner.GeneralHandler,
                    client.Tamer.Location.X,
                    client.Tamer.Location.Y,
                    client.Tamer.Partner.Location.X,
                    client.Tamer.Partner.Location.Y
                ).Serialize()
            );

            _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
            _sender.Send(new UpdateDigimonLocationCommand(client.Partner.Location));

            map?.RemoveClient(client);
        }

        // =============================================================================

        #region Broadcast

        public void BroadcastForChannel(byte channel, byte[] packet)
        {
            var maps = Maps.Where(x => x.Channel == channel).ToList();

            maps?.ForEach(map => { map.BroadcastForMap(packet); });
        }

        public void BroadcastGlobal(byte[] packet)
        {
            var maps = Maps.Where(x => x.Clients.Any()).ToList();

            maps?.ForEach(map => { map.BroadcastForMap(packet); });
        }

        public void BroadcastForSelectedMaps(byte[] packet, List<int> mapIds)
        {
            var maps = Maps.Where(map => map.Clients.Any() && mapIds.Contains(map.MapId)).ToList();

            maps?.ForEach(map => { map.BroadcastForMap(packet); });
        }

        public void BroadcastForMap(short mapId, byte channel, byte[] packet)
        {
            var maps = Maps.Where(x => x.MapId == mapId).ToList();

            maps?.ForEach(map => { map.BroadcastForMap(packet); });
        }

        public void BroadcastForMapAllChannels(short mapId, byte[] packet)
        {
            var maps = Maps.Where(x => x.Clients.Exists(gameClient => gameClient.Tamer.Location.MapId == mapId))
                .SelectMany(map => map.Clients);
            maps.ToList().ForEach(client => { client.Send(packet); });
        }

        public void BroadcastForUniqueTamer(long tamerId, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(x => x.TamerId == tamerId));

            map?.BroadcastForUniqueTamer(tamerId, packet);
        }

        public void BroadcastForTargetTamers(List<long> targetTamers, byte[] packet)
        {
            Maps
                .Where(x => x.Clients.Any(gameClient => targetTamers.Contains(gameClient.TamerId)))
                .ToList()
                .ForEach(map => map.BroadcastForTargetTamers(targetTamers, packet));
        }

        public void BroadcastForTargetTamers(long sourceId, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == sourceId));

            map?.BroadcastForTargetTamers(map.TamersView[sourceId], packet);
        }

        public void BroadcastForTamerViewsAndSelf(long sourceId, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == sourceId));

            map?.BroadcastForTamerViewsAndSelf(sourceId, packet);
        }

        public void BroadcastForTamerViewsAndSelf(GameClient client, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient =>
                gameClient.TamerId == client.TamerId && gameClient.Tamer.Channel == client.Tamer.Channel));

            map?.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
        }

        public void BroadcastForTamerViews(GameClient client, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient =>
                gameClient.TamerId == client.TamerId && gameClient.Tamer.Channel == client.Tamer.Channel));

            map?.BroadcastForTamerViewOnly(client.TamerId, packet);
        }

        #endregion

        // =============================================================================

        public GameClient? FindClientByTamerId(long tamerId)
        {
            return Maps.SelectMany(map => map.Clients).FirstOrDefault(client => client.TamerId == tamerId);
        }

        public GameClient? FindClientByTamerName(string tamerName)
        {
            return Maps.SelectMany(map => map.Clients).FirstOrDefault(client => client.Tamer.Name == tamerName);
        }

        public GameClient? FindClientByTamerHandle(int handle)
        {
            return Maps.SelectMany(map => map.Clients).FirstOrDefault(client => client.Tamer?.GeneralHandler == handle);
        }

        public GameClient? FindClientByTamerHandleAndChannel(int handle, long TamerId)
        {
            return Maps.Where(x => x.Clients.Exists(gameClient => gameClient.TamerId == TamerId))
                .SelectMany(map => map.Clients)
                .FirstOrDefault(client => client.Tamer?.GeneralHandler == handle);
        }
        public void BroadcastForMap(short mapId, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId);

            map?.BroadcastForMap(packet);
        }

        public void AddMapDrop(Drop drop, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            map?.DropsToAdd.Add(drop);
        }

        public void RemoveDrop(Drop drop, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            map?.RemoveMapDrop(drop);
        }

        public Drop? GetDrop(short mapId, int dropHandler, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.GetDrop(dropHandler);
        }

        //Mobs
        #region Mob Attack

        public bool IsMobsAttacking(long tamerId, bool isSummon = false)
        {
            // Find where the Tamer is
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.MobsAttacking(tamerId, isSummon) ?? false;
        }

        public bool IMobsAttacking(short mapId, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.IMobsAttacking(tamerId) ?? false;
        }

        public bool MobsAttacking(short mapId, long tamerId, byte channel = 0)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            return map?.MobsAttacking(tamerId) ?? false;
        }

        public bool MobsAttacking(short mapId, long tamerId, bool Summon, byte channel = 0)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            return map?.MobsAttacking(tamerId, true) ?? false;
        }

        #endregion

        // ----------------------------------------------------------------------------

        public void SummonMobs(SummonMobModel summon, long tamerId)
        {
            var gm = Maps.FirstOrDefault(x => x.Clients.Exists(gc => gc.TamerId == tamerId));

            if (gm == null)
            {
                return;
            }
            else
            {
                gm.AddMob(summon);
            }
        }

        public void AddSummonMob(short mapId, SummonMobModel summon, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            map?.AddMobSumon(summon);
        }

        public void AddSummonMobs(short mapId, SummonMobModel summon)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId);

            map?.AddMob(summon);
        }

        //public void AddSummonMobs(short mapId, SummonMobModel summon, long tamerId)
        //{
        //    var map = Maps.FirstOrDefault(x => x.Clients.Exists(x => x.TamerId == tamerId));

        //    map?.AddMob(summon);
        //}
        
        //public void AddSummonMobs(SummonMobModel summon)
        //{
        //    foreach (var map in Maps)
        //    {
        //        map.AddMob(summon);  // Add the summon to every map
        //    }
        //}
        
        //public void AddMobs(short mapId, MobConfigModel mob, long tamerId)
        //{
        //    var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

        //    map?.AddMob(mob);
        //}

        public void AddSummonMobs(short mapId, SummonMobModel summon, byte channel = 0)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            map?.AddMob(summon);
        }

        // ----------------------------------------------------------------------------

        public MobConfigModel? GetMobByHandler(short mapId, int handler, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (map == null)
                return null;

            return map.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }

        public SummonMobModel? GetMobByHandler(short mapId, int handler, bool summon, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (map == null)
                return null;

            return map.SummonMobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }

        public DigimonModel? GetEnemyByHandler(short mapId, int handler, long tamerId)
        {
            return Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId))?
                .ConnectedTamers.Select(x => x.Partner).FirstOrDefault(x => x.GeneralHandler == handler);
        }

        public MobConfigModel? GetMobByHandler(short mapId, int handler, byte channel = 0)
        {
            return _mobIndex.TryGetValue((mapId, channel, handler), out var mob) ? mob : null;
        }

        public SummonMobModel? GetMobByHandler(short mapId, int handler, bool summon, byte channel = 0)
        {
            return _mobIndexSummon.TryGetValue((mapId, channel, handler), out var mob) ? mob : null;
        }

        // ----------------------------------------------------------------------------

        public List<CharacterModel> GetNearbyTamers(short mapId, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.NearbyTamers(tamerId);
        }

        public List<MobConfigModel> GetMobsNearbyPartner(Location location, int range, byte channel = 0)
        {
            var targetMap = Maps.FirstOrDefault(x => x.MapId == location.MapId && x.Channel == channel);

            if (targetMap == null)
                return default;

            var originX = location.X;
            var originY = location.Y;

            return GetTargetMobs(targetMap.Mobs.Where(x => x.Alive && !x.AwaitingKillSpawn).ToList(), originX, originY, range).DistinctBy(x => x.Id).ToList();
        }

        public List<MobConfigModel> GetMobsNearbyPartner(Location location, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return default;

            var originX = location.X;
            var originY = location.Y;

            return GetTargetMobs(targetMap.Mobs.Where(x => x.Alive).ToList(), originX, originY, range)
                .DistinctBy(x => x.Id).ToList();
        }

        public List<MobConfigModel> GetMobsNearbyPartnerByHandler(Location location, int handler, int range, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (map == null)
                return null;

            var targetMob = map.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (targetMob == null)
                return default;

            var originX = targetMob.CurrentLocation.X;
            var originY = targetMob.CurrentLocation.Y;

            var areaMobs = new List<MobConfigModel>();

            areaMobs.Add(targetMob);

            areaMobs.AddRange(GetTargetMobs(map.Mobs.Where(x => x.Alive).ToList(), originX, originY, range / 5));

            return areaMobs.DistinctBy(x => x.Id).ToList();
        }

        public List<MobConfigModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return default;

            var originMob = targetMap.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return default;

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMobs = new List<MobConfigModel>();
            targetMobs.Add(originMob);

            targetMobs.AddRange(GetTargetMobs(targetMap.Mobs.Where(x => x.Alive).ToList(), originX, originY, range));

            return targetMobs.DistinctBy(x => x.Id).ToList();
        }

        public List<MobConfigModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, byte channel = 0)
        {
            if (!_mobIndex.TryGetValue((mapId, channel, handler), out var originMob))
                return default;

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMap = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            if (targetMap == null)
                return default;

            var aliveMobs = targetMap.Mobs.Where(x => x.Alive && !x.AwaitingKillSpawn).ToList();

            var nearbyMobs = GetTargetMobs(aliveMobs, originX, originY, range);

            nearbyMobs.Add(originMob);

            return nearbyMobs.DistinctBy(x => x.Id).ToList();
        }

        public static List<MobConfigModel> GetTargetMobs(List<MobConfigModel> mobs, int originX, int originY, int range)
        {
            var targetMobs = new List<MobConfigModel>();

            foreach (var mob in mobs)
            {
                var mobX = mob.CurrentLocation.X;
                var mobY = mob.CurrentLocation.Y;

                var distance = CalculateDistance(originX, originY, mobX, mobY);

                if (distance <= range)
                {
                    targetMobs.Add(mob);
                }
            }

            return targetMobs;
        }

        // ----------------------------------------------------------------------------

        public List<SummonMobModel> GetMobsNearbyPartner(Location location, int range, bool Summon, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return default;

            var originX = location.X;
            var originY = location.Y;

            return GetTargetMobs(targetMap.SummonMobs.Where(x => x.Alive).ToList(), originX, originY, range)
                .DistinctBy(x => x.Id).ToList();
        }

        public List<SummonMobModel> GetMobsNearbyPartner(Location location, int range, bool Summon, byte channel = 0)
        {
            var targetMap = Maps.FirstOrDefault(x => x.MapId == location.MapId && x.Channel == channel);

            if (targetMap == null)
                return default;

            var originX = location.X;
            var originY = location.Y;

            return GetTargetMobs(targetMap.SummonMobs.Where(x => x.Alive && !x.AwaitingKillSpawn).ToList(), originX, originY, range).DistinctBy(x => x.Id).ToList();
        }

        public List<SummonMobModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, bool Summon, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return new List<SummonMobModel>();

            var originMob = targetMap.SummonMobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return new List<SummonMobModel>();

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMobs = new List<SummonMobModel>();
            targetMobs.Add(originMob);

            targetMobs.AddRange(GetTargetMobs(targetMap.SummonMobs.Where(x => x.Alive).ToList(), originX, originY,
                range));

            return targetMobs.DistinctBy(x => x.Id).ToList();
        }

        public List<SummonMobModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, bool Summon, byte channel)
        {
            var targetMap = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            if (targetMap == null)
                return default;

            var originMob = targetMap.SummonMobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return default;

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMobs = new List<SummonMobModel>();
            targetMobs.Add(originMob);

            targetMobs.AddRange(GetTargetMobs(targetMap.SummonMobs.Where(x => x.Alive && !x.AwaitingKillSpawn).ToList(), originX, originY, range));

            return targetMobs.DistinctBy(x => x.Id).ToList();
        }

        public static List<SummonMobModel> GetTargetMobs(List<SummonMobModel> mobs, int originX, int originY, int range)
        {
            var targetMobs = new List<SummonMobModel>();

            foreach (var mob in mobs)
            {
                var mobX = mob.CurrentLocation.X;
                var mobY = mob.CurrentLocation.Y;

                var distance = CalculateDistance(originX, originY, mobX, mobY);

                if (distance <= range)
                {
                    targetMobs.Add(mob);
                }
            }

            return targetMobs;
        }

        // ----------------------------------------------------------------------------

        public IMob? GetIMobByHandler(int handler, long tamerId, bool isSummon = false)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (map == null)
                return null;

            if (isSummon)
            {
                return map.SummonMobs.FirstOrDefault(x => x.GeneralHandler == handler);
            }
            else
            {
                return map.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);
            }
        }

        public IMob GetNearestIMobToTarget(short mapId, int handler, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return null;

            var originMob = targetMap.IMobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return null;

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            return GetNearestIMob(targetMap.IMobs.Where(x => x.Alive).ToList(), originX, originY, range);
        }

        public static IMob GetNearestIMob(List<IMob> mobs, int originX, int originY, int range)
        {
            IMob nearestMob = null;
            double minDistance = double.MaxValue;

            foreach (var mob in mobs)
            {
                var mobX = mob.CurrentLocation.X;
                var mobY = mob.CurrentLocation.Y;
                var distance = CalculateDistance(originX, originY, mobX, mobY);

                if (distance <= range && distance < minDistance)
                {
                    minDistance = distance;
                    nearestMob = mob;
                }
            }

            return nearestMob;
        }
        
        public List<IMob> GetIMobsNearbyPartner(Location location, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return default;

            var originX = location.X;
            var originY = location.Y;

            return GetTargetIMobs(targetMap.IMobs.Where(x => x.Alive).ToList(), originX, originY, range)
                .DistinctBy(x => x.Id).ToList();
        }

        public List<IMob> GetIMobsNearbyTargetMob(short mapId, int handler, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return default;

            var originMob = targetMap.IMobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return default;

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMobs = new List<IMob>();
            targetMobs.Add(originMob);

            targetMobs.AddRange(GetTargetIMobs(targetMap.IMobs.Where(x => x.Alive).ToList(), originX, originY, range));

            return targetMobs.DistinctBy(x => x.Id).ToList();
        }
        
        public IMob? GetIMobByHandler(short mapId, int handler, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.IMobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }
        
        public static List<IMob> GetTargetIMobs(List<IMob> mobs, int originX, int originY, int range)
        {
            var targetMobs = new List<IMob>();

            foreach (var mob in mobs)
            {
                var mobX = mob.CurrentLocation.X;
                var mobY = mob.CurrentLocation.Y;

                var distance = CalculateDistance(originX, originY, mobX, mobY);

                if (distance <= range)
                {
                    targetMobs.Add(mob);
                }
            }

            return targetMobs;
        }

        // ----------------------------------------------------------------------------

        private static double CalculateDistance(int x1, int y1, int x2, int y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;

            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        // ----------------------------------------------------------------------------

        public bool EnemiesAttacking(short mapId, long partnerId, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.PlayersAttacking(partnerId) ?? false;
        }

        public async Task CallDiscord(string message, GameClient tamer, string coloured, string local, string Channel = "", bool custom = false)
        {
            var myChannel = Channel;
            var myToken = "";

            var payload = new
            {
                tts = false,
                embeds = new[]
                {
                    new
                    {
                        type = "rich",
                        color = Convert.ToInt32(coloured, 16),
                        footer = new
                        {
                            text = custom
                                ? $"{message} "
                                : $"[{local} ][Channel: {tamer.Tamer.Channel}],  {tamer.Tamer.Name}:  {message}"
                        },
                    }
                }
            };

            var json_data = JsonConvert.SerializeObject(payload);

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://discordapp.com/api/v6/channels/{myChannel}/messages"),
                    Content = new StringContent(json_data, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bot {myToken}");

                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
            }
        }

        public async Task CallDiscordWarnings(string message, string coloured, string dischannel)
        {
            var myChannel = dischannel;
            var myToken = "";

            // Create a payload with plain text content instead of embeds
            var payload = new
            {
                // Send the message without role mention
                content = message
            };

            var json_data = JsonConvert.SerializeObject(payload);

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://discordapp.com/api/v6/channels/{myChannel}/messages"),
                    Content = new StringContent(json_data, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bot {myToken}");

                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
            }
        }

        public async Task CallDiscordWarnings(string title, string message, string coloured, string dischannel, string role, long digimonid)
        {
            var myChannel = dischannel;
            var myToken = "";

            var payload = new
            {
                title = title,
                message = message,
                coloured = coloured,
                dischannel = dischannel,
                role = role,
                digimonid = digimonid,
                type = 1
            };

            var json_data = JsonConvert.SerializeObject(payload);

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://discordapp.com/api/v6/channels/{myChannel}/messages"),
                    Content = new StringContent(json_data, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bot {myToken}");

                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
            }
        }
    }
}