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
using MediatR;
using Serilog;
using System.Collections.Concurrent;

namespace DigitalWorldOnline.GameHost
{
    public sealed partial class DungeonsServer : IMapServer
    {
        private readonly PartyManager _partyManager;
        private readonly StatusManager _statusManager;
        private readonly ExpManager _expManager;
        private readonly DropManager _dropManager;

        private readonly AssetsLoader _assets;
        private readonly ConfigsLoader _configs;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        private readonly ConcurrentDictionary<(int mapId, byte channel, ushort handler), CharacterModel> _tamerIndex = new();
        private readonly ConcurrentDictionary<(int mapId, byte channel, int handler), MobConfigModel> _mobIndex = new();
        private readonly ConcurrentDictionary<(int mapId, byte channel, int handler), SummonMobModel> _mobIndexSummon = new();

        private readonly ConcurrentDictionary<long, GameClient> _clientByTamerId = new();
        private readonly ConcurrentDictionary<string, GameClient> _clientByTamerName = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<int, GameClient> _clientByTamerHandle = new();

        public List<GameMap> Maps { get; set; }

        public DungeonsServer(PartyManager partyManager, StatusManager statusManager, ExpManager expManager, DropManager dropManager,
           AssetsLoader assets, ConfigsLoader configs, ILogger logger, ISender sender, IMapper mapper)
        {
            _partyManager = partyManager;
            _statusManager = statusManager;
            _expManager = expManager;
            _dropManager = dropManager;
            _assets = assets.Load();
            _configs = configs.Load();
            _logger = logger;
            _sender = sender;
            _mapper = mapper;

            Maps = new List<GameMap>();
        }

        // ----------------------------------------------------------------------------------

        public IDictionary<byte, byte> GetLiveChannelsAndPlayerCountsForMap(int mapId)
        {
            var liveChannels = new Dictionary<byte, byte>();
            var mapInstances = Maps.Where(m => m.MapId == mapId).ToList();

            foreach (var gameMapInstance in Maps.Where(m => m.MapId == mapId))
            {
                liveChannels.Add(gameMapInstance.Channel, (byte)gameMapInstance.Clients.Count);
            }

            // 📌 En yüksek channel ID'yi bul
            int maxChannelId = mapInstances.Any() ? mapInstances.Max(x => x.Channel) : 0;

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
    }
}