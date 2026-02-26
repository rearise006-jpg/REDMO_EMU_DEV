using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Models.Config.Events;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Logger;

namespace DigitalWorldOnline.Commons.Models.Character
{
    public sealed partial class CharacterModel
    {
        //Temp
        public bool TempRecalculate { get; set; }
        public bool TempCalculating { get; set; }
        public DateTime TempUpdating { get; set; } = DateTime.Now;

        private static readonly Random _random = new();

        private int _baseMs => BaseStatus.MSValue + LevelingStatus.MSValue;
        private int _baseHp => LevelingStatus?.HPValue ?? 0;
        private int _baseDs => LevelingStatus?.DSValue ?? 0;
        private int _baseAt => LevelingStatus?.ATValue ?? 0;
        private int _baseDe => LevelingStatus?.DEValue ?? 0;
        private int _baseExp => 1;

        private int _handlerValue;
        private int _targetHandler;
        private int _currentHP = 0;
        private int _currentDS = 0;

        public bool PvpMap = false;

        /// <summary>
        /// Current character health points.
        /// </summary>
        public int CurrentHp
        {
            get => Math.Clamp(_currentHP, 0, HP);
            private set => _currentHP = value;
        }

        public int CurrentDs
        {
            get => Math.Clamp(_currentDS, 0, DS);
            private set => _currentDS = value;
        }

        public byte HpRate => HP > 0 ? (byte)(CurrentHp * 255 / HP) : (byte)0;

        public bool CanMissHit()
        {
            if (TargetIMob == null)
                return true;

            //if (TargetMob == null)
            //    return true;

            bool hasDebuff = TargetIMob.DebuffList.ActiveBuffs.Any(buff =>
                buff.BuffInfo.SkillInfo.Apply.Any(apply =>
                    apply.Attribute == SkillCodeApplyAttributeEnum.CrowdControl || !buff.DebuffExpired));

            if (hasDebuff)
                return false;

            double targetEvasion = TargetIMob.EVValue;
            double attackerHitRate = Partner.HT;

            int levelDifference = Partner.Level - TargetIMob.Level;

            if (attackerHitRate > targetEvasion || levelDifference > 15)
                return false;

            if (levelDifference <= 20 && Partner.Level >= TargetIMob.Level)
                return false;

            double attributeAdvantage = (Partner.BaseInfo.Attribute.HasAttributeAdvantage(TargetIMob.Attribute) ||
                                         Partner.BaseInfo.Element.HasElementAdvantage(TargetIMob.Element)) ? 1.5 : 1;

            double hitChance = attackerHitRate / (targetEvasion * attributeAdvantage);

            return _random.NextDouble() > hitChance;
        }

        // Otimização das funções e propriedades do CharacterModelBehavior

        public bool CanMissHit(bool summon)
        {
            if (TargetSummonMob == null)
                return true;

            var rand = Random.Shared;
            double evasion = TargetSummonMob.EVValue;
            double hitRate = Partner.HT;
            int levelDiff = Partner.Level - TargetSummonMob.Level;

            if (hitRate > evasion || levelDiff > 15)
                return false;

            if (levelDiff <= 20)
            {
                if (Partner.Level >= TargetSummonMob.Level)
                    return false;

                double advantage = 1.5;

                if (Partner.BaseInfo.Attribute.HasAttributeAdvantage(TargetMob.Attribute) ||
                    Partner.BaseInfo.Element.HasElementAdvantage(TargetMob.Element))
                    advantage = 2.0;

                if (TargetMob.Attribute.HasAttributeAdvantage(Partner.BaseInfo.Attribute) ||
                    TargetMob.Element.HasElementAdvantage(Partner.BaseInfo.Element))
                    advantage = 1.0;

                var chance = CalcularProbabilidadeAcerto(hitRate, Partner.Level, TargetSummonMob.Level, evasion, advantage);
                chance = Math.Clamp(chance, 0, 100);

                return chance <= 10 || rand.Next(1, 101) >= (int)chance;
            }

            return true;
        }

        public static double CalcularProbabilidadeAcerto(double hit, int lvl, int mobLvl, double ev, double adv)
        {
            var levelDiff = lvl - mobLvl;
            var multiplier = 1 / (1 + Math.Exp(-levelDiff / 9.0));
            return multiplier * (hit / ev) * adv * 100;
        }

        public bool NoThreats => !TargetMobs.Any(x => x.TargetTamer?.GeneralHandler == GeneralHandler);
        public bool Riding => CurrentCondition == ConditionEnum.Ride;
        public IMob? TargetIMob => TargetMob as IMob ?? TargetSummonMob as IMob ?? TargetIMobs.FirstOrDefault(x => x.GeneralHandler == _targetHandler);
        public MobConfigModel? TargetMob => TargetMobs.FirstOrDefault(x => x.GeneralHandler == _targetHandler);
        public SummonMobModel? TargetSummonMob => TargetSummonMobs.FirstOrDefault(x => x.GeneralHandler == _targetHandler);
        public EventMobConfigModel? TargetEventMob => TargetEventMobs.FirstOrDefault(x => x.GeneralHandler == _targetHandler);
        public DigimonModel? TargetPartner => TargetPartners.FirstOrDefault(x => x.GeneralHandler == _targetHandler);

        public bool HasAura => Aura.ItemId > 0 &&
                               (Aura.ItemInfo?.UseTimeType == 0 ||
                                (Aura.ItemInfo?.UseTimeType > 0 && Aura.RemainingMinutes() > 0 ||
                                 Aura.RemainingMinutes() != 0xFFFFFFFF));

        public ItemModel Aura => Equipment.Items[10];
        public ItemListModel Equipment => ItemList.First(x => x.Type == ItemListEnum.Equipment);
        public ItemListModel TamerSkill => ItemList.First(x => x.Type == ItemListEnum.TamerSkill);
        public ItemListModel Inventory => ItemList.First(x => x.Type == ItemListEnum.Inventory);
        public ItemListModel Warehouse => ItemList.First(x => x.Type == ItemListEnum.Warehouse);
        public ItemListModel ChipSets => ItemList.First(x => x.Type == ItemListEnum.Chipsets);
        public ItemListModel JogressChipSet => ItemList.First(x => x.Type == ItemListEnum.JogressChipset);
        public ItemListModel Digivice => ItemList.First(x => x.Type == ItemListEnum.Digivice);
        public ItemListModel RewardWarehouse => ItemList.First(x => x.Type == ItemListEnum.RewardWarehouse);
        public ItemListModel GiftWarehouse => ItemList.First(x => x.Type == ItemListEnum.GiftWarehouse);
        public ItemListModel ConsignedWarehouse => ItemList.First(x => x.Type == ItemListEnum.ConsignedWarehouse);
        public ItemListModel AccountWarehouse => ItemList.FirstOrDefault(x => x.Type == ItemListEnum.AccountWarehouse);
        public ItemListModel AccountShopWarehouse => ItemList.FirstOrDefault(x => x.Type == ItemListEnum.ShopWarehouse);
        public ItemListModel AccountBuyHistory => ItemList.FirstOrDefault(x => x.Type == ItemListEnum.BuyHistory);
        public ItemListModel AccountCashWarehouse => ItemList.FirstOrDefault(x => x.Type == ItemListEnum.CashWarehouse);
        public ItemListModel TamerShop => ItemList.First(x => x.Type == ItemListEnum.TamerShop);
        public ItemListModel ConsignedShopItems => ItemList.First(x => x.Type == ItemListEnum.ConsignedShop);

        public DigimonModel Partner => Digimons.OrderBy(x => x.Slot).First();
        public List<DigimonModel> ActiveDigimons => Digimons.Where(x => x.Id != Partner.Id).ToList();

        public void SwitchPartner(byte targetIndex)
        {
            var current = Digimons.First(x => x.Slot == 0);
            var target = Digimons.First(x => x.Slot == targetIndex);
            current.SetSlot(targetIndex);
            target.SetSlot(0);
        }

        public short ProperMS => (short)MS;

        public short AppearenceHandler => BitConverter.ToInt16(new byte[] { (byte)(_handlerValue >> 32), 0 }, 0);
        public ushort GeneralHandler => BitConverter.ToUInt16(new byte[] { (byte)(_handlerValue >> 32), 128 }, 0);

        public bool Alive => Partner.CurrentHp > 0;
        public bool SaveResourcesTime => DateTime.Now >= LastSaveResources;
        public bool SyncResourcesTime => DateTime.Now >= LastSyncResources;
        public bool DebuffTime => DateTime.Now >= LastDebuffUpdate;
        public bool ResetDailyQuestsTime => DateTime.Now.Date > LastDailyQuestCheck.Date;
        public bool CheckBuffsTime => DateTime.Now >= LastBuffsCheck;
        public bool CheckExpiredItemsTime => DateTime.Now >= LastExpiredItemsCheck;

        public bool HaveActiveCashSkill => ActiveSkill.Any(x => x.Type == TamerSkillTypeEnum.Cash || x.SkillId > 0);
        public bool IsSpecialMapActive { get; set; }

        public bool BreakEvolution => IsSpecialMapActive ||
                                     (ActiveEvolution.DsPerSecond > 0 && CurrentDs == 0) ||
                                     (ActiveEvolution.XgPerSecond > 0 && XGauge == 0);

        public bool HasXai => Xai?.ItemId > 0;
        public static int Fatigue => 0;

        public bool SetCombatOff { get; set; }

        private int ProperModel => (Model.GetHashCode() - CharacterModelEnum.MarcusDamon.GetHashCode()) * 128 + 10240160 << 8;

        // Otimizações das propriedades de status e métodos de controle de batalha

        public int HP => _baseHp
            + EquipmentAttribute(_baseHp, SkillCodeApplyAttributeEnum.MaxHP)
            + BuffAttribute(_baseHp, SkillCodeApplyAttributeEnum.MaxHP)
            + SocketAttribute(AccessoryStatusTypeEnum.HP, _baseHp)
            + DigiviceAccessoryStatus(AccessoryStatusTypeEnum.HP, _baseHp)
            + ChipsetStatus(_baseHp, SkillCodeApplyAttributeEnum.MaxHP);

        public int DS => _baseDs
            + EquipmentAttribute(_baseDs, SkillCodeApplyAttributeEnum.MaxDS)
            + BuffAttribute(_baseDs, SkillCodeApplyAttributeEnum.MaxDS)
            + SocketAttribute(AccessoryStatusTypeEnum.DS, _baseDs)
            + DigiviceAccessoryStatus(AccessoryStatusTypeEnum.DS, _baseDs)
            + ChipsetStatus(_baseDs, SkillCodeApplyAttributeEnum.MaxDS);

        public short AT => (short)(_baseAt
            + EquipmentAttribute(_baseAt, SkillCodeApplyAttributeEnum.AT, SkillCodeApplyAttributeEnum.DA)
            + BuffAttribute(_baseAt, SkillCodeApplyAttributeEnum.AT, SkillCodeApplyAttributeEnum.DA)
            + SocketAttribute(AccessoryStatusTypeEnum.AT, _baseAt)
            + DigiviceAccessoryStatus(AccessoryStatusTypeEnum.AT, _baseAt)
            + ChipsetStatus(_baseAt, SkillCodeApplyAttributeEnum.AT, SkillCodeApplyAttributeEnum.DA));

        public short DE => (short)(_baseDe
            + EquipmentAttribute(_baseDe, SkillCodeApplyAttributeEnum.DP)
            + BuffAttribute(_baseDe, SkillCodeApplyAttributeEnum.DP)
            + SocketAttribute(AccessoryStatusTypeEnum.DE, _baseDe)
            + DigiviceAccessoryStatus(AccessoryStatusTypeEnum.DE, _baseDe)
            + ChipsetStatus(_baseDe, SkillCodeApplyAttributeEnum.DP));

        public int MS => Math.Min(_baseMs
            + EquipmentAttribute(_baseMs, SkillCodeApplyAttributeEnum.MS,
                SkillCodeApplyAttributeEnum.MovementSpeedComparisonCorrectionBuff,
                SkillCodeApplyAttributeEnum.MovementSpeedIncrease)
            + BuffAttribute(_baseMs, SkillCodeApplyAttributeEnum.MS,
                SkillCodeApplyAttributeEnum.MovementSpeedComparisonCorrectionBuff,
                SkillCodeApplyAttributeEnum.MovementSpeedIncrease)
            + SocketAttribute(AccessoryStatusTypeEnum.MS, _baseMs),
            15000);

        public int KillExp => _baseExp
            + EquipmentAttribute(_baseExp, SkillCodeApplyAttributeEnum.EXP)
            + BuffAttribute(_baseExp, SkillCodeApplyAttributeEnum.EXP);

        public short BonusEXP => (short)(
            EquipmentAttribute(0, SkillCodeApplyAttributeEnum.EXP)
            + BuffAttribute(0, SkillCodeApplyAttributeEnum.EXP));

        private void SetBaseData()
        {
            StartTimers();
            CurrentCondition = ConditionEnum.Default;

            Location = CharacterLocationModel.Create(
                (short)GeneralSizeEnum.StartMapLocation,
                (short)GeneralSizeEnum.StartX,
                (short)GeneralSizeEnum.StartY);

            Level = 1;
            CurrentHp = 100;
            CurrentDs = 100;
            Size = (short)GeneralSizeEnum.StarterDigimonSize;

            State = CharacterStateEnum.Disconnected;
            EventState = CharacterEventStateEnum.None;

            DigimonSlots = (byte)GeneralSizeEnum.MinActiveDigimonList;

            for (int i = 0; i < 192; i++)
                MapRegions.Add(new CharacterMapRegionModel());
        }

        private void StartTimers()
        {
            var now = DateTime.Now;
            LastSyncResources = now.AddSeconds(10);
            LastDailyQuestCheck = now.AddSeconds(60);
            LastBuffsCheck = now.AddSeconds(10);
            LastFatigueUpdate = now.AddSeconds(30);
            LastRegenUpdate = now.AddSeconds(30);
            LastActiveEvolutionUpdate = now.AddSeconds(30);
            LastMovementUpdate = now.AddSeconds(30);
            LastSaveResources = now.AddSeconds(30);
            TimeRewardUpdate = now.AddSeconds(30);
            LastExpiredItemsCheck = now.AddSeconds(30);
            LastExpiredBuffsCheck = now.AddSeconds(30);
            EventQueueInfoTime = now;
            LastAfkNotification = now;
        }

        public bool SetAfk => CurrentCondition != ConditionEnum.Away && AfkNotifications >= 8;

        public void AddAfkNotifications(byte notifications)
        {
            AfkNotifications += notifications;
            LastAfkNotification = DateTime.Now;
        }

        public void AddPoints(int points)
        {
            if (DailyPoints.InsertDate.Day == DateTime.Now.Day)
                DailyPoints.AddPoints(points);
            else
                DailyPoints = new CharacterArenaDailyPointsModel(DateTime.Now, points, Id);
        }

        public void SetSize(short value) => Size = value;
        public void AddRepurchaseItem(ItemModel soldItem) => RepurchaseList.Add(soldItem);
        public void UpdateEventQueueInfoTime(int seconds = 30) => EventQueueInfoTime = DateTime.Now.AddSeconds(seconds);

        public static CharacterModel Create(long accountId, string name, int model, byte position, long serverId)
        {
            var character = new CharacterModel()
            {
                AccountId = accountId,
                Name = name,
                Model = (CharacterModelEnum)model,
                Position = position,
                ServerId = serverId
            };

            character.SetBaseData();
            return character;
        }

        // ------------------------------------------------------------------------------

        // Otimização completa dos métodos de batalha, alvo e experiência do Tamer

        private void AddIfNotExists<T>(List<T> list, T item, Func<T, bool> predicate)
        {
            if (!list.Any(predicate))
                list.Add(item);
        }

        public void StartBattle(MobConfigModel mob)
        {
            if (mob == null) return;

            AddIfNotExists(TargetMobs, mob, x => x.Id == mob.Id);
            SetBattleHandler(mob.GeneralHandler);

            UpdateCombatInteractionTime();
        }

        public void StartBattle(IMob mob)
        {
            if (mob == null) return;

            AddIfNotExists(TargetIMobs, mob, x => x.Id == mob.Id);
            SetBattleHandler(mob.GeneralHandler);
            UpdateCombatInteractionTime();
        }

        public void StartBattle(SummonMobModel mob)
        {
            if (mob == null) return;
            //TargetSummonMobs ??= new();
            AddIfNotExists(TargetSummonMobs, mob, x => x.Id == mob.Id);
            SetBattleHandler(mob.GeneralHandler);

            UpdateCombatInteractionTime();
        }

        public void StartBattle(EventMobConfigModel mob)
        {
            TargetEventMobs ??= new();
            AddIfNotExists(TargetEventMobs, mob, x => x.Id == mob.Id);
            SetBattleHandler(mob.GeneralHandler);
        }

        public void StartBattle(DigimonModel enemy)
        {
            AddIfNotExists(TargetPartners, enemy, x => x.Id == enemy.Id);
            SetBattleHandler(enemy.GeneralHandler);
        }

        private void SetBattleHandler(int handler)
        {
            if (!InBattle)
            {
                _targetHandler = handler;
                InBattle = true;
            }
        }

        //public void StopBattle() => ClearBattle(TargetMobs);
        public void StopBattle(bool summon) => ClearBattle(TargetSummonMobs);
        //public void StopIBattle() => ClearBattle(TargetIMobs);
        public void StopBattleDigimon() => ClearBattle(TargetPartners);

        private void ClearBattle<T>(List<T> list)
        {
            _targetHandler = 0;
            list.Clear();
            InBattle = false;
        }

        public void StopBattle()
        {
            _targetHandler = 0;
            TargetMobs.Clear();
            TargetIMobs.Clear();
            TargetSummonMobs.Clear();
            TargetPartners.Clear();
            InBattle = false;
            LastCombatInteractionTime = DateTime.MinValue;
        }

        public void UpdateTarget(MobConfigModel mobConfig)
        {
            if (!TargetMobs.Any(x => x.Id == mobConfig.Id))
                TargetMobs.Add(mobConfig);

            _targetHandler = mobConfig.GeneralHandler;

            UpdateCombatInteractionTime();
        }
        public void UpdateTarget(IMob mobConfig)
        {
            if (!TargetIMobs.Any(x => x.Id == mobConfig.Id))
                TargetIMobs.Add(mobConfig);

            _targetHandler = mobConfig.GeneralHandler;
        }
        public void UpdateTarget(SummonMobModel mobConfig)
        {
            if (!TargetSummonMobs.Any(x => x.Id == mobConfig.Id))
                TargetSummonMobs.Add(mobConfig);

            _targetHandler = mobConfig.GeneralHandler;
        }

        public void UpdateTarget(DigimonModel enemyPartner)
        {
            if (!TargetPartners.Any(x => x.Id == enemyPartner.Id))
                TargetPartners.Add(enemyPartner);

            _targetHandler = enemyPartner.GeneralHandler;
        }

        // Nova funçao para atualizar combate de mob
        public void UpdateCombatInteractionTime()
        {
            LastCombatInteractionTime = DateTime.Now;

            if (!InBattle)
            {
                InBattle = true;
            }
        }

        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Updates the character target (mob / Summon) when use skill.
        /// </summary>
        public void UpdateTargetWithSkill(List<MobConfigModel> mobs, SkillTypeEnum skillType)
        {
            mobs.ForEach(mob =>
            {
                if (!TargetMobs.Any(x => x.Id == mob.Id))
                    TargetMobs.Add(mob);
            });

            switch (skillType)
            {
                case SkillTypeEnum.Single:
                case SkillTypeEnum.TargetArea:
                    _targetHandler = mobs.First().GeneralHandler;
                    break;
            }
        }

        public void UpdateTargetWithSkill(List<IMob> mobs, SkillTypeEnum skillType)
        {
            mobs.ForEach(mob =>
            {
                if (!TargetIMobs.Any(x => x.Id == mob.Id))
                    TargetIMobs.Add(mob);
            });

            switch (skillType)
            {
                case SkillTypeEnum.Single:
                case SkillTypeEnum.TargetArea:
                    _targetHandler = mobs.First().GeneralHandler;
                    break;
            }
        }
        public void StartBattleWithSkill(List<IMob> mobs, SkillTypeEnum skillType)
        {
            mobs.ForEach(mob =>
            {
                if (!TargetIMobs.Any(x => x.Id == mob.Id))
                    TargetIMobs.Add(mob);
            });

            switch (skillType)
            {
                case SkillTypeEnum.Single:
                case SkillTypeEnum.TargetArea:
                    _targetHandler = mobs.First().GeneralHandler;
                    break;
            }

            InBattle = true;
        }

        public void UpdateTargetWithSkill(List<SummonMobModel> mobs, SkillTypeEnum skillType)
        {
            mobs.ForEach(mob =>
            {
                if (!TargetSummonMobs.Any(x => x.Id == mob.Id))
                    TargetSummonMobs.Add(mob);
            });

            switch (skillType)
            {
                case SkillTypeEnum.Single:
                case SkillTypeEnum.TargetArea:
                    _targetHandler = mobs.First().GeneralHandler;
                    break;
            }
        }

        public void StartRideMode()
        {
            PreviousCondition = CurrentCondition;
            CurrentCondition = ConditionEnum.Ride;

            Partner.StartRideMode();

            ResetAfkNotifications();
        }
        public void StopRideMode()
        {
            PreviousCondition = CurrentCondition;
            CurrentCondition = ConditionEnum.Default;

            Partner.StopRideMode();

            ResetAfkNotifications();
        }
        public void StartBattleWithSkill(List<MobConfigModel> mobs, SkillTypeEnum skillType)
        {
            mobs.ForEach(mob =>
            {
                if (!TargetMobs.Any(x => x.Id == mob.Id))
                    TargetMobs.Add(mob);
            });

            switch (skillType)
            {
                case SkillTypeEnum.Single:
                case SkillTypeEnum.TargetArea:
                    _targetHandler = mobs.First().GeneralHandler;
                    break;
            }

            InBattle = true;
        }

        public void StartBattleWithSkill(List<SummonMobModel> mobs, SkillTypeEnum skillType)
        {
            if (TargetSummonMobs == null)
                TargetSummonMobs = new List<SummonMobModel>();

            mobs.ForEach(mob =>
            {
                if (!TargetSummonMobs.Any(x => x.Id == mob.Id))
                    TargetSummonMobs.Add(mob);
            });

            switch (skillType)
            {
                case SkillTypeEnum.Single:
                case SkillTypeEnum.TargetArea:
                    _targetHandler = mobs.First().GeneralHandler;
                    break;
            }

            InBattle = true;
        }

        public void RemoveTarget(MobConfigModel mobConfig)
        {
            if (_targetHandler == mobConfig.GeneralHandler)
                _targetHandler = 0;

            TargetMobs.RemoveAll(x => x.Id == mobConfig.Id);

            if (!TargetMobs.Any())
                InBattle = false;
        }

        public void RemoveTarget(SummonMobModel mobConfig)
        {
            if (TargetSummonMobs == null)
                TargetSummonMobs = new List<SummonMobModel>();

            if (_targetHandler == mobConfig.GeneralHandler)
                _targetHandler = 0;

            TargetSummonMobs.RemoveAll(x => x.Id == mobConfig.Id);

            if (!TargetSummonMobs.Any())
                InBattle = false;
        }

        public void RemoveTarget(DigimonModel mobConfig)
        {
            if (_targetHandler == mobConfig.GeneralHandler)
                _targetHandler = 0;

            TargetPartners.RemoveAll(x => x.Id == mobConfig.Id);

            if (!TargetPartners.Any())
                InBattle = false;
        }



        public void ReceiveExp(long value) => CurrentExperience += value;

        public void LevelUp(byte levels = 1)
        {
            if (Level + levels <= (int)GeneralSizeEnum.TamerLevelMax)
            {
                Level += levels;
                CurrentExperience = 0;
                FullHeal();
            }
        }

        public void SetLevel(byte level) => Level = level;

        public void LooseExp(long value, bool decreaseLevel = false)
        {
            CurrentExperience = Math.Max(CurrentExperience - value, 0);
            if (decreaseLevel && Level >= 2) Level--;
        }

        public void SetExp(long value) => CurrentExperience = value;

        public int EquipmentAttribute(int baseValue, params SkillCodeApplyAttributeEnum[] attributes)
        {
            int total = 0;

            foreach (var item in Equipment.EquippedItems)
            {
                if (item.ItemInfo?.SkillInfo == null || item.RemainingMinutes() == 0xFFFFFFFF) continue;

                foreach (var apply in item.ItemInfo.SkillInfo.Apply)
                {
                    if (!attributes.Contains(apply.Attribute)) continue;

                    total += apply.Type switch
                    {
                        SkillCodeApplyTypeEnum.Default => apply.Value,
                        SkillCodeApplyTypeEnum.Unknown105 => apply.Value,
                        SkillCodeApplyTypeEnum.Percent => (int)(baseValue * (decimal)apply.Value / 100),
                        SkillCodeApplyTypeEnum.AlsoPercent => apply.Attribute == SkillCodeApplyAttributeEnum.EXP
                            ? apply.Value * item.ItemInfo.TypeN
                            : (int)(baseValue * 0.10),
                        _ => 0
                    };
                }
            }

            return total;
        }

        /// <summary>
        /// Returns the hit rate value from equipment with correct type handling.
        /// </summary>
        /// <param name="baseValue">Base HT value.</param>
        public int EquipmentAttributeHT(int baseValue)
        {
            int total = 0;

            foreach (var item in Equipment.EquippedItems)
            {
                if (item.ItemInfo?.SkillInfo == null || item.RemainingMinutes() == 0xFFFFFFFF)
                    continue;

                foreach (var apply in item.ItemInfo.SkillInfo.Apply)
                {
                    // Check if this is a HT attribute
                    if (apply.Attribute == SkillCodeApplyAttributeEnum.HT)
                    {
                        switch (apply.Type)
                        {
                            case SkillCodeApplyTypeEnum.Default:
                                // For HT, add the direct value
                                total += apply.Value;
                                //   Console.WriteLine($"Item {item.ItemId} adds {apply.Value} HT (Default)");
                                break;
                            case SkillCodeApplyTypeEnum.Percent:
                                // For percentage-based HT buffs
                                int percentValue = (int)(baseValue * (decimal)apply.Value / 100);
                                total += percentValue;
                                //  Console.WriteLine($"Item {item.ItemId} adds {percentValue} HT ({apply.Value}% of {baseValue})");
                                break;
                            case SkillCodeApplyTypeEnum.AlsoPercent:
                                // For AlsoPercent type
                                total += apply.Value;
                                //   Console.WriteLine($"Item {item.ItemId} adds {apply.Value} HT (AlsoPercent)");
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            //  Console.WriteLine($"Total HT from equipment: {total}");
            return total;
        }

        /// <summary>
        /// Returns the skill cooldown reduction value from equipment with proper handling for AlsoPercent type.
        /// </summary>
        /// <param name="baseValue">Base SCD value.</param>
        /// <summary>
        /// Returns the skill cooldown reduction value from equipment with proper handling for AlsoPercent type.
        /// </summary>
        /// <param name="baseValue">Base SCD value.</param>
        public int EquipmentAttributeSCD(int baseValue)
        {
            int total = 0;
            //Console.WriteLine($"[DEBUG] EquipmentAttributeSCD called for {Name}, checking {Equipment?.EquippedItems?.Count ?? 0} items");

            if (Equipment?.EquippedItems == null)
                return 0;

            foreach (var item in Equipment.EquippedItems)
            {
                if (item == null || item.ItemInfo?.SkillInfo == null || item.RemainingMinutes() == 0xFFFFFFFF)
                    continue;

                // Console.WriteLine($"[DEBUG] Checking item {item.ItemId} ({item.ItemInfo?.Name ?? "Unknown"})");

                foreach (var apply in item.ItemInfo.SkillInfo.Apply)
                {
                    //Console.WriteLine($"[DEBUG] Attribute: {apply.Attribute}, Type: {apply.Type}, Value: {apply.Value}");

                    // Check both SCD (proper) and HT (incorrectly assigned in some items but intended to be SCD)
                    // This ensures that items with either attribute contribute to skill cooldown reduction
                    if (apply.Attribute == SkillCodeApplyAttributeEnum.SCD ||
                        (apply.Attribute == SkillCodeApplyAttributeEnum.HT && item.ItemInfo.Name.Contains("SCD")))
                    {
                        int valueToAdd = 0;

                        switch (apply.Type)
                        {
                            case SkillCodeApplyTypeEnum.Default:
                                valueToAdd = apply.Value;
                                break;
                            case SkillCodeApplyTypeEnum.Percent:
                                valueToAdd = (int)(baseValue * (decimal)apply.Value / 100);
                                break;
                            case SkillCodeApplyTypeEnum.AlsoPercent:
                                valueToAdd = apply.Value * Math.Max(1, item.ItemInfo.TypeN);
                                break;
                            case SkillCodeApplyTypeEnum.Unknown105:
                                valueToAdd = apply.Value;
                                break;
                        }

                        total += valueToAdd;
                        // Console.WriteLine($"[DEBUG] Added {valueToAdd} to SCD from {item.ItemId} (Attribute: {apply.Attribute}), running total: {total}");
                    }
                }
            }

            //Console.WriteLine($"[DEBUG] Final SCD from equipment: {total}");
            return total;
        }

        public int SocketAttribute(AccessoryStatusTypeEnum type, int baseValue)
        {
            int totalValue = 0;

            foreach (var item in Equipment.EquippedItems)
            {
                if (item.ItemInfo == null || item.ItemInfo.SkillInfo == null || item.RemainingMinutes() == 0xFFFFFFFF)
                    continue;

                var socketList = item.SocketStatus.Where(socket => socket.Value > 0).ToList();
                var accList = item.AccessoryStatus.Where(acc => acc.Value > 0).ToList();

                if (socketList.Any())
                {
                    bool summedAccValues = false;

                    foreach (var socket in socketList)
                    {
                        if (type == socket.Type)
                        {
                            var applyAtt = (double)item.ItemInfo.ApplyElement / 100;

                            if (socket.Type != AccessoryStatusTypeEnum.MS)
                            {
                                if (!summedAccValues)
                                {
                                    foreach (var accValue in accList)
                                    {
                                        var socketValue = (int)(accValue.Value * applyAtt);

                                        totalValue += socketValue;
                                    }

                                    summedAccValues = true;
                                }
                            }
                            else
                            {
                                foreach (var accValue in accList)
                                {
                                    var socketValue = (int)(accValue.Value * applyAtt);

                                    double socketValuePercent = (double)socketValue / 100;

                                    double socketMS = baseValue * socketValuePercent;

                                    totalValue += (int)socketMS;
                                }
                            }
                        }
                    }
                }
            }

            return totalValue;
        }

        /// <summary>
        /// Returns the target  chipset status value.
        /// </summary>
        /// <param name="type">Target status type.</param>
        public int ChipsetStatus(int baseValue, params SkillCodeApplyAttributeEnum[] attributes)
        {
            int totalValue = 0;

            foreach (var item in ChipSets.EquippedItems)
            {
                if (item.ItemInfo == null || item.ItemInfo.SkillInfo == null)
                    continue;

                if (!IsSameFamily(item))
                    continue;

                foreach (var apply in item.ItemInfo.SkillInfo.Apply)
                {
                    if (!attributes.Any(x => x == apply.Attribute))
                        continue;

                    switch (apply.Type)
                    {
                        case SkillCodeApplyTypeEnum.Default:
                            totalValue += apply.Value;
                            break;

                        case SkillCodeApplyTypeEnum.Unknown105:
                        case SkillCodeApplyTypeEnum.Percent:
                            totalValue += (int)(baseValue * (decimal)apply.Value / 100);
                            break;

                        case SkillCodeApplyTypeEnum.AlsoPercent:
                            if (apply.Attribute == SkillCodeApplyAttributeEnum.EXP)
                                totalValue += (apply.Value * item.ItemInfo.TypeN);
                            else
                                totalValue += (int)(baseValue * 0.10);
                            break;
                    }
                }
            }

            return totalValue;
        }


        public bool IsSameFamily(ItemModel item)
        {
            if ((DigimonFamilyEnum)item.FamilyType == Partner.BaseInfo.Family1 ||
                (DigimonFamilyEnum)item.FamilyType == Partner.BaseInfo.Family2 ||
                (DigimonFamilyEnum)item.FamilyType == Partner.BaseInfo.Family3)
            {
                return true;
            }

            if ((DigimonFamilyEnum)item.FamilyType == DigimonFamilyEnum.All)
            {
                return true;
            }

            return false;
        }

        
        public int DigiviceAccessoryStatus(AccessoryStatusTypeEnum type, int baseValue)
        {
            int totalValue = 0;

            foreach (var item in Digivice.EquippedItems)
            {
                if (!item.HasAccessoryStatus ||
                    Level < item.ItemInfo.TamerMinLevel ||
                    Partner.Level < item.ItemInfo.DigimonMinLevel)
                    continue;

                foreach (var status in item.AccessoryStatus)
                {
                    if (status.Type != type)
                        continue;

                    decimal percent = item.HasSocketStatus ? 0 : (decimal)item.Power / 100;
                    int added = 0;

                    // removed Element and Attribute checks for Data/Vaccine/Virus/Unknown types
                    // from this block
                    if (type == AccessoryStatusTypeEnum.AS)
                    {
                        decimal pct = (decimal)status.Value / 100;
                        added = (int)((percent * pct * baseValue) / 100);
                    }
                    else if (type == AccessoryStatusTypeEnum.CT ||
                            type == AccessoryStatusTypeEnum.EV ||
                            type == AccessoryStatusTypeEnum.ATT)
                    {
                        added = (int)(status.Value * percent * 100);
                    }
                    else if (type == AccessoryStatusTypeEnum.CD)
                    {
                        added = (int)((status.Value / 100m) * item.Power);
                    }
                    else
                    {
                        added = (int)(percent * status.Value);
                    }

                    totalValue += added;
                }
            }

            return totalValue;
        }


        
        public static (int attributeBonus, int elementBonus) GetDigiviceAttributeAndElementBonus(GameClient client)
        {
            int attrBonus = 0;
            int elemBonus = 0;

            if (client?.Tamer?.Digivice?.EquippedItems == null)
                return (0, 0);

            var partnerAttr = client.Tamer.Partner.BaseInfo.Attribute;
            var partnerElem = client.Tamer.Partner.BaseInfo.Element;

            foreach (var item in client.Tamer.Digivice.EquippedItems)
            {
                foreach (var status in item.AccessoryStatus)
                {
                    decimal percent = item.HasSocketStatus ? 0 : item.Power / 100m;

                    // Atributos
                    if (status.Type is AccessoryStatusTypeEnum.Data
                        or AccessoryStatusTypeEnum.Vacina
                        or AccessoryStatusTypeEnum.Virus
                        or AccessoryStatusTypeEnum.Unknown)
                    {
                        if (partnerAttr == UtilitiesFunctions.AccessoryStatusTypeEnumToAttribute(status.Type))
                            attrBonus += (int)(percent * (status.Value / 100m));
                    }

                    // Elementos
                    if (status.Type is AccessoryStatusTypeEnum.Fire
                        or AccessoryStatusTypeEnum.Water
                        or AccessoryStatusTypeEnum.Wood
                        or AccessoryStatusTypeEnum.Thunder
                        or AccessoryStatusTypeEnum.Earth
                        or AccessoryStatusTypeEnum.Wind
                        or AccessoryStatusTypeEnum.Light
                        or AccessoryStatusTypeEnum.Dark
                        or AccessoryStatusTypeEnum.Ice
                        or AccessoryStatusTypeEnum.Steel)
                    {
                        if (partnerElem == UtilitiesFunctions.AccessoryStatusTypeEnumToElement(status.Type))
                            elemBonus += (int)(percent * (status.Value / 100m));
                    }
                }
            }

            return (attrBonus, elemBonus);
        }




       

        public int AccessoryStatus(AccessoryStatusTypeEnum type, int baseValue)
        {
            int totalValue = 0;

            foreach (var item in Equipment.EquippedItems)
            {
                if (!item.HasAccessoryStatus &&
                    Level >= item.ItemInfo.TamerMinLevel &&
                    Partner.Level >= item.ItemInfo.DigimonMinLevel)
                    continue;

                foreach (var statusValue in item.AccessoryStatus
                    .Where(x => x.Type == type)
                    .Select(x => x.Value))
                {
                    decimal percent = item.HasSocketStatus ? 0 : (decimal)item.Power / 100;

                    if (type == AccessoryStatusTypeEnum.AS || type >= AccessoryStatusTypeEnum.Data)
                    {
                        if (type >= AccessoryStatusTypeEnum.Data)
                        {
                            if (!UtilitiesFunctions.HasAcessoryAttribute(Partner.BaseInfo.Attribute, type) ||
                                !UtilitiesFunctions.HasAcessoryElement(Partner.BaseInfo.Element, type))
                                break;
                        }

                        var percentValue = (decimal)statusValue / 100;
                        totalValue += (int)((percent * percentValue * baseValue) / 100);
                    }
                    else if (type == AccessoryStatusTypeEnum.CT ||
                            type == AccessoryStatusTypeEnum.EV ||
                            type == AccessoryStatusTypeEnum.ATT)
                    {
                        totalValue += (int)(statusValue * percent * 100);
                    }
                    else if (type == AccessoryStatusTypeEnum.CD)
                    {
                        totalValue += (int)((statusValue / 100) * item.Power);
                    }
                    else
                    {
                        totalValue += (int)(percent * statusValue);
                    }
                }
            }

            return totalValue;
        }



        /// <summary>
        /// Returns the target attribute buffs value.
        /// </summary>
        /// <param name="baseValue">Base character attribute value.</param>
        /// <param name="attributes">Target attribute params.</param>
        public int BuffAttribute(int baseValue, params SkillCodeApplyAttributeEnum[] attributes)
        {
            var totalValue = 0.0;
            var SomaValue = 0.0;

            foreach (var buff in BuffList.ActiveBuffs)
            {
                if (buff.BuffInfo == null || buff.BuffInfo.SkillInfo == null)
                    continue;

                foreach (var apply in buff.BuffInfo.SkillInfo.Apply)
                {
                    if (attributes.Any(x => x == apply.Attribute))
                    {
                        switch (apply.Type)
                        {
                            case SkillCodeApplyTypeEnum.Default:
                                totalValue += apply.Value;
                                break;

                            case SkillCodeApplyTypeEnum.AlsoPercent:
                            case SkillCodeApplyTypeEnum.Percent:
                                {
                                    SomaValue += apply.Value + (buff.TypeN) * apply.IncreaseValue;

                                    if (apply.Attribute == SkillCodeApplyAttributeEnum.SCD)
                                    {
                                        totalValue = SomaValue * 100;
                                        break;
                                    }
                                    else if (apply.Attribute == SkillCodeApplyAttributeEnum.CAT ||
                                             apply.Attribute == SkillCodeApplyAttributeEnum.EXP)
                                    {
                                        totalValue = SomaValue;
                                        break;
                                    }

                                    totalValue += (SomaValue / 100.0) * baseValue;
                                }
                                break;
                        }
                    }
                }
            }

            return (int)totalValue;
        }

        public int GetDeckBuff(DeckOptionEnum type, int baseValue = 0)
        {
            double finalValue = 0;  // Usando double para maior precisão
            if (ActiveDeck == null || ActiveDeck.Count == 0)
            {
                return 0;
            }

            foreach (var deck in ActiveDeck)
            {
                if (deck.Condition != DeckConditionEnum.Passive.GetHashCode())
                {
                    continue;
                }

                if ((DeckOptionEnum)deck.Option == type)
                {
                    double deckUp = deck.Value / 100.0;
                    double bonus = baseValue * deckUp;
                    finalValue += bonus;
                }
            }

            int result = (int)Math.Round(finalValue);
            return result;
        }
        public double GetDeckBuff(DeckOptionEnum type, double baseValue = 0)
        {
            double finalValue = 0;  // Usando double para maior precisão
            if (ActiveDeck == null || ActiveDeck.Count == 0)
            {
                return 0;
            }

            foreach (var deck in ActiveDeck)
            {
                if (deck.Condition != DeckConditionEnum.Passive.GetHashCode())
                {
                    continue;
                }

                if ((DeckOptionEnum)deck.Option == type)
                {
                    double deckUp = deck.Value / 100.0;
                    double bonus = baseValue * deckUp;
                    finalValue += bonus;
                }
            }

            double result = finalValue;
            return result;
        }

        /// <summary>
        /// Sets the character as dead.
        /// </summary>
        public void Die()
        {
            InBattle = false;
            Dead = true;

            PreviousCondition = CurrentCondition;
            CurrentCondition = ConditionEnum.Die;

            CurrentHp = 0;
            CurrentDs = 0;
            ActiveEvolution.SetDs(0);
            ActiveEvolution.SetXg(0);

            Partner.Die();
        }

        /// <summary>
        /// Sets the character as alive.
        /// </summary>
        public void Revive()
        {
            if (!Alive)
            {
                Dead = false;

                CurrentHp = HP / 4;
                CurrentDs = DS / 5;
                Partner.RestoreHp(Partner.HP / 3);
                Partner.RestoreDs(Partner.DS / 4);
            }

            PreviousCondition = CurrentCondition;
            CurrentCondition = ConditionEnum.Default;

            Partner.Revive();
        }

        /// <summary>
        /// Adds a new digimon to the list.
        /// </summary>
        /// <param name="digimon">The digimon to add.</param>
        public void AddDigimon(DigimonModel digimon) => Digimons.Add(digimon);

        /// <summary>
        /// Update target digimon by slot.
        /// </summary>
        /// <param name="digimon">New digimon</param>
        /// <param name="slot">Target slot</param>
        public void UpdateDigimon(DigimonModel digimon, long digimonId)
        {
            var target = Digimons.First(x => x.Id == digimonId);
            target = digimon;
        }

        /// <summary>
        /// Adds a new friend to the list.
        /// </summary>
        /// <param name="friend">The friend to add.</param>
        public void AddFriend(CharacterFriendModel friend) => Friends.Add(friend);

        /// <summary>
        /// Updates the current tamer title.
        /// </summary>
        /// <param name="newTitleId">The new title id</param>
        public void UpdateCurrentTitle(short newTitleId) => CurrentTitle = newTitleId;

        /// <summary>
        /// Updates the current tamer deck buff id.
        /// </summary>
        /// <param name="deckBuffId">Set the new deck buff</param>
        /// <param name="deckBuffModel"></param>
        public void UpdateDeckBuffId(int? deckBuffId, DeckBuffModel? deckBuffModel)
        {
            DeckBuffId = deckBuffId;
            DeckBuff = deckBuffModel;
        }

        /// <summary>
        /// Updates the current tamer title.
        /// </summary>
        /// <param name="sendOnceSent">The new title id</param>
        public void UpdateInitialPacketSentOnceSent(bool sendOnceSent) => InitialPacketSentOnceSent = sendOnceSent;

        /// <summary>
        /// Adds a new ItemList to the current object list.
        /// </summary>
        /// <param name="itemList">The new ItemList</param>
        public void AddItemList(ItemListModel itemList) => ItemList.Add(itemList);

        /// <summary>
        /// Verifies every item expiration time and sets the
        /// proper duration value for expired items.
        /// </summary>
        public void CheckExpiredItems()
        {
        }

        /// <summary>
        /// Set the handler value based on the current map.
        /// </summary>
        /// <param name="mapHandler">The current map handler value.</param>
        /// <returns>The character instance.</returns>
        public void SetHandlerValue(short mapHandler)
        {
            _handlerValue = ProperModel + mapHandler;
        }

        public void SetLastExpiredItemsCheck() => LastExpiredItemsCheck = DateTime.Now.AddSeconds(60);

        /// <summary>
        /// Restores the previous character condition.
        /// </summary>
        public void RestorePreviousCondition() => CurrentCondition = PreviousCondition;

        /// <summary>
        /// Updates the character current condiction and saves the previous one.
        /// </summary>
        /// <param name="condition"></param>
        public void UpdateCurrentCondition(ConditionEnum condition)
        {
            PreviousCondition = CurrentCondition;
            CurrentCondition = condition;
        }

        /// <summary>
        /// Updates character target handler.
        /// </summary>
        /// <param name="handler">New handler.</param>
        public void UpdateTargetHandler(int handler) => TargetHandler = handler;

        /// <summary>
        /// Updates character shop item id.
        /// </summary>
        /// <param name="shopItemId">New shop item id.</param>
        public void UpdateShopItemId(int shopItemId) => ShopItemId = shopItemId > 0 ? shopItemId : ShopItemId;

        /// <summary>
        /// Updates character's shop name.
        /// </summary>
        /// <param name="shopName">New shop name.</param>
        public void UpdateShopName(string shopName) => ShopName = shopName;

        /// <summary>
        /// Updates the character's save resources timer.
        /// </summary>
        public void UpdateSaveResourcesTime(int seconds = 20) => LastSaveResources = DateTime.Now.AddSeconds(seconds);

        /// <summary>
        /// Updates the character's sync resources timer.
        /// </summary>
        public void UpdateSyncResourcesTime() => LastSyncResources = DateTime.Now.AddSeconds(5);

        public void UpdateDebuffTime() => LastDebuffUpdate = DateTime.Now.AddSeconds(5);

        /// <summary>
        /// Updates the character's daily quest sync timer.
        /// </summary>
        public void UpdateDailyQuestsSyncTime() => LastDailyQuestCheck = DateTime.Now.AddSeconds(60);

        /// <summary>
        /// Updates the character's buffs check timer.
        /// </summary>
        public void UpdateBuffsCheckTime() => LastBuffsCheck = DateTime.Now.AddSeconds(2);


        /// <summary>
        /// Passive resources regeneration.
        /// </summary>
        public void AutoRegen()
        {
            if (!Dead && !InBattle && DateTime.Now >= LastRegenUpdate.AddSeconds(5))
            {
                LastRegenUpdate = DateTime.Now;

                if (CurrentHp < HP)
                {
                    CurrentHp += (int)Math.Ceiling(HP * 0.01);
                    if (CurrentHp > HP) CurrentHp = HP;
                }

                if (CurrentDs < DS)
                {
                    CurrentDs += 10;
                    if (CurrentDs > DS) CurrentDs = DS;
                }

            }
        }

        /// <summary>
        /// Fully heals character's HP and DS.
        /// </summary>
        public void FullHeal()
        {
            CurrentHp = HP;
            CurrentDs = DS;
        }

        /// <summary>
        /// Recover character HP.
        /// </summary>
        public void RecoverHp(int hpToRecover)
        {
            if (CurrentHp + hpToRecover <= HP)
                CurrentHp += hpToRecover;
            else
                CurrentHp = HP;
        }

        /// <summary>
        /// Recover character DS.
        /// </summary>
        public void RecoverDs(int dsToRecover)
        {
            if (CurrentDs + dsToRecover <= DS)
                CurrentDs += dsToRecover;
            else
                CurrentDs = DS;
        }

        /// <summary>
        /// Resources reduction for character's active evolution.
        /// </summary>
        public void ActiveEvolutionReduction()
        {
            if (!Dead && DateTime.Now >= LastActiveEvolutionUpdate.AddSeconds(2))
            {
                LastActiveEvolutionUpdate = DateTime.Now;

                if (ActiveEvolution.DsPerSecond > 0 && !HasAura)
                {
                    if (ActiveEvolution.DsPerSecond > CurrentDs)
                        ConsumeDs(CurrentDs);
                    else
                        ConsumeDs(ActiveEvolution.DsPerSecond);
                }

                if (ActiveEvolution.XgPerSecond > 0)
                {
                    if (ActiveEvolution.XgPerSecond > XGauge)
                        ConsumeXg(XGauge);
                    else
                        ConsumeXg(ActiveEvolution.XgPerSecond);
                }
            }
        }

        /// <summary>
        /// Reduces character DS.
        /// </summary>
        /// <param name="value">Value to reduce.</param>
        /// <returns>True if it's possible to reduce, false if it's not.</returns>
        public bool ConsumeDs(int value)
        {
            if (CurrentDs >= value)
            {
                CurrentDs -= value;
                if (CurrentDs <= 0) CurrentDs = 0;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Reduces character XGauge.
        /// </summary>
        /// <param name="value">Value to reduce.</param>
        /// <returns>True if it's possible to reduce, false if it's not.</returns>
        public bool ConsumeXg(int value)
        {
            if (XGauge >= value)
            {
                XGauge -= value;
                if (XGauge <= 0)
                {
                    XGauge = 0;
                    return false;
                }
                else return true;
            }

            return false;
        }

        public bool ConsumeXCrystal(short value)
        {
            if (XCrystals >= value)
            {
                XCrystals -= value;
                if (XCrystals <= 0)
                {
                    XCrystals = 0;
                    return false;
                }
                else return true;
            }

            return false;
        }

        public void SetPartnerPassiveBuff(CharacterModelEnum model = CharacterModelEnum.Unknow)
        {
            if (model == CharacterModelEnum.Unknow)
                model = Model;

            var attribute = Partner.BaseInfo.Attribute;

            var buffs = new Dictionary<CharacterModelEnum, Dictionary<DigimonAttributeEnum, (int, int)>>()
            {
                [CharacterModelEnum.MarcusDamon] = new()
                {
                    [DigimonAttributeEnum.Data] = (40212, 8000131),
                    [DigimonAttributeEnum.Vaccine] = (40211, 8000121)
                },
                [CharacterModelEnum.ThomasNorstein] = new()
                {
                    [DigimonAttributeEnum.Data] = (40214, 8000221),
                    [DigimonAttributeEnum.Virus] = (40215, 8000231)
                },
                [CharacterModelEnum.YoshinoFujieda] = new()
                {
                    [DigimonAttributeEnum.Data] = (40217, 8000321),
                    [DigimonAttributeEnum.Vaccine] = (40218, 8000331)
                },
                [CharacterModelEnum.KeenanKrier] = new()
                {
                    [DigimonAttributeEnum.None] = (40221, 8000431),
                    [DigimonAttributeEnum.Vaccine] = (40220, 8000421)
                },
                [CharacterModelEnum.TaiKamiya] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40224, 8000531),
                    [DigimonAttributeEnum.Vaccine] = (40223, 8000521)
                },
                [CharacterModelEnum.MimiTachikawa] = new()
                {
                    [DigimonAttributeEnum.Data] = (40226, 8000621),
                    [DigimonAttributeEnum.None] = (40227, 8000631)
                },
                [CharacterModelEnum.MattIshida] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40229, 8000721),
                    [DigimonAttributeEnum.Data] = (40230, 8000731)
                },
                [CharacterModelEnum.TakeruaKaishi] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40232, 8000821),
                    [DigimonAttributeEnum.None] = (40233, 8000831)
                },
                [CharacterModelEnum.HikariKamiya] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40235, 8000921),
                    [DigimonAttributeEnum.Vaccine] = (40236, 8000931)
                },
                [CharacterModelEnum.SoraTakenoushi] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40238, 8001021),
                    [DigimonAttributeEnum.Unknown] = (40239, 8001031)
                },
                [CharacterModelEnum.IzzyIzumi] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40275, 8001421),
                    [DigimonAttributeEnum.Unknown] = (40276, 8001431)
                },
                [CharacterModelEnum.JoeKido] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40278, 8001521),
                    [DigimonAttributeEnum.Unknown] = (40279, 8001531)
                },
                [CharacterModelEnum.TakatoMatsuki] = new()
                {
                    [DigimonAttributeEnum.Virus] = (40282, 8001621),
                    [DigimonAttributeEnum.Vaccine] = (40283, 8001631)
                },
                [CharacterModelEnum.RikaNonaka] = new()
                {
                    [DigimonAttributeEnum.Data] = (40285, 8001721),
                    [DigimonAttributeEnum.Virus] = (40286, 8001731)
                },
                [CharacterModelEnum.HenryWong] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40288, 8001821),
                    [DigimonAttributeEnum.Data] = (40289, 8001831)
                },
                [CharacterModelEnum.KatoJeri] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40294, 8001921),
                    [DigimonAttributeEnum.Unknown] = (40295, 8001931)
                },
                [CharacterModelEnum.AkiyamaRyo] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40297, 8002021),
                    [DigimonAttributeEnum.None] = (40298, 8002031)
                },
                [CharacterModelEnum.HiroAmanokawa] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40300, 8002121),
                    [DigimonAttributeEnum.None] = (40301, 8002131)
                },
                [CharacterModelEnum.RuliTsukiyono] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40303, 8002221),
                    [DigimonAttributeEnum.None] = (40304, 8002231)
                },
                [CharacterModelEnum.KiyoshirouHigashimitarai] = new()
                {
                    [DigimonAttributeEnum.Vaccine] = (40306, 8002321),
                    [DigimonAttributeEnum.None] = (40307, 8002331)
                }
                // Os três últimos estão vazios conforme original:
                // CharacterModelEnum.KanbaraTakuya, KoichiKimura, Cleiton
            };

            if (buffs.TryGetValue(model, out var attrDict) &&
                attrDict.TryGetValue(attribute, out var buff))
            {
                Partner.BuffList.Add(DigimonBuffModel.Create(buff.Item1, buff.Item2));
            }
        }

        public void RemovePartnerPassiveBuff()
        {
            var targetBuff = Partner.BuffList.TamerBaseSkill();
            if (targetBuff != null)
            {
                Partner.BuffList.ForceExpired(targetBuff.BuffId);
                Partner.BuffList.Remove(targetBuff.BuffId);
            }
        }

        public void SetCurrentChannel(byte? channel) => Channel = channel ?? 0;
        public void SetHidden(bool hidden) => Hidden = hidden;
        public void SetGodMode(bool enabled) => GodMode = enabled;
        public void UpdateState(CharacterStateEnum state) => State = state;
        public void UpdateEventState(CharacterEventStateEnum state) => EventState = state;
        public void UpdateName(string name) => Name = name;
        public void SetBaseStatus(CharacterBaseStatusAssetModel status) => BaseStatus = status;
        public void SetLevelStatus(CharacterLevelStatusAssetModel status) => LevelingStatus = status;
        public void SetGuild(GuildModel guild = null) => Guild = guild;
        public void SetXai(CharacterXaiModel? xai) => Xai = xai;

        public void CleanupDeadTargetsAndBattleState()
        {
            // Remove dead or null mobs from all target lists
            TargetMobs?.RemoveAll(mob => mob == null || !mob.Alive);
            TargetIMobs?.RemoveAll(mob => mob == null || (mob is MobConfigModel m && !m.Alive));
            TargetSummonMobs?.RemoveAll(mob => mob == null || !mob.Alive);
            TargetEventMobs?.RemoveAll(mob => mob == null || !mob.Alive);
            TargetPartners?.RemoveAll(partner => partner == null || !partner.Alive);

            // If all lists are empty, stop battle
            bool hasTargets =
                (TargetMobs != null && TargetMobs.Count > 0) ||
                (TargetIMobs != null && TargetIMobs.Count > 0) ||
                (TargetSummonMobs != null && TargetSummonMobs.Count > 0) ||
                (TargetEventMobs != null && TargetEventMobs.Count > 0) ||
                (TargetPartners != null && TargetPartners.Count > 0);

            if (!hasTargets)
                StopBattle(true); // Or StopBattle() depending on your logic
        }

        public void MovementUpdated() => LastMovementUpdate = DateTime.Now;
        public void UpdateTimeReward()
        {
            TimeReward.RewardIndex++;
            TimeReward.CurrentTime = 0;
        }

        public void SetXGauge(int xGauge)
        {
            XGauge += xGauge;
            if (XGauge > Xai.XGauge) XGauge = Xai.XGauge;
        }

        public void SetXCrystals(int xCrystals)
        {
            XCrystals += (short)xCrystals;
            if (XCrystals > Xai.XCrystals) XCrystals = Xai.XCrystals;
        }

        public void NewLocation(int x, int y, float z = 0)
        {
            Location.SetX(x);
            Location.SetY(y);
            Location.SetZ(z);
        }

        public void NewViewLocation(int x, int y)
        {
            ViewLocation.SetX(x);
            ViewLocation.SetY(y);
        }

        public void NewLocation(int mapId, int x, int y, bool toEvent = false)
        {
            if (toEvent)
            {
                BeforeEvent.SetMapId(Location.MapId);
                BeforeEvent.SetX(Location.X);
                BeforeEvent.SetY(Location.Y);
            }

            Location.SetMapId((short)mapId);
            Location.SetX(x);
            Location.SetY(y);
        }


        public void Move(int wait, int newX, int newY)
        {
            const int baseSplitter = 32;

            if (wait > 0)
            {
                var octers = wait / baseSplitter;
                if (octers > 0)
                {
                    for (int i = 0; i < baseSplitter && !TempRecalculate; i++)
                    {
                        Thread.Sleep(octers);
                        ViewLocation.SetX(ViewLocation.X + (newX - ViewLocation.X) / baseSplitter);
                        ViewLocation.SetY(ViewLocation.Y + (newY - ViewLocation.Y) / baseSplitter);
                    }
                }
                else Thread.Sleep(wait);
            }
            else
            {
                Thread.Sleep(500);
                ViewLocation.SetX(newX);
                ViewLocation.SetY(newY);
            }

            TempCalculating = false;
        }

        public byte[] SerializeMapRegion()
        {
            using var m = new MemoryStream();
            foreach (var region in MapRegions)
                m.Write(region.ToArray(), 0, 1);
            return m.ToArray();
        }

        // Deck
        public int? CurrentActiveDeck { get; private set; }

        public void SetActiveDeck(int deckId)
        {
            CurrentActiveDeck = deckId;
        }

        public void SetActiveDecks(List<CharacterActiveDeckModel> decks)
        {
            ActiveDeck = decks ?? new List<CharacterActiveDeckModel>();
        }
        public CharacterModel SetDeckBuff(DeckBuffModel? deckBuffModel)
        {
            DeckBuff = deckBuffModel;
            return this;
        }
        public void SetClientOption(int value)
        {
            ClientOption = value;
        }
    }
}
