using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Config.Events;
using DigitalWorldOnline.Commons.Models.Map.Dungeons;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Models.TamerShop;
using DigitalWorldOnline.Commons.Utils;

namespace DigitalWorldOnline.Commons.Models.Map
{
    public sealed partial class GameMap : MapConfigModel
    {

        // Dynamic
        public byte Channel { get; set; }
        public int Channels { get; set; } // Config.Map tablosundaki Channels
        public int MaxChannels { get; set; } // Yeni property ekleyin
        public byte MaxPlayersPerChannel { get; set; }
        public DateTime WithoutTamers { get; private set; }
        public DateTime NextDatabaseOperation { get; private set; }
        public bool Initialized { get; private set; }
        public bool Operating { get; private set; }
        public bool UpdateMobs { get; private set; }
        public List<MobConfigModel> MobsToAdd { get; private set; }
        public List<MobConfigModel> MobsToRemove { get; private set; }
        public List<SummonMobModel> SummonMobs { get; private set; }
        public List<EventMobConfigModel> EventMobsToAdd { get; private set; }
        public List<EventMobConfigModel> EventMobsToRemove { get; private set; }
        public List<GameClient> Clients { get; private set; }
        public List<Drop> Drops { get; private set; }
        public List<ConsignedShop> ConsignedShops { get; private set; }
        public Dictionary<long, List<long>> TamersView { get; private set; }
        public Dictionary<long, List<long>> MobsView { get; private set; }
        public Dictionary<long, List<long>> DropsView { get; private set; }
        public Dictionary<long, List<long>> ConsignedShopView { get; private set; }
        public Dictionary<short, long> TamerHandlers { get; private set; }
        public Dictionary<short, long> DigimonHandlers { get; private set; }
        public Dictionary<short, long> MobHandlers { get; private set; }
        public Dictionary<short, long> DropHandlers { get; private set; }
        public List<int> ColiseumMobs = new();

        public object DropsLock { get; private set; }
        public object ClientsLock { get; private set; }
        public object DigimonHandlersLock { get; private set; }
        public object TamerHandlersLock { get; private set; }
        public bool IsRoyalBase { get; private set; }
        public RoyalBaseMap? RoyalBaseMap { get; private set; }
        public DateTime? CreationTime { get; private set; } = DateTime.Now;

        // --------------------------------------------------------------------------------

        public GameMap(short mapId, byte channel) : base(mapId, new List<MobConfigModel>())
        {
            Channel = channel;

            // Inicializa todos os locks
            DropsLock = new object();
            ClientsLock = new object();
            DigimonHandlersLock = new object();
            TamerHandlersLock = new object();

            // Inicializa todas as coleções
            Clients = new List<GameClient>();
            Drops = new List<Drop>();
            ConsignedShops = new List<ConsignedShop>();
            MobsToAdd = new List<MobConfigModel>();
            MobsToRemove = new List<MobConfigModel>();
            EventMobsToAdd = new List<EventMobConfigModel>();
            EventMobsToRemove = new List<EventMobConfigModel>();
            TamersView = new Dictionary<long, List<long>>();
            MobsView = new Dictionary<long, List<long>>();
            DropsView = new Dictionary<long, List<long>>();
            ConsignedShopView = new Dictionary<long, List<long>>();
            TamerHandlers = new Dictionary<short, long>();
            DigimonHandlers = new Dictionary<short, long>();
            MobHandlers = new Dictionary<short, long>();
            DropHandlers = new Dictionary<short, long>();
            ColiseumMobs = new List<int>();
            SummonMobs = new List<SummonMobModel>();

            // Inicializa outras propriedades de estado
            WithoutTamers = DateTime.MaxValue; // Ou DateTime.UtcNow
            NextDatabaseOperation = DateTime.Now.AddSeconds(30);
            Initialized = false; // Será inicializado pelo método Initialize()
            Operating = false;
            UpdateMobs = false;
            IsRoyalBase = false;
            MaxPlayersPerChannel = UtilitiesFunctions.MaxPlayersPerChannel;
        }

        public GameMap(short mapId, List<MobConfigModel> mobs, List<Drop> drops) : base(mapId, mobs)
        {
            Drops = drops;
            DropsLock = new object();
            ClientsLock = new object();
            DigimonHandlersLock = new object();
            TamerHandlersLock = new object();
            IsRoyalBase = false;
        }

        public GameMap(int mapId, List<EventMobConfigModel> mobs, List<Drop> drops) : base(mapId, mobs)
        {
            Drops = drops;
            DropsLock = new object();
            ClientsLock = new object();
            DigimonHandlersLock = new object();
            TamerHandlersLock = new object();
            IsRoyalBase = false;
        }

        public GameMap() : base()
        {
            DropsLock = new object();
            ClientsLock = new object();
            DigimonHandlersLock = new object();
            TamerHandlersLock = new object();

            Channel = 0;
            Clients = new List<GameClient>();
            Drops = new List<Drop>();
            ConsignedShops = new List<ConsignedShop>();

            MobsToAdd = new List<MobConfigModel>();
            MobsToRemove = new List<MobConfigModel>();
            EventMobsToAdd = new List<EventMobConfigModel>();
            EventMobsToRemove = new List<EventMobConfigModel>();
            TamersView = new Dictionary<long, List<long>>();
            MobsView = new Dictionary<long, List<long>>();
            DropsView = new Dictionary<long, List<long>>();
            ConsignedShopView = new Dictionary<long, List<long>>();
            TamerHandlers = new Dictionary<short, long>();
            DigimonHandlers = new Dictionary<short, long>();
            MobHandlers = new Dictionary<short, long>();
            DropHandlers = new Dictionary<short, long>();
            ColiseumMobs = new List<int>();
            SummonMobs = new List<SummonMobModel>();

            WithoutTamers = DateTime.MaxValue;
            NextDatabaseOperation = DateTime.Now.AddSeconds(30);
            IsRoyalBase = false;
        }
    }
}