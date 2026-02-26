using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost.EventsServer;
using DigitalWorldOnline.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Collections.Concurrent;

namespace DigitalWorldOnline.GameHost
{
    public sealed partial class MapServer : IMapServer
    {
        private readonly PartyManager _partyManager;
        private readonly StatusManager _statusManager;
        private readonly ExpManager _expManager;
        private readonly DropManager _dropManager;
        private readonly EventManager _eventManager;

        private readonly AssetsLoader _assets;
        private readonly ConfigsLoader _configs;

        private readonly DungeonsServer _dungeonServer;
        private readonly  EventServer _eventServer;
        private readonly PvpServer _pvpServer;
        private readonly MapServer _mapServer;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly ConcurrentDictionary<(int mapId, byte channel, ushort handler), CharacterModel> _tamerIndex = new();
        private readonly ConcurrentDictionary<(int mapId, byte channel, int handler), MobConfigModel> _mobIndex = new();
        private readonly ConcurrentDictionary<(int mapId, byte channel, int handler), SummonMobModel> _mobIndexSummon = new();

        private readonly ConcurrentDictionary<long, GameClient> _clientByTamerId = new();
        private readonly ConcurrentDictionary<string, GameClient> _clientByTamerName = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<int, GameClient> _clientByTamerHandle = new();

        private readonly object _mapsLock = new object();

        public List<GameMap> Maps { get; set; }

        public MapServer(PartyManager partyManager, StatusManager statusManager, ExpManager expManager, DropManager dropManager,
            AssetsLoader assets, ConfigsLoader configs, ILogger logger, ISender sender, IMapper mapper, IServiceProvider serviceProvider,
            EventManager eventManager, DungeonsServer dungeonServer, IConfiguration configuration)
        {
            _partyManager = partyManager;
            _statusManager = statusManager;
            _expManager = expManager;
            _dropManager = dropManager;
            _assets = assets;
            _configs = configs.Load();
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
            _serviceProvider = serviceProvider;
            _eventManager = eventManager;
            _dungeonServer = dungeonServer;
            _mapServer = this;
            _configuration = configuration;

            Maps = new List<GameMap>();
        }

        // ----------------------------------------------------------------------------------

        public IDictionary<byte, byte> GetLiveChannelsAndPlayerCountsForMap(int mapId)
        {
            // 📌 Doğru field adını kullan
            var liveChannels = new Dictionary<byte, byte>();
            var mapInstances = Maps.Where(m => m.MapId == mapId).ToList();

            // Tüm aktif map instance'larını döngüle
            foreach (var gameMapInstance in mapInstances)
            {
                liveChannels.Add(gameMapInstance.Channel, (byte)gameMapInstance.Clients.Count);
            }

            // 📌 En yüksek channel ID'yi bul
            int maxChannelId = mapInstances.Any() ? mapInstances.Max(x => x.Channel) : 2;

            // 📌 Eksik kanalları 0 oyuncu ile ekle
            for (byte i = 0; i <= maxChannelId; i++)
            {
                if (!liveChannels.ContainsKey(i))
                {
                    liveChannels.Add(i, 0);
                }
            }

            return liveChannels;
        }

        // ----------------------------------------------------------------------------------

        private void SaveMobToDatabase(MobConfigModel mob)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var mobDto = dbContext.MobConfig.SingleOrDefault(m => m.Id == mob.Id);

                if (mobDto == null)
                {
                    _logger.Error($"BOSS {mob.Name},{mob.Id} Does not exist in the database Unable to call MobConfig.");
                    return;
                }

                mobDto.DeathTime = mob.DeathTime;
                mobDto.ResurrectionTime = mob.ResurrectionTime;

                try
                {
                    dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    _logger.Error($"BOSS time Update error： {mob.Name} (Id: {mob.Id}): {ex.Message}");
                }
            }
        }

    }
}