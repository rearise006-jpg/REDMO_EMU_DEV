using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Constants;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Models.TamerShop;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace DigitalWorldOnline.GameHost
{
    public sealed partial class DungeonsServer
    {
        private DateTime _lastMapsSearch = DateTime.Now;
        //private DateTime _lastMobsSearch = DateTime.Now;
        private DateTime _lastConsignedShopsSearch = DateTime.Now;

        private readonly int _startToSee = DungeonServerConstants.StartToSeeMob;
        private readonly int _stopSeeing = DungeonServerConstants.StopToSeeMob;

        // CLEAN MAPS
        public Task CleanMaps()
        {
            var mapsToRemove = new List<GameMap>();
            mapsToRemove.AddRange(Maps.Where(x => x.CloseMap));

            foreach (var map in mapsToRemove)
            {
                // _logger.Information($"Removing inactive instance for {map.Type} map {map.Id} - {map.Name}...");
                Maps.Remove(map);
            }

            return Task.CompletedTask;
        }

        public Task CleanMap(int DungeonId)
        {
            var mapToClose = Maps.FirstOrDefault(x => x.DungeonId == DungeonId);

            if (mapToClose != null)
            {
                // _logger.Information($"Removing inactive instance for {mapToClose.Type} mapID: {mapToClose.MapId} - {mapToClose.Name}");
                Maps.Remove(mapToClose);
            }

            return Task.CompletedTask;
        }


        // SEARCH MAPS
        private async Task SearchNewMapsInternal(bool isParty, GameClient? client = null)
        {
            var mapsToLoad = _mapper.Map<List<GameMap>>(await _sender.Send(new GameMapsConfigQuery(MapTypeEnum.Dungeon)));
            var party = isParty && client != null ? _partyManager.FindParty(client.TamerId) : null;

            foreach (var newMap in mapsToLoad)
            {
                bool shouldAddMap = false;
                if (isParty && party != null)
                {
                    shouldAddMap = !Maps.Exists(x => x.DungeonId == party.Id) && newMap.MapId == client?.Tamer.Location.MapId;
                }
                else if (client != null)
                {
                    shouldAddMap = !Maps.Exists(x => x.DungeonId == client.TamerId) && newMap.MapId == client.Tamer.Location.MapId;
                }
                else if (!isParty && client == null)
                {
                    shouldAddMap = _partyManager.Parties.Any(partymap =>
                        Maps.All(x => x.Id == partymap.Id) &&
                        newMap.MapId == partymap.Members.ElementAt((byte)partymap.LeaderId).Value.Location.MapId);
                }

                if (shouldAddMap)
                {
                    _logger.Debug($"Initializing new instance for {newMap.Type} {(isParty ? $"party {party?.Id}" : $"tamer {client?.TamerId}")} - {newMap.Name}...");

                    var newDungeon = (GameMap)newMap.Clone();

                    newDungeon.FilterMobsForNewInstance();

                    if (isParty && party != null)
                    {
                        newDungeon.SetId(party.Id);
                    }
                    else if (client != null)
                    {
                        newDungeon.SetId((int)client.TamerId);
                    }

                    Maps.Add(newDungeon);
                }
            }
        }

        public async Task SearchNewMaps(CancellationToken cancellationToken)
        {
            if (DateTime.Now > _lastMapsSearch)
            {
                await SearchNewMapsInternal(false);

                _lastMapsSearch = DateTime.Now.AddSeconds(DungeonServerConstants.MapsSearchIntervalSeconds);
            }
        }

        public async Task SearchNewMaps(bool IsParty, GameClient client)
        {
            await SearchNewMapsInternal(IsParty, client);
        }

        // MAP OBJECTS
        public async Task GetMapObjects(CancellationToken cancellationToken)
        {
            await GetMapMobs(cancellationToken);
        }

        public async Task GetMapObjects()
        {
            await GetMapMobs();
        }

        // MAP MOBS
        private async Task GetMapMobsInternal(CancellationToken cancellationToken = default)
        {
            var initializedMaps = Maps.Where(x => x.Initialized).ToList();

            foreach (var map in initializedMaps)
            {
                var query = new MapMobConfigsQuery(map.Id);

                var mapMobs = _mapper.Map<List<MobConfigModel>>(
                    cancellationToken == default
                        ? await _sender.Send(query)
                        : await _sender.Send(query, cancellationToken));

                if (mapMobs != null)
                {
                    mapMobs.RemoveAll(x => x.Coliseum && x.Round > 0);
                }

                if (map.RequestMobsUpdate(mapMobs))
                {
                    map.UpdateMobsList();
                }
            }
        }

        private async Task GetMapMobs(CancellationToken cancellationToken) => await GetMapMobsInternal(cancellationToken);

        private async Task GetMapMobs() => await GetMapMobsInternal();

        /// <summary>
        /// Gets the consigned shops latest list.
        /// </summary>
        /// <returns>The consigned shops collection</returns>
        private async Task GetMapConsignedShops(CancellationToken cancellationToken)
        {
            if (DateTime.Now > _lastConsignedShopsSearch)
            {
                // Take a snapshot of initialized maps
                var initializedMaps = Maps.Where(x => x.Initialized).ToList();
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
                    await CleanMaps();
                    await GetMapObjects(cancellationToken);

                    var tasks = new List<Task>();

                    Maps.ForEach(map => { tasks.Add(RunMap(map)); });

                    await Task.WhenAll(tasks);

                    await Task.Delay(500, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Unexpected map exception: {ex.Message} {ex.StackTrace}");
                    await Task.Delay(3000, cancellationToken);
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
                map.Initialize();
                map.ManageHandlers();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var tasks = new List<Task>
                {
                    Task.Run(() => TamerOperation(map)),
                    Task.Run(() => DigimonOperation(map)),
                    Task.Run(() => MonsterOperation(map)),
                    Task.Run(() => DropsOperation(map))
                };

                await Task.WhenAll(tasks);

                stopwatch.Stop();
                var totalTime = stopwatch.Elapsed.TotalMilliseconds;

                if (totalTime >= 1000)
                    _logger.Information($"[DungeonServer] :: BaseOperation -> RunMap on Map [{map.MapId}] : {totalTime}.");

                await Task.Delay(300);
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected error at map running: {ex.Message} {ex.StackTrace}.");
            }
        }

        /// <summary>
        /// Adds a new gameclient to the target map.
        /// </summary>
        /// <param name="client">The game client to be added.</param>
        public async Task AddClient(GameClient client)
        {
            if (client.Tamer.TargetTamerIdTP > 0)
            {
                var map = Maps.FirstOrDefault(x => x.Clients.Exists(y => y.TamerId == client.Tamer.TargetTamerIdTP));
                client.SetLoading();

                client.Tamer.MobsInView.Clear();
                map?.AddClient(client);
                client.Tamer.Revive();


            }
            else
            {
                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));
                if (mapConfig.Type == MapTypeEnum.Dungeon)
                {
                    var party = _partyManager.FindParty(client.TamerId);

                    if (party != null)
                    {
                        var partyMap = Maps.FirstOrDefault(x => x.Initialized &&
                                                                x.DungeonId == party.LeaderId &&
                                                                x.MapId == client.Tamer.Location.MapId ||
                                                                x.DungeonId == party.Id &&
                                                                x.MapId == client.Tamer.Location.MapId);

                        if (partyMap != null)
                        {
                            client.SetLoading();

                            client.Tamer.MobsInView.Clear();
                            partyMap.AddClient(client);
                            client.Tamer.Revive();
                        }
                        else
                        {
                            Maps.RemoveAll(x => x.DungeonId == party.LeaderId || x.DungeonId == party.Id);

                            await SearchNewMaps(true, client);

                            while (partyMap == null)
                            {
                                partyMap = Maps.FirstOrDefault(x => x.Initialized &&
                                                                    (x.DungeonId == party.LeaderId ||
                                                                     x.DungeonId == party.Id) &&
                                                                    x.MapId == client.Tamer.Location.MapId);

                                if (partyMap != null)
                                {
                                    client.Tamer.MobsInView.Clear();
                                    partyMap.AddClient(client);
                                    client.Tamer.Revive();
                                    return;
                                }

                                _logger.Warning($"Waiting Dungeon map {client.Tamer.Location.MapId} initialization.");
                                await Task.Delay(1000);
                            }
                        }
                    }
                    else
                    {
                        await SearchNewMaps(false, client);

                        var map = Maps.FirstOrDefault(x => x.Initialized && x.DungeonId == client.Tamer.Id);

                        if (map != null)
                        {
                            client.Tamer.MobsInView.Clear();
                            map.AddClient(client);
                            client.Tamer.Revive();
                        }
                        else
                        {
                            await Task.Run(async () =>
                            {
                                while (map == null)
                                {
                                    await Task.Delay(1000);

                                    map = Maps.FirstOrDefault(x => x.Initialized && x.DungeonId == client.Tamer.Id);

                                    _logger.Warning($"Waiting Dungeon map {client.Tamer.Location.MapId} initialization.");
                                }

                                client.Tamer.MobsInView.Clear();
                                map.AddClient(client);
                                client.Tamer.Revive();
                            });
                        }
                    }
                }
            }

            return;
        }

        /// <summary>
        /// Removes the gameclient from the target map.
        /// Handles cleanup of empty & completed dungeon instances.
       
        /// </summary>
        public void RemoveClient(GameClient client)
        {
            if (client == null)
                return;

            // Guardar ubicaciones antes de salir
            _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));
            _sender.Send(new UpdateDigimonLocationCommand(client.Partner.Location));

            // Buscar la instancia donde está este cliente
            var map = Maps.FirstOrDefault(x => x.Clients.Any(gameClient => gameClient.TamerId == client.TamerId));

            if (map == null)
                return;

            // Remover cliente
            map.RemoveClient(client);

            Console.WriteLine($"[DUNGEON REMOVE CLIENT] {client.Tamer.Name} removed from instance Id={map.Id}, DungeonId={map.DungeonId}, MapId={map.MapId}. Remaining={map.Clients.Count}");

            // Si aún quedan clientes → no limpiar
            if (map.Clients.Count > 0)
                return;

            // Si la instancia NO está completada → no limpiar
           // if (!map.IsDungeonCompleted)
           // {
           //     Console.WriteLine($"[DUNGEON KEEP ALIVE] Instance Id={map.Id} not completed. Keeping alive.");
             //   return;
          //  }

            // Si llega aquí → instancia completada + vacía → limpiarla
            Console.WriteLine($"[DUNGEON CLEANUP] Removing completed and empty dungeon instance Id={map.Id}, DungeonId={map.DungeonId}, MapId={map.MapId}");

            Maps.Remove(map);
        }


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

        public void BroadcastForMap(short mapId, byte[] packet, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Clients.Exists(y => y.TamerId == tamerId));

            map?.BroadcastForMap(packet);
        }

        public void BroadcastForUniqueTamer(long tamerId, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            map?.BroadcastForUniqueTamer(tamerId, packet);
        }

        public void BroadcastForMapAllChannels(short mapId, byte[] packet)
        {
            var maps = Maps.Where(x => x.Clients.Exists(gameClient => gameClient.Tamer.Location.MapId == mapId)).SelectMany(map => map.Clients);
            maps.ToList().ForEach(client => { client.Send(packet); });
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

        public void BroadcastForTamerViews(long sourceId, byte[] packet)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == sourceId));

            map?.BroadcastForTamerViewOnly(sourceId, packet);
        }

        #endregion

        #region GameClient

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

        #endregion

        #region Drops

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

        #endregion

        #region Mob Attack

        public bool IsMobsAttacking(long tamerId, bool isSummon = false)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.MobsAttacking(tamerId, isSummon) ?? false;
        }

        public bool MobsAttacking(short mapId, long tamerId, byte channel = 0)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            return map?.MobsAttacking(tamerId) ?? false;
        }

        public bool MobsAttacking(short mapId, long tamerId, bool Summon, byte channel = 0)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(x => x.TamerId == tamerId));

            return map?.MobsAttacking(tamerId) ?? false;
        }

        #endregion

        public List<CharacterModel> GetNearbyTamers(short mapId, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.NearbyTamers(tamerId);
        }

        public void AddSummonMobs(short mapId, SummonMobModel summon, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(x => x.TamerId == tamerId));

            map?.AddMob(summon);
        }

        public void AddSummonMobs(short mapId, SummonMobModel summon, byte channel = 0)
        {
            var map = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            map?.AddMob(summon);
        }

        public void AddMobs(short mapId, MobConfigModel mob, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            map?.AddMob(mob);
        }

        // --------------------------------------------------------------------------------------------------------------

        public MobConfigModel? GetMobByHandler(int handler, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (map == null)
                return null;

            return map.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }

        public MobConfigModel? GetMobByHandler(short mapId, int handler, byte channel = 0)
        {
            return Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel)?.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }

        public SummonMobModel? GetMobByHandler(int handler, long tamerId, bool summon)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.SummonMobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }

        public SummonMobModel? GetMobByHandler(short mapId, int handler, bool summon, byte channel = 0)
        {
            return Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel)?.SummonMobs.FirstOrDefault(x => x.GeneralHandler == handler);
        }

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

        // --------------------------------------------------------------------------------------------------------------

        public List<MobConfigModel> GetMobsNearbyPartner(Location location, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return new List<MobConfigModel>();

            var originX = location.X;
            var originY = location.Y;

            return GetTargetMobs(targetMap.Mobs.Where(x => x.Alive).ToList(), originX, originY, range)
                .DistinctBy(x => x.Id).ToList();
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

        public List<MobConfigModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return new List<MobConfigModel>();

            var originMob = targetMap.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return new List<MobConfigModel>();

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMobs = new List<MobConfigModel>();
            targetMobs.Add(originMob);

            targetMobs.AddRange(GetTargetMobs(targetMap.Mobs.Where(x => x.Alive).ToList(), originX, originY,
                range / 5));

            return targetMobs.DistinctBy(x => x.Id).ToList();
        }

        public List<MobConfigModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, byte channel = 0)
        {
            var targetMap = Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel);

            if (targetMap == null)
                return default;

            var originMob = targetMap.Mobs.FirstOrDefault(x => x.GeneralHandler == handler);

            if (originMob == null)
                return default;

            var originX = originMob.CurrentLocation.X;
            var originY = originMob.CurrentLocation.Y;

            var targetMobs = new List<MobConfigModel>();
            targetMobs.Add(originMob);

            targetMobs.AddRange(GetTargetMobs(targetMap.Mobs.Where(x => x.Alive && !x.AwaitingKillSpawn).ToList(), originX, originY, range));

            return targetMobs.DistinctBy(x => x.Id).ToList();
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

        // --------------------------------------------------------------------------------------------------------------

        public List<SummonMobModel> GetMobsNearbyPartner(Location location, int range, bool Summon, long tamerId)
        {
            var targetMap = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            if (targetMap == null)
                return new List<SummonMobModel>();

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
                range / 5));

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

        // --------------------------------------------------------------------------------------------------------------

        public IMob? GetIMobByHandler(short mapId, int handler, long tamerId)
        {
            var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));

            return map?.IMobs.FirstOrDefault(x => x.GeneralHandler == handler);
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


        private static double CalculateDistance(int x1, int y1, int x2, int y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            
        }

        public async Task PvpDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 9101)
                return;

            int[] PvpDungeon = { 100070};

            //Console.WriteLine($"[1600-KILL] MobType {mob.Type} died. Checking if it's one of the bosses...");

            if (!PvpDungeon.Contains(mob.Type))
                return;

            //Console.WriteLine($"[1600-BOSS] Boss {mob.Type} died. Evaluating remaining bosses...");


            foreach (var bossType in PvpDungeon)
            {
                var bossAlive = map.Mobs.Any(m => m.Type == bossType && !m.Dead);
               // Console.WriteLine($"[1600-BOSS-STATE] BossType {bossType} Alive={bossAlive}");
            }

            //===========================================================
            // DETECCIÓN DE TODOS LOS BOSSES MUERTOS
            //===========================================================
            bool allBossesDead = PvpDungeon.All(type =>
                map.Mobs.All(m => m.Type != type || m.Dead));

            //Console.WriteLine($"[1600-ALL-DEAD] → {allBossesDead}");

            if (!allBossesDead)
                return;

            //===========================================================
            // Diagnóstico de instancia recibida
            //===========================================================
            //  Console.WriteLine($"[1600-MAP-PARAM] map.Id={map.Id}, DungeonId={map.DungeonId}, Hash={map.GetHashCode()}");

           // Console.WriteLine("[1600-MAPS LIST] Active instances in DungeonsServer:");
           //   foreach (var m in Maps)
           //  {
           //      Console.WriteLine(
            //  $" → InstanceId={m.Id}, MapId={m.MapId}, DungeonId={m.DungeonId}, Hash={m.GetHashCode()}, Completed={m.IsDungeonCompleted}, Clients={m.Clients.Count}");
            //  }

            //===========================================================
            // BUSCAR LA INSTANCIA REAL
            //===========================================================
            var realInstance = Maps.FirstOrDefault(x =>
                x.Id == map.Id ||
                (x.DungeonId == map.DungeonId && x.MapId == map.MapId));

            if (realInstance == null)
            {
             //   Console.WriteLine($"[1600-ERROR] NO REAL INSTANCE MATCH FOUND. Flag applied to local copy only.");
                map.IsDungeonCompleted = true;
                map.DungeonCompletedTime = DateTime.Now;
                return;
            }

            //===========================================================
            // MARCAR DUNGEON COMPLETADO
            //===========================================================
            realInstance.IsDungeonCompleted = true;
            realInstance.DungeonCompletedTime = DateTime.Now;

           // Console.WriteLine( $"[1600-COMPLETE] Marked REAL INSTANCE completed. InstanceId={realInstance.Id}, DungeonId={realInstance.DungeonId}, Hash={realInstance.GetHashCode()}");
        }



        public async Task VerifyEDGNDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1600)
                return;

            int[] zdgBossTypes = { 51070, 51071, 99710 };

            //Console.WriteLine($"[1600-KILL] MobType {mob.Type} died. Checking if it's one of the bosses...");

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            //Console.WriteLine($"[1600-BOSS] Boss {mob.Type} died. Evaluating remaining bosses...");


            foreach (var bossType in zdgBossTypes)
            {
                var bossAlive = map.Mobs.Any(m => m.Type == bossType && !m.Dead);
               // Console.WriteLine($"[1600-BOSS-STATE] BossType {bossType} Alive={bossAlive}");
            }

            //===========================================================
            // DETECCIÓN DE TODOS LOS BOSSES MUERTOS
            //===========================================================
            bool allBossesDead = zdgBossTypes.All(type =>
                map.Mobs.All(m => m.Type != type || m.Dead));

            //Console.WriteLine($"[1600-ALL-DEAD] → {allBossesDead}");

            if (!allBossesDead)
                return;

            //===========================================================
            // Diagnóstico de instancia recibida
            //===========================================================
            //  Console.WriteLine($"[1600-MAP-PARAM] map.Id={map.Id}, DungeonId={map.DungeonId}, Hash={map.GetHashCode()}");

           // Console.WriteLine("[1600-MAPS LIST] Active instances in DungeonsServer:");
           //   foreach (var m in Maps)
           //  {
           //      Console.WriteLine(
            //  $" → InstanceId={m.Id}, MapId={m.MapId}, DungeonId={m.DungeonId}, Hash={m.GetHashCode()}, Completed={m.IsDungeonCompleted}, Clients={m.Clients.Count}");
            //  }

            //===========================================================
            // BUSCAR LA INSTANCIA REAL
            //===========================================================
            var realInstance = Maps.FirstOrDefault(x =>
                x.Id == map.Id ||
                (x.DungeonId == map.DungeonId && x.MapId == map.MapId));

            if (realInstance == null)
            {
             //   Console.WriteLine($"[1600-ERROR] NO REAL INSTANCE MATCH FOUND. Flag applied to local copy only.");
                map.IsDungeonCompleted = true;
                map.DungeonCompletedTime = DateTime.Now;
                return;
            }

            //===========================================================
            // MARCAR DUNGEON COMPLETADO
            //===========================================================
            realInstance.IsDungeonCompleted = true;
            realInstance.DungeonCompletedTime = DateTime.Now;

           // Console.WriteLine( $"[1600-COMPLETE] Marked REAL INSTANCE completed. InstanceId={realInstance.Id}, DungeonId={realInstance.DungeonId}, Hash={realInstance.GetHashCode()}");
        }



        public async Task VerifyZDGNDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1601)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51082, 51085, 51084, 51083 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[ZDGN Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: ZDG Normal - Uprising flame");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyBDGNDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1602)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51090, 99711, 51225 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[BDGN Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: BDG Normal - Trace of Black Steel");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyQDGNDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1603)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51101, 51099, 51100, 51098 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[QDGN Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: QDG Normal - Descending Thunder God");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyEDGHDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1610)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51108, 51109, 99710 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[EDGH Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: EDG Hard - Scar of Water Crystal");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }

        }

        public async Task VerifyZDGHDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1611)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51215, 51216, 51217, 51218 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[ZDGH Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: ZDG Hard - Uprising flame");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyBDGHDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1612)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51225, 51223, 99711 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[BDGH Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: BDG Hard - Trace of Black Steel");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyQDGHDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1613)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51040, 51041, 51042, 51043 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[QDGH Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: QDG Hard - Descending Thunder God");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyFDGDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1608)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51179 };

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[FDGN Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: FDG Normal - Fanglongmon UnderGround");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        public async Task VerifyRBNDungeonCompletion(GameMap map, IMob mob, GameClient client)
        {
            if (map.MapId != 1703)
                return;

            // Check if this is one of the target mob types
            int[] zdgBossTypes = { 51152};

            if (!zdgBossTypes.Contains(mob.Type))
                return;

            // Verify if all target mobs are dead in this instance
            bool allBossesDead = true;
            foreach (int bossType in zdgBossTypes)
            {
                // Check if any mob of this type is still alive
                if (map.Mobs.Any(m => m.Type == bossType && !m.Dead))
                {
                    allBossesDead = false;
                    break;
                }
            }

            if (allBossesDead)
            {
                // Get party information and build the message
                var party = _partyManager.FindParty(client.TamerId);
                string partyLeaderName = "Unknown";
                List<string> allPartyMemberNames = new List<string>();

                // Build the party info
                if (party != null)
                {
                    // Find the party leader
                    var partyLeaderClient = Maps
                        .SelectMany(m => m.Clients)
                        .FirstOrDefault(c => c.TamerId == party.LeaderId);

                    if (partyLeaderClient != null)
                    {
                        partyLeaderName = partyLeaderClient.Tamer.Name;

                        // Get all party members and their names
                        foreach (var member in party.Members.Values)
                        {
                            var memberClient = Maps.SelectMany(m => m.Clients).FirstOrDefault(c => c.TamerId == member.Id);
                            if (memberClient != null)
                            {
                                allPartyMemberNames.Add(memberClient.Tamer.Name);
                            }
                        }
                    }
                }

                // Calculate clear time
                TimeSpan clearTime = DateTime.Now - (map.CreationTime ?? DateTime.Now);
                string formattedClearTime = $"{clearTime.Minutes:D2}:{clearTime.Seconds:D2}";

                // Calculate respawn time if needed
                DateTimeOffset respawnTime = DateTimeOffset.Now.AddHours(3);
                long unixTimeSeconds = respawnTime.ToUnixTimeSeconds();

                // Build the message with proper formatting
                StringBuilder messageContent = new StringBuilder();
                messageContent.AppendLine("**[RBN Success]**");
                messageContent.AppendLine($"TamerName: {client.Tamer.Name}");

                // Add party info based on whether user is in a party or solo
                if (party != null)
                {
                    messageContent.AppendLine($"Party Leader: {partyLeaderName}");
                    if (allPartyMemberNames.Count > 0)
                    {
                        messageContent.AppendLine($"Party Members: {string.Join(", ", allPartyMemberNames)}");
                    }
                }
                else
                {
                    messageContent.AppendLine("Solo Squad");
                }

                messageContent.AppendLine($"Clear time: {formattedClearTime}");
                messageContent.AppendLine($"Dungeon Name: RB Normal - Royal Base");
                //messageContent.Append($"Boss will respawn <t:{unixTimeSeconds}:R> at <t:{unixTimeSeconds}:t>");

                // Send Discord notification
                //await CallDiscordWarnings(messageContent.ToString(), "3CE813", "");
                    map.IsDungeonCompleted = true;
                    map.DungeonCompletedTime = DateTime.Now;
            }
        }

        
        // ------------------------------------------------------------------------------
        // EDG Skill
        // ------------------------------------------------------------------------------

        #region EDG Get Skill in order

        public int GetMegaSeadraSkill(IMob mob)
        {
            if (mob.Type != 51070)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 121; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 120; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 119; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 118; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetMarinDevimonSkill(IMob mob)
        {
            if (mob.Type != 51071)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 126; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 125; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 124; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 123; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetXuanwumonSkill(IMob mob)
        {
            if (mob.Type != 51076)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 138; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 138; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 138; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 138; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }

        #endregion

        // ------------------------------------------------------------------------------
        // ZDG Skill & mechanics
        // ------------------------------------------------------------------------------

        #region ZDG GET skill in order

        public void ZDGPhoenixMechanic(IMob mob, long tamerId)
        {
            try
            {
                // Log detailed information about input parameters
                // _logger.Information($"ZDGPhoenixMechanic called with mob: {(mob == null ? "null" : $"Type={mob.Type}")} and tamerId: {tamerId}");

                if (mob == null)
                {
                    //_logger.Error("ZDGPhoenixMechanic failed: mob parameter is null");
                    return;
                }

                // Check for null mob location
                if (mob.CurrentLocation == null)
                {
                   // _logger.Error($"ZDGPhoenixMechanic failed: mob (Type={mob.Type}, Id={mob.Id}) has null CurrentLocation");
                    return;
                }

                var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));
                if (map == null)
                {
                   // _logger.Error($"ZDGPhoenixMechanic failed: No map found containing client with tamerId {tamerId}");
                    return;
                }

                // Get client for message broadcasting
                var client = map.Clients.FirstOrDefault(gc => gc.TamerId == tamerId);
                if (client == null)
                {
                    //_logger.Error($"ZDGPhoenixMechanic failed: No client found with tamerId {tamerId}");
                    return;
                }

                // Check if client.Tamer is null
                if (client.Tamer == null)
                {
                   // _logger.Error($"ZDGPhoenixMechanic failed: Client's Tamer is null for tamerId {tamerId}");
                    return;
                }

               // _logger.Information($"Mob {mob.Type} summoning reinforcements");

                // SummonDTO ID for the reinforcement mobs
                long summonDTOId = 8;

                // Check if _assets is null
                if (_assets == null)
                {
                    //_logger.Error("ZDGPhoenixMechanic failed: _assets is null");
                    return;
                }

                // Find summon info based on Summon ID
                var summonInfo = _assets.SummonInfo?.FirstOrDefault(x => x.Id == summonDTOId);
                if (summonInfo == null)
                {
                    //_logger.Error($"Invalid Summon ID: {summonDTOId} - Could not find SummonInfo");
                    return;
                }

                // Check if SummonedMobs collection is null or empty
                if (summonInfo.SummonedMobs == null || !summonInfo.SummonedMobs.Any())
                {
                    //_logger.Error($"ZDGPhoenixMechanic failed: summonInfo.SummonedMobs is null or empty for SummonID {summonDTOId}");
                    return;
                }

                // Generate a unique threshold identifier for this spawn event
                int uniqueThreshold = (int)(DateTime.Now.Ticks % 10000);

                // Track which types of mobs we've spawned to ensure all debuff types are applied
                List<int> spawnedMobTypes = new List<int>();

                foreach (var mobToAdd in summonInfo.SummonedMobs)
                {
                    if (mobToAdd == null)
                    {
                        // _logger.Warning("Skipping null mob in summonInfo.SummonedMobs");
                        continue;
                    }

                    try
                    {
                        // Clone the mob to avoid modifying the original template
                        var summon = (SummonMobModel)mobToAdd.Clone();

                        // Set a unique ID based on current time to avoid ID conflicts
                        summon.SetId(mob.Id * 100 + uniqueThreshold + summonInfo.SummonedMobs.IndexOf(mobToAdd) + 1000);

                        // Position at the mob's location
                        summon.SetLocation((short)map.MapId, mob.CurrentLocation.X, mob.CurrentLocation.Y);

                        // Set mob's channel to match the client's
                        summon.MobChannel = client.Tamer.Channel;

                        // Initialize/reset the mob
                        summon.TamersViewing.Clear();
                        summon.Reset();
                        summon.SetRespawn();
                        summon.SetDuration();

                        //_logger.Information($"Adding reinforcement mob Type={summon.Type} at Map={map.MapId}, X={mob.CurrentLocation.X}, Y={mob.CurrentLocation.Y}, Channel={client.Tamer?.Channel}");

                        // Add to map
                        map.AddMob(summon);

                        // Track this mob type
                        spawnedMobTypes.Add(summon.Type);

                        ZDGZDEBUFF(summon, 0, true);

                        // Broadcast to nearby players
                        foreach (var nearbyClient in map.Clients)
                        {
                            if (nearbyClient == null || nearbyClient.Tamer == null || nearbyClient.Tamer.Location == null)
                            {
                                //_logger.Warning("Skipping null client or client with null Tamer/Location");
                                continue;
                            }

                            double distance = CalculateDistance(
                                nearbyClient.Tamer.Location.X, nearbyClient.Tamer.Location.Y,
                                summon.CurrentLocation.X, summon.CurrentLocation.Y);

                            if (distance <= _startToSee)
                            {
                                if (!summon.TamersViewing.Contains(nearbyClient.TamerId))
                                {
                                    summon.TamersViewing.Add(nearbyClient.TamerId);
                                    nearbyClient.Send(new Commons.Packets.MapServer.LoadMobsPacket(summon, true));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error processing individual summoned mob: {ex.Message}");
                    }
                }

                // _logger.Information($"Spawned {spawnedMobTypes.Count} mobs with types: {string.Join(", ", spawnedMobTypes)}");

            }
            catch (Exception ex)
            {
                _logger.Error($"Error in ZDGPhoenixMechanic: {ex.Message}\n{ex.StackTrace}");
            }
        }
        public int GetBirdramonSkill(IMob mob)
        {
            if (mob.Type != 51082)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 139; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 139; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 140; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 139; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetGarudamonSkill(IMob mob)
        {
            if (mob.Type != 51083)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                // _logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 144; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 143; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 142; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 141; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetPhoenixSkill(IMob mob)
        {
            if (mob.Type != 51084)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 148; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 147; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 146; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 145; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetZhuqiaomonSkill(IMob mob)
        {
            if (mob.Type != 51085)
                return 0; // Not Zhuqiaomon mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Zhuqiaomon mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Zhuqiaomon using skill 159 at 10% HP");
                return 159; // Ultimate skill at 10% HP
            }
            else if (hpPercentage <= 20)
            {
                //_logger.Information("Zhuqiaomon using skill 158 at 20% HP");
                return 158; // Skill at 20% HP
            }
            else if (hpPercentage <= 30)
            {
                //_logger.Information("Zhuqiaomon using skill 157 at 30% HP");
                return 157; // Skill at 30% HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Zhuqiaomon using skill 156 at 40% HP");
                return 156; // Skill at 40% HP
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Zhuqiaomon using skill 155 at 50% HP");
                return 155; // Skill at 50% HP
            }
            else if (hpPercentage <= 55)
            {
                //_logger.Information("Zhuqiaomon using skill 154 at 60% HP");
                return 154; // Skill at 60% HP
            }
            else if (hpPercentage <= 60)
            {
                //_logger.Information("Zhuqiaomon using skill 153 at 70% HP");
                return 153; // Skill at 70% HP
            }
            else if (hpPercentage <= 70)
            {
                //_logger.Information("Zhuqiaomon using skill 152 at 80% HP");
                return 152; // Skill at 80% HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Zhuqiaomon using skill 151 at 90% HP");
                return 151; // Skill at 90% HP
            }
            else if (hpPercentage <= 90)
            {
                //_logger.Information("Zhuqiaomon using skill 150 at 100% HP");
                return 150; // Basic skill at 100% HP
            }
            else if (hpPercentage <= 95)
            {
                //_logger.Information("Zhuqiaomon using skill 150 at 100% HP");
                return 150; // Basic skill at 100% HP
            }
            else
            {
                _logger.Information("Default Skill");
                return 0; // Default skill
            }
        }
        public void ZDGZhuqiaomonMechanic(IMob mob, long tamerId)
        {
            try
            {
                var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));
                if (map == null) return;

                // Debug logging to track execution
                //_logger.Information($"ZDGPhoenixMechanic called for mob {mob.Type} on map {map.MapId}");

                // Get client for message broadcasting
                var client = map.Clients.FirstOrDefault(gc => gc.TamerId == tamerId);

                if (client != null)
                {
                    //_logger.Information($"Mob {mob.Type} summoning reinforcements");

                    // SummonDTO ID for the reinforcement mobs
                    long summonDTOId = 8;

                    // Find summon info based on Summon ID
                    var summonInfo = _assets.SummonInfo.FirstOrDefault(x => x.Id == summonDTOId);
                    if (summonInfo == null)
                    {
                        //_logger.Error($"Invalid Summon ID: {summonDTOId} - Could not find SummonInfo");
                        return;
                    }

                    // Generate a unique threshold identifier for this spawn event
                    int uniqueThreshold = (int)(DateTime.Now.Ticks % 10000);

                    foreach (var mobToAdd in summonInfo.SummonedMobs ?? Enumerable.Empty<SummonMobModel>())
                    {
                        // Clone the mob to avoid modifying the original template
                        var summon = (SummonMobModel)mobToAdd.Clone();

                        // Set a unique ID based on current time to avoid ID conflicts
                        summon.SetId(mob.Id * 100 + uniqueThreshold + summonInfo.SummonedMobs.IndexOf(mobToAdd) + 1000);

                        ZDGZDEBUFF(summon, 0, true);

                        // Position at the mob's location
                        summon.SetLocation((short)map.MapId, mob.CurrentLocation.X, mob.CurrentLocation.Y);

                        // Set mob's channel to match the client's
                        summon.MobChannel = client.Tamer.Channel;

                        // Initialize/reset the mob
                        summon.TamersViewing.Clear();
                        summon.Reset();
                        summon.SetRespawn();
                        summon.SetDuration();

                        //_logger.Information($"Adding reinforcement mob Type={summon.Type} at Map={map.MapId}, X={mob.CurrentLocation.X}, Y={mob.CurrentLocation.Y}, Channel={client.Tamer?.Channel}");

                        // Add to map
                        map.AddMob(summon);

                        // Broadcast to nearby players
                        foreach (var nearbyClient in map.Clients)
                        {
                            double distance = CalculateDistance(
                                nearbyClient.Tamer.Location.X, nearbyClient.Tamer.Location.Y,
                                summon.CurrentLocation.X, summon.CurrentLocation.Y);

                            if (distance <= _startToSee)
                            {
                                if (!summon.TamersViewing.Contains(nearbyClient.TamerId))
                                {
                                    summon.TamersViewing.Add(nearbyClient.TamerId);
                                    nearbyClient.Send(new Commons.Packets.MapServer.LoadMobsPacket(summon, true));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in ZDGPhoenixMechanic: {ex.Message}\n{ex.StackTrace}");
            }
        }
        public void ZDGZDEBUFF(IMob mob, long tamerId, bool isSpawn)
        {
            try
            {
                //_logger.Information($"ZDGZDEBUFF called with mob type {mob.Type}, tamerId {tamerId}, isSpawn {isSpawn}");

                // Find the map that contains clients
                var map = Maps.FirstOrDefault(x => x.Clients.Any());
                if (map == null)
                {
                    //_logger.Warning($"ZDGZDEBUFF: No map with clients found");
                    return;
                }

                // When isSpawn is true, we apply the debuff normally using the mob's type
                if (isSpawn)
                {
                    int debuffId = GetDebuffIdForMobType(mob.Type);
                    if (debuffId == 0)
                    {
                        //_logger.Information($"ZDGZDEBUFF: Mob type {mob.Type} doesn't match any defined types for applying debuff");
                        return;
                    }

                    //_logger.Information($"ZDGZDEBUFF: Applying debuff {debuffId} to partners in range for mob type {mob.Type}");
                    ApplyDebuffToPartnersInRange(map, mob, debuffId);
                }
                else
                {
                    // When isSpawn is false (mob is killed), we need to check which mob type was killed
                    // and remove the corresponding debuff
                    int debuffId = GetDebuffIdForMobType(mob.Type);
                    if (debuffId == 0)
                    {
                        //_logger.Information($"ZDGZDEBUFF: Mob type {mob.Type} doesn't match any defined types for removing debuff");
                        return;
                    }

                    //_logger.Information($"ZDGZDEBUFF: Removing debuff {debuffId} from killing partner with tamerId {tamerId} for mob type {mob.Type}");

                    // Add more detailed logging to track the removal process
                    //_logger.Information($"Attempting to remove debuff {debuffId} associated with mob type {mob.Type}");

                    RemoveDebuffFromKillingPartner(map, tamerId, debuffId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in ZDGZDEBUFF: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private int GetDebuffIdForMobType(int mobType)
        {
            switch (mobType)
            {
                case 51023:
                    //_logger.Information($"GetDebuffIdForMobType: Mob type 51023 matched to debuff 60007");
                    return 60007;
                case 51061:
                    //_logger.Information($"GetDebuffIdForMobType: Mob type 51061 matched to debuff 60006");
                    return 60006;
                case 30483:
                    //_logger.Information($"GetDebuffIdForMobType: Mob type 30483 matched to debuff 60005");
                    return 60005;
                default:
                    //_logger.Information($"GetDebuffIdForMobType: No debuff match for mob type {mobType}");
                    return 0; // No matching debuff
            }
        }
        private void ApplyDebuffToPartnersInRange(GameMap map, IMob mob, int debuffId)
        {
            try
            {
                //_logger.Information($"Starting ApplyDebuffToPartnersInRange for mob type {mob.Type}, debuff ID {debuffId}");

                // Get the buff information from assets
                var buffInfo = _assets.BuffInfo.FirstOrDefault(b => b.BuffId == debuffId);
                if (buffInfo == null)
                {
                    //_logger.Warning($"Could not find buff with ID {debuffId} in assets");
                    return;
                }

                // Check if mob's CurrentLocation is null
                if (mob.CurrentLocation == null)
                {
                    //_logger.Warning($"Cannot apply debuff: mob CurrentLocation is null for mob type {mob.Type}");
                    return;
                }

                // Range for debuff application (1500 units as specified)
                int range = 1500;

                // Set debuff duration to 3 minutes (in milliseconds)
                int debuffDuration = 3 * 60 * 1000; // 3 minutes in milliseconds

                // Find all clients/partners in range
                int clientsInRange = 0;
                int debuffsApplied = 0;

                foreach (var client in map.Clients)
                {
                    // Skip if partner or partner's location is null
                    if (client.Partner == null || client.Partner.Location == null)
                    {
                        //_logger.Warning($"Skipping client: Partner or Partner.Location is null");
                        continue;
                    }

                    // Calculate distance from mob to partner
                    double distance = CalculateDistance(
                        mob.CurrentLocation.X, mob.CurrentLocation.Y,
                        client.Partner.Location.X, client.Partner.Location.Y);

                    // Check if partner is within range
                    if (distance <= range)
                    {
                        clientsInRange++;
                        //_logger.Information($"Partner of tamer {client.Tamer.Name} is in range (distance: {distance})");

                        // Check if partner already has this specific debuff
                        if (client.Partner.BuffList.Buffs.Any(x => x.BuffId == debuffId))
                        {
                            //_logger.Information($"Partner of tamer {client.Tamer.Name} already has debuff {debuffId} (will not reapply)");
                            continue;
                        }

                        //_logger.Information($"Applying debuff {debuffId} to partner of tamer {client.Tamer.Name} with 3-minute duration");

                        // Create new debuff for the partner using DigimonBuffModel.Create
                        var newDigimonBuff = DigimonBuffModel.Create(
                            buffInfo.BuffId,
                            buffInfo.SkillId);

                        // Set the buff info
                        newDigimonBuff.SetBuffInfo(buffInfo);

                        // Set the buff duration to 3 minutes
                        newDigimonBuff.SetDuration(debuffDuration);

                        // Make sure the buff has a proper end time
                        newDigimonBuff.SetEndDate(DateTime.Now.AddMilliseconds(debuffDuration));

                        //_logger.Information($"Set debuff {debuffId} duration to 3 minutes ({debuffDuration} ms)");

                        // Add debuff to partner's buff list
                        client.Partner.BuffList.Add(newDigimonBuff);
                        debuffsApplied++;

                        // Send the AddBuffPacket to notify about the new buff
                        // Important: Use buff level 1 to ensure the countdown is displayed
                        client.Send(new Commons.Packets.GameServer.AddBuffPacket(
                            client.Partner.GeneralHandler,
                            buffInfo,
                            (short)1,  // Set buff level to 1
                            debuffDuration).Serialize());

                        // Update client status
                        client.Send(new Commons.Packets.GameServer.UpdateStatusPacket(client.Tamer));

                        // Save changes to database
                        _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));
                    }
                }

                _logger.Information($"Applied {debuffsApplied} debuffs out of {clientsInRange} clients in range for debuff {debuffId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in ApplyDebuffToPartnersInRange: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void RemoveDebuffFromKillingPartner(GameMap map, long tamerId, int debuffId)
        {
            try
            {
                // Find the client who killed the mob
                var killerClient = map.Clients.FirstOrDefault(x => x.TamerId == tamerId);
                if (killerClient == null)
                {
                    //_logger.Warning($"Could not find client with ID {tamerId} for debuff removal");
                    return;
                }

                //_logger.Information($"Found killer client: {killerClient.Tamer.Name} (ID: {tamerId})");
                //_logger.Information($"Partner buff list contains {killerClient.Partner.BuffList.ActiveBuffs.Count} active buffs");

                // List all active buffs for debugging
                foreach (var buff in killerClient.Partner.BuffList.ActiveBuffs)
                {
                    //_logger.Information($"Active buff found: ID={buff.BuffId}, Name={buff.BuffInfo?.Name ?? "Unknown"}");
                }

                // Check if partner has the debuff
                var digimonBuff = killerClient.Partner.BuffList.ActiveBuffs
                    .FirstOrDefault(buff => buff.BuffId == debuffId);

                if (digimonBuff != null)
                {
                    string buffName = digimonBuff.BuffInfo?.Name ?? "Unknown";
                    string debuffType = GetDebuffType(debuffId);

                    //_logger.Information($"Removing debuff {debuffId} ({debuffType}) from partner of tamer {killerClient.Tamer.Name}");

                    // Remove the buff from buff list
                    killerClient.Partner.BuffList.Remove(debuffId);

                    // Send RemoveBuffPacket to client
                    killerClient.Send(new Commons.Packets.GameServer.RemoveBuffPacket(
                        killerClient.Partner.GeneralHandler,
                        debuffId).Serialize());

                    // Update client status
                    killerClient.Send(new Commons.Packets.GameServer.UpdateStatusPacket(killerClient.Tamer));

                    // Save updated buff list to database
                    _sender.Send(new UpdateDigimonBuffListCommand(killerClient.Partner.BuffList));

                    // Notify client with a system message based on debuff type
                    //string message = "";
                    switch (debuffType)
                    {
                        case "Silence":
                            //message = $"Your partner is no longer silenced and can use skills again!";
                            break;
                        case "Return (Fire)":
                            //message = $"Your partner is no longer affected by the fire damage reflection!";
                            break;
                        case "Degeneration":
                            //message = $"Your partner's attack power has been restored!";
                            break;
                        default:
                            //message = $"Your partner has been freed from {buffName}!";
                            break;
                    }
                }
                else
                {
                    _logger.Information($"Partner of tamer {killerClient.Tamer.Name} does not have debuff {debuffId}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in RemoveDebuffFromKillingPartner: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void DebuffTamersPartner(GameClient client, IMob mob, int duration, int skillId, int buffId)
        {
            try
            {
                if (client == null || client.Partner == null || mob == null)
                {
                    //_logger.Warning($"DebuffTamersPartner: Invalid parameters - client or mob is null");
                    return;
                }

                // Get the buff information from assets
                var buffInfo = _assets.BuffInfo.FirstOrDefault(b => b.BuffId == buffId);
                if (buffInfo == null)
                {
                    //_logger.Warning($"Could not find buff with ID {buffId} in assets");
                    return;
                }

                // Default duration is 5 minutes if not specified
                if (duration <= 0)
                {
                    duration = 5 * 60; // 5 minutes in seconds
                }

                // Duration in milliseconds
                int durationMs = duration * 1000;

                // Get the debuff type based on the buffId
                string debuffType = GetDebuffType(buffId);
                //_logger.Information($"Applying {debuffType} debuff from mob {mob.Type} to partner {client.Partner.Name} (Duration: {duration}s)");

                // Check if partner already has this specific debuff
                var existingBuff = client.Partner.BuffList.ActiveBuffs.FirstOrDefault(b => b.BuffId == buffId);
                if (existingBuff != null)
                {
                    //_logger.Information($"Partner {client.Partner.Name} already has {debuffType} debuff - updating duration");

                    // Update the duration of the existing buff
                    existingBuff.IncreaseEndDate(durationMs);

                    // Update client status
                    client.Send(new Commons.Packets.GameServer.UpdateStatusPacket(client.Tamer).Serialize());
                    return;
                }

                // Create new debuff for the partner
                var newDigimonBuff = DigimonBuffModel.Create(buffInfo.BuffId, skillId);
                newDigimonBuff.SetBuffInfo(buffInfo);
                newDigimonBuff.SetDuration(durationMs);
                newDigimonBuff.SetEndDate(DateTime.Now.AddMilliseconds(durationMs));

                // Store original stats before applying debuff if needed (for restoration when debuff expires)
                int originalAT = client.Partner.AT;

                // Apply specific debuff effects based on type
                switch (debuffType)
                {
                    case "Degeneration":
                        // Store the original AT value for later restoration
                        newDigimonBuff.SetTypeN(originalAT);

                        // Reduce partner's AT by 30%
                        int reducedAT = (int)(originalAT * 0.7); // 30% reduction

                        // Use reflection to temporarily modify AT value
                        var atField = client.Partner.GetType().GetField("_at",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (atField != null)
                        {
                            atField.SetValue(client.Partner, reducedAT);
                            //_logger.Information($"Reduced {client.Partner.Name}'s AT from {originalAT} to {reducedAT} (30% reduction)");
                        }
                        break;

                    case "Return (Fire)":

                    case "Silence":
                        break;
                }

                // Add debuff to partner's buff list
                client.Partner.BuffList.Add(newDigimonBuff);

                // Find the map for this client
                var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == client.TamerId));
                if (map == null)
                {
                    //_logger.Warning($"Could not find map for client {client.Tamer.Name} (ID: {client.TamerId})");
                    return;
                }

                // Send the AddBuffPacket to visualize the debuff
                map.BroadcastForTamerViewsAndSelf(client.TamerId, new Commons.Packets.GameServer.AddBuffPacket(
                    client.Partner.GeneralHandler,
                    buffInfo,
                    (short)1,  // Set buff level to 1
                    durationMs).Serialize());

                // Schedule cleanup when debuff expires
                Task.Delay(durationMs).ContinueWith(_ =>
                {
                    RemoveBuffPacket(client, buffId, debuffType, originalAT);
                });

                // Update client status
                client.Send(new Commons.Packets.GameServer.UpdateStatusPacket(client.Tamer).Serialize());

                // Save changes to database
                _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));

            }
            catch (Exception ex)
            {
                _logger.Error($"Error in DebuffTamersPartner: {ex.Message}\n{ex.StackTrace}");
            }
        }
        public void ProcessReturnFireDamageReflection(GameClient client, IMob targetMob, int damageDealt)
        {
            try
            {
                if (client?.Partner == null || targetMob == null || damageDealt <= 0)
                    return;

                // Check if partner has the Return (Fire) debuff
                bool hasReturnFireDebuff = client.Partner.BuffList.ActiveBuffs.Any(b => b.BuffId == 60006);
                if (!hasReturnFireDebuff)
                    return;

                // Check if the target monster has Fire element
                if (targetMob.Element != DigimonElementEnum.Fire)
                    return;

                // Calculate damage to reflect back to partner (50% of damage dealt)
                int reflectedDamage = (int)(damageDealt * 0.5);

                // Apply the reflected damage to the partner
                int remainingHp = client.Partner.ReceiveDamage(reflectedDamage);

                var map = Maps.FirstOrDefault(x => x.Clients.Exists(c => c.TamerId == client.TamerId));
                if (map != null)
                {
                    // Update HP for all viewers
                    map.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new Commons.Packets.GameServer.UpdateCurrentHPRatePacket(
                            client.Partner.GeneralHandler,
                            client.Partner.HpRate).Serialize());
                }

                // Check if partner died from reflected damage
                if (client.Partner.CurrentHp <= 0)
                {
                    //_logger.Information($"Partner {client.Partner.Name} died from Return (Fire) reflected damage");

                    // Handle partner death using the Die method
                    client.Partner.Die();

                    // Update client with death status
                    client.Send(new Commons.Packets.GameServer.UpdateStatusPacket(client.Tamer).Serialize());

                    // Send additional death packets
                    if (map != null)
                    {
                        // Send combat off packet
                        map.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new Commons.Packets.GameServer.Combat.SetCombatOffPacket(client.Partner.GeneralHandler).Serialize());

                        // Send specific death animation packet
                        map.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new Commons.Packets.GameServer.Combat.KillOnHitPacket(
                                targetMob.GeneralHandler,  // Attacker (the monster)
                                client.Partner.GeneralHandler,  // Target (the partner)
                                reflectedDamage,  // Damage
                                0  // Hit type (0 = normal)
                            ).Serialize());

                        // Update condition
                        map.BroadcastForTamerViewsAndSelf(client.TamerId,
                            new Commons.Packets.GameServer.SyncConditionPacket(
                                client.Partner.GeneralHandler,
                                client.Partner.CurrentCondition,
                                ""
                            ).Serialize());
                    }

                    // Reset combat state
                    client.Tamer.StopBattle();
                    client.Partner.StopAutoAttack();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing Return (Fire) damage reflection: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private string GetDebuffType(int buffId)
        {
            switch (buffId)
            {
                case 60005:
                    return "Degeneration";
                case 60006:
                    return "Return (Fire)";
                case 60007:
                    return "Silence";
                default:
                    return "Regular";
            }
        }
        private void RemoveBuffPacket(GameClient client, int buffId, string debuffType, int originalAT)
        {
            try
            {
                if (client == null || client.Partner == null)
                {
                    //_logger.Warning($"RemoveBuffPacket: Client or partner is null");
                    return;
                }

                //_logger.Information($"Removing {debuffType} debuff (ID: {buffId}) from {client.Partner.Name}");

                // Check if the buff is still active before attempting to remove it
                var buff = client.Partner.BuffList.ActiveBuffs.FirstOrDefault(b => b.BuffId == buffId);
                if (buff == null)
                {
                    //_logger.Information($"Buff {buffId} has already been removed from {client.Partner.Name}");
                    return;
                }

                // Remove the buff from partner's buff list
                client.Partner.BuffList.Remove(buffId);

                // Handle specific debuff type cleanup
                switch (debuffType)
                {
                    case "Degeneration":
                        // Restore original AT value
                        if (buff != null)
                        {
                            int storedOriginalAT = buff.TypeN;
                            var atField = client.Partner.GetType().GetField("_at",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (atField != null)
                            {
                                atField.SetValue(client.Partner, storedOriginalAT);
                                //_logger.Information($"Restored {client.Partner.Name}'s AT back to original value {storedOriginalAT}");
                            }
                        }
                        break;

                    case "Return (Fire)":
                        //_logger.Information($"Removed Return (Fire) debuff from {client.Partner.Name}");
                        break;

                    case "Silence":
                        //_logger.Information($"Removed Silence debuff from {client.Partner.Name} - skills can be used again");
                        break;
                }

                // Find the map for this client
                var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == client.TamerId));
                if (map != null)
                {
                    // Send RemoveBuffPacket to client
                    map.BroadcastForTamerViewsAndSelf(client.TamerId,
                        new Commons.Packets.GameServer.RemoveBuffPacket(
                            client.Partner.GeneralHandler,
                            buffId).Serialize());
                }

                // Update client status
                client.Send(new Commons.Packets.GameServer.UpdateStatusPacket(client.Tamer).Serialize());

                // Save changes to database
                _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in RemoveBuffPacket: {ex.Message}\n{ex.StackTrace}");
            }
        }
        public bool CanPartnerCastSkills(GameClient client)
        {
            if (client?.Partner == null)
                return false;

            // Check if partner has the Silence debuff (ID 60007)
            bool hasSilenceDebuff = client.Partner.BuffList.ActiveBuffs.Any(b => b.BuffId == 60007);

            // If the debuff is active, the partner cannot cast skills
            if (hasSilenceDebuff)
            {
                return false;
            }

            return true;
        }
        #endregion

        // ------------------------------------------------------------------------------
        // BDG Skill
        // ------------------------------------------------------------------------------
        #region BDG GET skill in order
        public int GetSinduramonRaidSkill(IMob mob)
        {
            if (mob.Type != 51090)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 174; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 170; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 169; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 168; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetBlossomonSkill(IMob mob)
        {
            if (mob.Type != 51092)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 194; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 191; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 188; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 186; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetBaihumonSkill(IMob mob)
        {
            if (mob.Type != 51094)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 207; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 205; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 203; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 199; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        #endregion

        // ------------------------------------------------------------------------------
        // QDG Skill
        // ------------------------------------------------------------------------------

        #region QDG GET skill in order & Mechanics

        public int GetAntylamonRaidSkill(IMob mob)
        {
            if (mob.Type != 51098)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 825; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 824; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 823; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 822; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetMihiramonRaidSkill(IMob mob)
        {
            if (mob.Type != 51099)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 830; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 829; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 833; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 829; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetMajiramonRaidSkill(IMob mob)
        {
            if (mob.Type != 51100)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 10)
            {
                //_logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 838; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 840; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 839; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 837; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public int GetQinglongmonRaSkill(IMob mob)
        {
            if (mob.Type != 51101)
                return 0; // Not Phoenix mob

            // Calculate HP percentage (0-100%)
            float hpPercentage = (mob.CurrentHpRate * 100f / 255f);
            //_logger.Information($"Phoenix mob at {hpPercentage:F1}% HP selecting appropriate skill");

            // Select skill based on HP thresholds
            if (hpPercentage <= 20)
            {
                //  _logger.Information("Phoenix using ultimate skill (148) at critical HP");
                return 853; // Ultimate skill at very low HP
            }
            else if (hpPercentage <= 40)
            {
                //_logger.Information("Phoenix using strong skill (147) at low HP");
                return 849; // Strong skill at low HP - should trigger minions
            }
            else if (hpPercentage <= 50)
            {
                //_logger.Information("Phoenix using medium skill (146) at medium HP");
                return 846; // Medium skill at medium HP
            }
            else if (hpPercentage <= 80)
            {
                //_logger.Information("Phoenix using basic skill (145) at high HP");
                return 845; // Basic skill at high HP
            }
            else
            {
                //_logger.Information("Phoenix HP above 80%, using default attack");
                return 0; // Default to basic attacks above 80% HP
            }
        }
        public void QinglongmonDungeonMechanic(IMob mob, long tamerId)
        {
            try
            {
                // Find the map containing the client with tamerId
                var map = Maps.FirstOrDefault(x => x.Clients.Exists(gameClient => gameClient.TamerId == tamerId));
                if (map == null)
                {
                    //_logger.Warning($"QinglongmonDungeonMechanic: No map found containing client with tamerId {tamerId}");
                    return;
                }

                // Get client for message broadcasting and channel information
                var client = map.Clients.FirstOrDefault(gc => gc.TamerId == tamerId);
                if (client == null)
                {
                    //_logger.Warning($"QinglongmonDungeonMechanic: No client found with tamerId {tamerId}");
                    return;
                }

                // Check if mob's CurrentLocation is null
                if (mob.CurrentLocation == null)
                {
                    //_logger.Warning($"QinglongmonDungeonMechanic: Mob's CurrentLocation is null for mob type {mob.Type}");
                    return;
                }

                _logger.Information($"Qinglongmon (Type: {mob.Type}) summoning reinforcements");

                // SummonDTO ID for the reinforcement mobs - fixed to 83 as requested
                long summonDTOId = 84;

                // Find summon info based on Summon ID
                var summonInfo = _assets.SummonInfo?.FirstOrDefault(x => x.Id == summonDTOId);
                if (summonInfo == null)
                {
                    //_logger.Error($"QinglongmonDungeonMechanic: Invalid Summon ID: {summonDTOId} - Could not find SummonInfo");
                    return;
                }

                // Check if SummonedMobs collection is null or empty
                if (summonInfo.SummonedMobs == null || !summonInfo.SummonedMobs.Any())
                {
                    //_logger.Error($"QinglongmonDungeonMechanic: summonInfo.SummonedMobs is null or empty for SummonID {summonDTOId}");
                    return;
                }

                // Generate a unique threshold identifier for this spawn event
                int uniqueThreshold = (int)(DateTime.Now.Ticks % 10000);

                // Spawn each mob defined in the summon configuration
                foreach (var mobToAdd in summonInfo.SummonedMobs)
                {
                    if (mobToAdd == null)
                    {
                        //_logger.Warning("QinglongmonDungeonMechanic: Skipping null mob in summonInfo.SummonedMobs");
                        continue;
                    }

                    try
                    {
                        // Clone the mob to avoid modifying the original template
                        var summon = (SummonMobModel)mobToAdd.Clone();

                        // Set a unique ID based on current time to avoid ID conflicts
                        summon.SetId(mob.Id * 100 + uniqueThreshold + summonInfo.SummonedMobs.IndexOf(mobToAdd) + 1000);

                        // Position at the mob's location
                        summon.SetLocation((short)map.MapId, mob.CurrentLocation.X, mob.CurrentLocation.Y);

                        // Set mob's channel to match the client's
                        summon.MobChannel = client.Tamer.Channel;

                        // Initialize/reset the mob
                        summon.TamersViewing.Clear();
                        summon.Reset();
                        summon.SetRespawn();
                        summon.SetDuration();

                        // _logger.Information($"Adding Qinglongmon reinforcement mob Type={summon.Type} at Map={map.MapId}, X={mob.CurrentLocation.X}, Y={mob.CurrentLocation.Y}, Channel={client.Tamer?.Channel}");

                        // Add to map
                        map.AddMob(summon);

                        // Broadcast to nearby players
                        foreach (var nearbyClient in map.Clients)
                        {
                            if (nearbyClient == null || nearbyClient.Tamer == null || nearbyClient.Tamer.Location == null)
                            {
                                continue;
                            }

                            double distance = CalculateDistance(
                                nearbyClient.Tamer.Location.X, nearbyClient.Tamer.Location.Y,
                                summon.CurrentLocation.X, summon.CurrentLocation.Y);

                            if (distance <= _startToSee)
                            {
                                if (!summon.TamersViewing.Contains(nearbyClient.TamerId))
                                {
                                    summon.TamersViewing.Add(nearbyClient.TamerId);
                                    nearbyClient.Send(new Commons.Packets.MapServer.LoadMobsPacket(summon, true));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error processing individual summoned mob in QinglongmonDungeonMechanic: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in QinglongmonDungeonMechanic: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        // ------------------------------------------------------------------------------
        // Royal Base
        // ------------------------------------------------------------------------------


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
                                ? $"{message}"
                                : $"[{local}] Dungeon Server [CH{tamer.Tamer.Channel}] {tamer.Tamer.Name}: {message}"
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
                // Send just the message without role mention
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