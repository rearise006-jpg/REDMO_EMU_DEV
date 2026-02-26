using DigitalWorldOnline.Commons.Models.Assets.XML.MapObject;
using DigitalWorldOnline.Commons.Models.Assets.XML.InfiniteWar;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.DTOs.Assets;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Events;
using DigitalWorldOnline.Commons.Models.Summon;
using FluentValidation;
using AutoMapper;
using MediatR;
using Serilog;
using DigitalWorldOnline.Application.AssetsManager;

namespace DigitalWorldOnline.Application
{
    public class AssetsLoader
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private bool? _loading;
        private readonly object _lock = new object();
        private readonly object _reloadLock = new object();
        private DateTime _lastLoadTime = DateTime.MinValue;
        private readonly TimeSpan _reloadInterval = TimeSpan.FromMinutes(1);
        private readonly ILogger _logger;

        private readonly string _appBaseDirectory = AppContext.BaseDirectory;
        private readonly string _assetsFolderPath;
        private readonly string _mapObjectXmlPath;
        private readonly string _InfiniteWar_RankRewardItemsXmlPath;

        public bool Loading => _loading == null || _loading.Value;

        #region Database Lists - Thread-Safe Properties

        private IReadOnlyDictionary<int, ItemAssetModel> _itemInfo;
        public IReadOnlyDictionary<int, ItemAssetModel> ItemInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _itemInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _itemInfo = value;
                }
            }
        }

        private List<SummonModel> _summonInfo;
        public List<SummonModel> SummonInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _summonInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _summonInfo = value;
                }
            }
        }

        private List<SummonMobModel> _summonMobInfo;
        public List<SummonMobModel> SummonMobInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _summonMobInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _summonMobInfo = value;
                }
            }
        }

        private List<CharacterLevelStatusAssetModel> _tamerLevelInfo;
        public List<CharacterLevelStatusAssetModel> TamerLevelInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _tamerLevelInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _tamerLevelInfo = value;
                }
            }
        }

        private List<CharacterBaseStatusAssetModel> _tamerBaseInfo;
        public List<CharacterBaseStatusAssetModel> TamerBaseInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _tamerBaseInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _tamerBaseInfo = value;
                }
            }
        }

        private List<DigimonLevelStatusAssetModel> _digimonLevelInfo;
        public List<DigimonLevelStatusAssetModel> DigimonLevelInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _digimonLevelInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _digimonLevelInfo = value;
                }
            }
        }

        private List<DigimonBaseInfoAssetModel> _digimonBaseInfo;
        public List<DigimonBaseInfoAssetModel> DigimonBaseInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _digimonBaseInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _digimonBaseInfo = value;
                }
            }
        }

        private List<DigimonSkillAssetModel> _digimonSkillInfo;
        public List<DigimonSkillAssetModel> DigimonSkillInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _digimonSkillInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _digimonSkillInfo = value;
                }
            }
        }

        private IReadOnlyDictionary<int, MonsterSkillAssetModel> _monsterSkill;
        public IReadOnlyDictionary<int, MonsterSkillAssetModel> MonsterSkill
        {
            get
            {
                lock (_reloadLock)
                {
                    return _monsterSkill;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _monsterSkill = value;
                }
            }
        }

        private List<SkillCodeAssetModel> _skillCodeInfo;
        public List<SkillCodeAssetModel> SkillCodeInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _skillCodeInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _skillCodeInfo = value;
                }
            }
        }

        private List<SkillInfoAssetModel> _skillInfo;
        public List<SkillInfoAssetModel> SkillInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _skillInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _skillInfo = value;
                }
            }
        }

        private IReadOnlyDictionary<int, MonsterSkillInfoAssetModel> _monsterSkillInfo;
        public IReadOnlyDictionary<int, MonsterSkillInfoAssetModel> MonsterSkillInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _monsterSkillInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _monsterSkillInfo = value;
                }
            }
        }

        private List<MonthlyEventAssetModel> _monthlyEvents;
        public List<MonthlyEventAssetModel> MonthlyEvents
        {
            get
            {
                lock (_reloadLock)
                {
                    return _monthlyEvents;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _monthlyEvents = value;
                }
            }
        }

        private List<AchievementAssetModel> _achievementAssets;
        public List<AchievementAssetModel> AchievementAssets
        {
            get
            {
                lock (_reloadLock)
                {
                    return _achievementAssets;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _achievementAssets = value;
                }
            }
        }

        private List<SealDetailAssetModel> _sealInfo;
        public List<SealDetailAssetModel> SealInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _sealInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _sealInfo = value;
                }
            }
        }

        private List<EvolutionAssetModel> _evolutionInfo;
        public List<EvolutionAssetModel> EvolutionInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _evolutionInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _evolutionInfo = value;
                }
            }
        }

        private List<BuffInfoAssetModel> _buffInfo;
        public List<BuffInfoAssetModel> BuffInfo
        {
            get
            {
                lock (_reloadLock)
                {
                    return _buffInfo;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _buffInfo = value;
                }
            }
        }

        private List<ScanDetailAssetModel> _scanDetail;
        public List<ScanDetailAssetModel> ScanDetail
        {
            get
            {
                lock (_reloadLock)
                {
                    return _scanDetail;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _scanDetail = value;
                }
            }
        }

        private List<ContainerAssetModel> _container;
        public List<ContainerAssetModel> Container
        {
            get
            {
                lock (_reloadLock)
                {
                    return _container;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _container = value;
                }
            }
        }

        private List<StatusApplyAssetModel> _statusApply;
        public List<StatusApplyAssetModel> StatusApply
        {
            get
            {
                lock (_reloadLock)
                {
                    return _statusApply;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _statusApply = value;
                }
            }
        }

        private List<TitleStatusAssetModel> _titleStatus;
        public List<TitleStatusAssetModel> TitleStatus
        {
            get
            {
                lock (_reloadLock)
                {
                    return _titleStatus;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _titleStatus = value;
                }
            }
        }

        private List<AccessoryRollAssetModel> _accessoryRoll;
        public List<AccessoryRollAssetModel> AccessoryRoll
        {
            get
            {
                lock (_reloadLock)
                {
                    return _accessoryRoll;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _accessoryRoll = value;
                }
            }
        }

        private List<PortalAssetModel> _portal;
        public List<PortalAssetModel> Portal
        {
            get
            {
                lock (_reloadLock)
                {
                    return _portal;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _portal = value;
                }
            }
        }

        private List<MapRegionAssetModel> _mapRegion;
        public List<MapRegionAssetModel> MapRegion
        {
            get
            {
                lock (_reloadLock)
                {
                    return _mapRegion;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _mapRegion = value;
                }
            }
        }

        private List<HatchAssetModel> _hatchs;
        public List<HatchAssetModel> Hatchs
        {
            get
            {
                lock (_reloadLock)
                {
                    return _hatchs;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _hatchs = value;
                }
            }
        }

        private List<QuestAssetModel> _quest;
        public List<QuestAssetModel> Quest
        {
            get
            {
                lock (_reloadLock)
                {
                    return _quest;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _quest = value;
                }
            }
        }

        private List<int> _questItemList;
        public List<int> QuestItemList
        {
            get
            {
                lock (_reloadLock)
                {
                    return _questItemList;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _questItemList = value;
                }
            }
        }

        private List<short> _dailyQuestList;
        public List<short> DailyQuestList
        {
            get
            {
                lock (_reloadLock)
                {
                    return _dailyQuestList;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _dailyQuestList = value;
                }
            }
        }

        private List<MapAssetModel> _maps;
        public List<MapAssetModel> Maps
        {
            get
            {
                lock (_reloadLock)
                {
                    return _maps;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _maps = value;
                }
            }
        }

        private List<CloneAssetModel> _clones;
        public List<CloneAssetModel> Clones
        {
            get
            {
                lock (_reloadLock)
                {
                    return _clones;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _clones = value;
                }
            }
        }

        private List<CloneValueAssetModel> _cloneValues;
        public List<CloneValueAssetModel> CloneValues
        {
            get
            {
                lock (_reloadLock)
                {
                    return _cloneValues;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _cloneValues = value;
                }
            }
        }

        private List<TamerSkillModel> _tamerSkills;
        public List<TamerSkillModel> TamerSkills
        {
            get
            {
                lock (_reloadLock)
                {
                    return _tamerSkills;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _tamerSkills = value;
                }
            }
        }

        private List<NpcAssetModel> _npcs;
        public List<NpcAssetModel> Npcs
        {
            get
            {
                lock (_reloadLock)
                {
                    return _npcs;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _npcs = value;
                }
            }
        }

        private List<NpcColiseumAssetModel> _npcColiseum;
        public List<NpcColiseumAssetModel> NpcColiseum
        {
            get
            {
                lock (_reloadLock)
                {
                    return _npcColiseum;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _npcColiseum = value;
                }
            }
        }

        private List<ArenaRankingDailyItemRewardsModel> _arenaRankingDailyItemRewards;
        public List<ArenaRankingDailyItemRewardsModel> ArenaRankingDailyItemRewards
        {
            get
            {
                lock (_reloadLock)
                {
                    return _arenaRankingDailyItemRewards;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _arenaRankingDailyItemRewards = value;
                }
            }
        }

        private List<EvolutionArmorAssetModel> _evolutionsArmor;
        public List<EvolutionArmorAssetModel> EvolutionsArmor
        {
            get
            {
                lock (_reloadLock)
                {
                    return _evolutionsArmor;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _evolutionsArmor = value;
                }
            }
        }

        private List<ExtraEvolutionNpcAssetModel> _extraEvolutions;
        public List<ExtraEvolutionNpcAssetModel> ExtraEvolutions
        {
            get
            {
                lock (_reloadLock)
                {
                    return _extraEvolutions;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _extraEvolutions = value;
                }
            }
        }

        private List<CashShopAssetModel> _cashShopAssets;
        public List<CashShopAssetModel> CashShopAssets
        {
            get
            {
                lock (_reloadLock)
                {
                    return _cashShopAssets;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _cashShopAssets = value;
                }
            }
        }

        private List<TimeRewardAssetModel> _timeRewardAssets;
        public List<TimeRewardAssetModel> TimeRewardAssets
        {
            get
            {
                lock (_reloadLock)
                {
                    return _timeRewardAssets;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _timeRewardAssets = value;
                }
            }
        }

        private List<TimeRewardModel> _timeRewardEvents;
        public List<TimeRewardModel> TimeRewardEvents
        {
            get
            {
                lock (_reloadLock)
                {
                    return _timeRewardEvents;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _timeRewardEvents = value;
                }
            }
        }

        private List<GotchaAssetModel> _gotcha;
        public List<GotchaAssetModel> Gotcha
        {
            get
            {
                lock (_reloadLock)
                {
                    return _gotcha;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _gotcha = value;
                }
            }
        }

        private List<DeckBuffModel> _deckBuffs;
        public List<DeckBuffModel> DeckBuffs
        {
            get
            {
                lock (_reloadLock)
                {
                    return _deckBuffs;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _deckBuffs = value;
                }
            }
        }

        private List<DeckOptionModel> _encyclopediaDecks;
        public List<DeckOptionModel> EncyclopediaDecks
        {
            get
            {
                lock (_reloadLock)
                {
                    return _encyclopediaDecks;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _encyclopediaDecks = value;
                }
            }
        }

        #endregion

        #region XML Lists - Thread-Safe Properties

        private List<MapObjectAssetModel> _mapObjects;
        public List<MapObjectAssetModel> MapObjects
        {
            get
            {
                lock (_reloadLock)
                {
                    return _mapObjects;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _mapObjects = value;
                }
            }
        }

        private List<InfiniteWar_RankRewardItemsXmlModel> _infiniteWar_RankRewardItems;
        public List<InfiniteWar_RankRewardItemsXmlModel> InfiniteWar_RankRewardItems
        {
            get
            {
                lock (_reloadLock)
                {
                    return _infiniteWar_RankRewardItems;
                }
            }
            private set
            {
                lock (_reloadLock)
                {
                    _infiniteWar_RankRewardItems = value;
                }
            }
        }

        #endregion

        public AssetsLoader(ISender sender, IMapper mapper, ILogger logger)
        {
            _sender = sender;
            _mapper = mapper;
            _logger = logger;

            string folderPath = Path.GetFullPath(Path.Combine(_appBaseDirectory, "..\\..\\..\\..\\..\\..\\"));
            _assetsFolderPath = Path.Combine(folderPath, "Assets_xml");

            _mapObjectXmlPath = Path.Combine(_assetsFolderPath, "MapObject.xml");
            _InfiniteWar_RankRewardItemsXmlPath = Path.Combine(_assetsFolderPath, "InfiniteWar_RankRewardItems.xml");
        }

        public AssetsLoader Load()
        {
            lock (_lock)
            {
                if (_loading == null)
                {
                    _loading = true;
                    _lastLoadTime = DateTime.Now;

                    Task.Run(LoadAssets).ContinueWith(t =>
                    {
                        lock (_lock)
                        {
                            _loading = false;
                        }

                        if (t.IsFaulted)
                        {
                            _logger.Error($"Failed to load assets: {t.Exception?.Message}");
                        }
                    });
                }
            }
            return this;
        }

        public AssetsLoader Reload()
        {
            lock (_lock)
            {
                if (_loading == false)
                {
                    if (DateTime.Now - _lastLoadTime < _reloadInterval)
                    {
                        _logger.Information($"Reloading too soon. Please wait before reloading again.");
                        return this;
                    }

                    _loading = true;
                    _lastLoadTime = DateTime.Now;

                    Task.Run(LoadAssets).ContinueWith(t =>
                    {
                        _logger.Information($"Reloading all Assets.");

                        lock (_lock)
                        {
                            _loading = false;
                        }

                        if (t.IsFaulted)
                        {
                            _logger.Error($"Failed to reload assets: {t.Exception?.Message}");
                        }
                    });
                }
                else
                {
                    _logger.Warning("Assets are currently loading. Cannot reload at this time.");
                }
            }
            return this;
        }

        private async Task LoadAssets()
        {
            LogMessage(ConsoleColor.Yellow, "LOADING GAME ASSETS ...");

            _logger.Information("Loading database assets !!");
            try
            {
                try
                {
                    ItemInfo = ItemlistAssetManager.Instance.GetAllItems();

                    if (ItemInfo == null)
                    {
                        _logger.Error($"Failed to load ItemList XML");
                        ItemInfo = new Dictionary<int, ItemAssetModel>();
                    }
                    else
                    {
                        _logger.Information($"Loaded {ItemInfo.Count} items from XML");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading ItemList: {ex.Message}");
                    ItemInfo = new Dictionary<int, ItemAssetModel>();
                }

                SummonInfo = _mapper.Map<List<SummonModel>>(await _sender.Send(new SummonAssetsQuery()));
                SummonMobInfo = _mapper.Map<List<SummonMobModel>>(await _sender.Send(new SummonMobAssetsQuery()));
                SkillCodeInfo = _mapper.Map<List<SkillCodeAssetModel>>(await _sender.Send(new SkillCodeAssetsQuery()));
                TamerLevelInfo = _mapper.Map<List<CharacterLevelStatusAssetModel>>(await _sender.Send(new TamerLevelingAssetsQuery()));
                TamerBaseInfo = _mapper.Map<List<CharacterBaseStatusAssetModel>>(await _sender.Send(new TamerBaseStatusAssetsQuery()));
                DigimonLevelInfo = _mapper.Map<List<DigimonLevelStatusAssetModel>>(await _sender.Send(new DigimonLevelingAssetsQuery()));
                DigimonBaseInfo = _mapper.Map<List<DigimonBaseInfoAssetModel>>(await _sender.Send(new AllDigimonBaseInfoQuery()));

                try
                {
                    SkillInfo = SkillAssetManager.Instance.GetAllItems();
                    if (SkillInfo == null)
                    {
                        _logger.Error("Failed to load SkillInfo");
                        SkillInfo = new List<SkillInfoAssetModel>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading SkillInfo: {ex.Message}");
                    SkillInfo = new List<SkillInfoAssetModel>();
                }

                DigimonSkillInfo = _mapper.Map<List<DigimonSkillAssetModel>>(await _sender.Send(new DigimonSkillAssetsQuery()));

                try
                {
                    MonsterSkillInfo = MonsterSkillInfoAssetManager.Instance.GetAllItems();
                    MonsterSkill = MonsterSkillInfoAssetManager.Instance.GetAllMonsterSkillsAsDictionary();
                    _logger.Information("Monster skill and info loaded from XML successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading MonsterSkill: {ex.Message}");
                    MonsterSkillInfo = new Dictionary<int, MonsterSkillInfoAssetModel>();
                    MonsterSkill = new Dictionary<int, MonsterSkillAssetModel>();
                }

                SealInfo = _mapper.Map<List<SealDetailAssetModel>>(await _sender.Send(new SealStatusAssetsQuery()));
                EvolutionInfo = _mapper.Map<List<EvolutionAssetModel>>(await _sender.Send(new DigimonEvolutionAssetsQuery()));

                try
                {
                    BuffInfo = BuffInfoAssetManager.Instance.GetAllBuffs();
                    if (BuffInfo == null)
                    {
                        _logger.Error("Failed to load BuffInfo");
                        BuffInfo = new List<BuffInfoAssetModel>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading BuffInfo: {ex.Message}");
                    BuffInfo = new List<BuffInfoAssetModel>();
                }

                ScanDetail = _mapper.Map<List<ScanDetailAssetModel>>(await _sender.Send(new ScanDetailAssetQuery()));
                Container = _mapper.Map<List<ContainerAssetModel>>(await _sender.Send(new ContainerAssetQuery()));
                StatusApply = _mapper.Map<List<StatusApplyAssetModel>>(await _sender.Send(new StatusApplyAssetQuery()));
                TitleStatus = _mapper.Map<List<TitleStatusAssetModel>>(await _sender.Send(new AllTitleStatusAssetsQuery()));
                AccessoryRoll = _mapper.Map<List<AccessoryRollAssetModel>>(await _sender.Send(new AccessoryRollAssetsQuery()));
                Portal = _mapper.Map<List<PortalAssetModel>>(await _sender.Send(new PortalAssetsQuery()));

                try
                {
                    MapRegion = MapRegionAssetManager.Instance.GetAllRegions();
                    if (MapRegion == null)
                    {
                        _logger.Error("Failed to load MapRegion");
                        MapRegion = new List<MapRegionAssetModel>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading MapRegion: {ex.Message}");
                    MapRegion = new List<MapRegionAssetModel>();
                }

                Npcs = _mapper.Map<List<NpcAssetModel>>(await _sender.Send(new NpcAssetsQuery()));
                NpcColiseum = _mapper.Map<List<NpcColiseumAssetModel>>(await _sender.Send(new NpcColiseumAssetsQuery()));
                Quest = _mapper.Map<List<QuestAssetModel>>(await _sender.Send(new QuestAssetsQuery()));
                Hatchs = _mapper.Map<List<HatchAssetModel>>(await _sender.Send(new HatchAssetsQuery()));
                Maps = _mapper.Map<List<MapAssetModel>>(await _sender.Send(new MapAssetsQuery()));
                Clones = _mapper.Map<List<CloneAssetModel>>(await _sender.Send(new CloneAssetsQuery()));
                CloneValues = _mapper.Map<List<CloneValueAssetModel>>(await _sender.Send(new CloneValueAssetsQuery()));

                try
                {
                    TamerSkills = TamerSkillAssetManager.Instance.GetAllSkills();
                    if (TamerSkills == null)
                    {
                        _logger.Error("Failed to load TamerSkills");
                        TamerSkills = new List<TamerSkillModel>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading TamerSkills: {ex.Message}");
                    TamerSkills = new List<TamerSkillModel>();
                }

                MonthlyEvents = _mapper.Map<List<MonthlyEventAssetModel>>(await _sender.Send(new MonthlyEventAssetsQuery()));

                try
                {
                    EncyclopediaDecks = DeckOptionAssetManager.Instance.GetAllItems();
                    if (EncyclopediaDecks == null)
                    {
                        _logger.Error("Failed to load EncyclopediaDecks");
                        EncyclopediaDecks = new List<DeckOptionModel>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error loading EncyclopediaDecks: {ex.Message}");
                    EncyclopediaDecks = new List<DeckOptionModel>();
                }

                AchievementAssets = _mapper.Map<List<AchievementAssetModel>>(await _sender.Send(new AchievementAssetsQuery()));
                ArenaRankingDailyItemRewards = _mapper.Map<List<ArenaRankingDailyItemRewardsModel>>(await _sender.Send(new ArenaRankingDailyItemRewardsQuery()));
                EvolutionsArmor = _mapper.Map<List<EvolutionArmorAssetModel>>(await _sender.Send(new EvolutionArmorAssetsQuery()));
                ExtraEvolutions = _mapper.Map<List<ExtraEvolutionNpcAssetModel>>(await _sender.Send(new ExtraEvolutionNpcAssetQuery()));
                CashShopAssets = _mapper.Map<List<CashShopAssetModel>>(await _sender.Send(new CashShopAssetsQuery()));
                TimeRewardAssets = _mapper.Map<List<TimeRewardAssetModel>>(await _sender.Send(new TimeRewardAssetsQuery()));
                TimeRewardEvents = _mapper.Map<List<TimeRewardModel>>(await _sender.Send(new TimeRewardEventsQuery()));
                DeckBuffs = _mapper.Map<List<DeckBuffModel>>(await _sender.Send(new DeckBuffAssetsQuery()));
                Gotcha = _mapper.Map<List<GotchaAssetModel>>(await _sender.Send(new GotchaAssetsQuery()));
            }
            catch (Exception ex)
            {
                _logger.Error("Error on Loading Assets: {Message}", ex.Message);
                _logger.Error("Stack Trace: {StackTrace}", ex.StackTrace);
            }

            _logger.Information("Loading additional information !!");

            try
            {
                var itemInfoList = ItemInfo?.Values.ToList() ?? new List<ItemAssetModel>();
                itemInfoList.ForEach(item =>
                {
                    var skillCode = SkillCodeInfo?.FirstOrDefault(x => x.SkillCode == item.SkillCode);
                    item.SetSkillInfo(skillCode);
                });

                BuffInfo?.ForEach(buff =>
                {
                    var skillInfo = SkillInfo?.FirstOrDefault(x => x.SkillId == buff.SkillCode || x.SkillId == buff.DigimonSkillCode);
                    buff.SetSkillInfo(skillInfo);
                });

                DigimonSkillInfo?.ForEach(skill =>
                {
                    var skillInfo = SkillInfo?.FirstOrDefault(x => x.SkillId == skill.SkillId);
                    skill.SetSkillInfo(skillInfo);
                });

                if (MonsterSkill != null && MonsterSkillInfo != null)
                {
                    foreach (var skill in MonsterSkill.Values)
                    {
                        if (MonsterSkillInfo.TryGetValue(skill.SkillId, out var skillInfo))
                        {
                            skill.SetSkillInfo(skillInfo);
                        }
                    }
                }

                if (SealInfo != null)
                {
                    SealInfo = SealInfo.OrderByDescending(x => x.RequiredAmount).ToList();
                }

                if (ItemInfo != null)
                {
                    QuestItemList = ItemInfo.Values
                        .Where(x => x.Type == 80 || x.Type == 85)
                        .Select(x => x.ItemId)
                        .ToList();
                }
                else
                {
                    QuestItemList = new List<int>();
                }

                if (Quest != null)
                {
                    DailyQuestList = Quest
                        .Where(x => x.QuestType == QuestTypeEnum.DailyQuest)
                        .Select(x => (short)x.QuestId)
                        .ToList();
                }
                else
                {
                    DailyQuestList = new List<short>();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error on Loading additional Assets: {Message}", ex.Message);
                _logger.Error("Stack Trace: {StackTrace}", ex.StackTrace);
            }

            _logger.Information("Loading XML assets !!");

            try
            {
                // MapObjects
                var mapObjectAssetWrapper = MapObjectReader.LoadFromXml(_mapObjectXmlPath);

                if (mapObjectAssetWrapper != null && mapObjectAssetWrapper.MapObjects != null)
                {
                    MapObjects = mapObjectAssetWrapper.MapObjects;
                    _logger.Information("MapObject XML loaded !!");
                }
                else
                {
                    MapObjects = new List<MapObjectAssetModel>();
                    _logger.Error($"Can't load MapObject XML: {_mapObjectXmlPath}");
                }

                // InfiniteWar
                var infiniteWar_RankRewardItemsXmlWrapper = InfiniteWar_RankRewardItemsReader.LoadFromXml(_InfiniteWar_RankRewardItemsXmlPath);

                if (infiniteWar_RankRewardItemsXmlWrapper != null && infiniteWar_RankRewardItemsXmlWrapper.InfiniteWar_RankRewardItems != null)
                {
                    InfiniteWar_RankRewardItems = infiniteWar_RankRewardItemsXmlWrapper.InfiniteWar_RankRewardItems;
                    _logger.Information("InfiniteWar_RankRewardItems.xml XML loaded !!");
                }
                else
                {
                    InfiniteWar_RankRewardItems = new List<InfiniteWar_RankRewardItemsXmlModel>();
                    _logger.Error($"Can't load InfiniteWar_RankRewardItems.xml: {_InfiniteWar_RankRewardItemsXmlPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error on Loading xml Assets: {Message}", ex.Message);
                _logger.Error("Stack Trace: {StackTrace}", ex.StackTrace);
            }

            LogMessage(ConsoleColor.Yellow, "ASSETS LOADED !!");
        }

        /// <summary>
        /// Safe method to get item info with null checking
        /// </summary>
        public ItemAssetModel GetItemInfo(int itemId)
        {
            lock (_reloadLock)
            {
                if (ItemInfo != null && ItemInfo.TryGetValue(itemId, out var item))
                {
                    return item;
                }
                return null;
            }
        }

        /// <summary>
        /// Check if an item ID is valid
        /// </summary>
        public bool IsValidItem(int itemId)
        {
            lock (_reloadLock)
            {
                return ItemInfo != null && ItemInfo.ContainsKey(itemId);
            }
        }

        private void LogMessage(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"|----------------------------------------------------|");
            Console.WriteLine($"|---------  {message.ToUpper()}");
            Console.WriteLine($"|----------------------------------------------------|");
            Console.ResetColor();
        }
    }
}