using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using System.Diagnostics;

namespace DigitalWorldOnline.Commons.Utils
{
    public static class UtilitiesFunctions
    {
        // ---------------------------------------------------------------------------
        // MAP TYPE CONFIGURATION
        // ---------------------------------------------------------------------------

        #region MapType_Config

        public static List<short> DungeonMapIds = new List<short>()
{
    // D-Terminal Dungeons
    13, 95,
    
    // Battle/Event Maps
    17, 18, 20, 50, 51,
    
    // Seven Devil Bases
    21, 22, 23, 24, 25, 26, 27,
    
    // Yokohama Burning Time (Weekdays)
    96, 97, 98, 99,
    
    // Yokohama Burning Time (Weekends)
    101, 102, 103,
    
    // Odaiba Dungeons
    205, 210, 211, 213, 214, 215, 252, 253, 270,    
    
    // Susanomon DG
    89, 
    
    // Training Ground
    88,
    
    // Western Area Dungeons
    1110, 1111, 1112,
    
    // File Island Dungeons
    1304, 1308, 1309, 1310, 1311,   
    
    // Server Continent Dungeons
    1403, 1404, 1406,
    
    // Four Holy Beasts Dungeons
    1600, 1601, 1602, 1603, 1604, 1605, 1606, 1607, 1608, 1609,
    1610, 1611, 1612, 1613, 1614, 1615,
    
    // Royal Base Dungeons
    1701, 1702, 1703, 1704, 1705, 1706,
    
    // Tamers Dungeons
    1809, 1810, 1911, 1912, 1914, 1915,
    
    // Shadow Labyrinth
    2001, 2002,
    
    // PVP/Special Events
    9101, 9861, 9862, 9863, 9864,
    
    // Attribute Arena
    3000
};

        public static List<short> EventMapIds = new List<short>()
        {
            
        };

        public static List<short> PvpMapIds = new List<short>()
        {
            
        };

        #endregion

        // ---------------------------------------------------------------------------
        // CHANNEL CONFIGURATION
        // ---------------------------------------------------------------------------

        #region Channel_Config

        /// <summary>
        /// ⚠️ UYARI: Bu constant artık sadece fallback olarak kullanılıyor.
        /// ASLA GetTargetChannel parametresi olarak kullanılmayın!
        /// Dinamik kanal sayısı Config.Map tablosundan alınmalıdır.
        /// </summary>
        [Obsolete("Bu constant artık önerilmemektedir. Bunun yerine mapConfig.Channels değerini kullanın.")]
        public const byte DefaultMapChannelsCount = 3;

        public const byte MaxPlayersPerChannel = 100;   // max 255

        private static readonly Random _random = new();

        /// <summary>
        /// ✅ YENİ VERSİYON - Dinamik kanal sayısı desteği
        /// 
        /// KULLANIM:
        /// var targetChannel = UtilitiesFunctions.GetTargetChannel(
        ///     preferredChannel: client.PreferredChannel,
        ///     channels: channelDictionary,
        ///     maxChannels: mapConfig.Channels  // ⭐ ZORUNLU OLARAK GEÇ
        /// );
        /// 
        /// NOT: maxChannels parametresini ASLA boş bırakma, her zaman mapConfig.Channels değerini geç!
        /// </summary>
        public static byte GetTargetChannel(byte preferredChannel, Dictionary<byte, byte> channels, byte maxChannels)
        {
            // 📌 Validation: maxChannels en az 1 olmalı
            if (maxChannels == 0)
            {
                maxChannels = 1;
            }

            // Eğer tercih edilen kanal geçerliyse ve kullanılabiliyorsa, onu seç
            if (preferredChannel != byte.MaxValue && preferredChannel < maxChannels)
            {
                if (channels != null && channels.TryGetValue(preferredChannel, out byte playerCount))
                {
                    if (playerCount < MaxPlayersPerChannel)
                    {
                        return preferredChannel;
                    }
                }
                else if (preferredChannel < maxChannels)
                {
                    return preferredChannel;
                }
            }

            // Kullanılabilir kanal listesini oluştur
            var availableChannels = new List<byte>();

            for (byte i = 0; i < maxChannels; i++)
            {
                if (channels != null && channels.TryGetValue(i, out byte playerCount))
                {
                    if (playerCount < MaxPlayersPerChannel)
                    {
                        availableChannels.Add(i);
                    }
                }
                else
                {
                    availableChannels.Add(i);
                }
            }

            // Eğer kullanılabilir kanal varsa, rasgele seç
            if (availableChannels.Any())
            {
                return SelectRandomChannel(availableChannels);
            }

            // Fallback: kanal dolu olsa bile kanal 0'ı döndür
            return 0;
        }

        /// <summary>
        /// Seleciona um canal aleatório de uma coleção de chaves de canal.
        /// </summary>
        private static byte SelectRandomChannel(IEnumerable<byte> channelKeys)
        {
            var keys = channelKeys.ToList();

            if (keys.Count == 0)
            {
                return 0;
            }

            return keys[_random.Next(keys.Count)];
        }

        public static byte GetChannelLoad(this byte playerCount)
        {
            return playerCount switch
            {
                >= 0 and < (byte)(MaxPlayersPerChannel * 0.2) => (byte)ChannelLoadEnum.Empty,
                >= (byte)(MaxPlayersPerChannel * 0.2) and < (byte)(MaxPlayersPerChannel * 0.3) => (byte)ChannelLoadEnum.TwentyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.3) and < (byte)(MaxPlayersPerChannel * 0.4) => (byte)ChannelLoadEnum.ThirtyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.4) and < (byte)(MaxPlayersPerChannel * 0.5) => (byte)ChannelLoadEnum.FourtyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.5) and < (byte)(MaxPlayersPerChannel * 0.6) => (byte)ChannelLoadEnum.FiftyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.6) and < (byte)(MaxPlayersPerChannel * 0.7) => (byte)ChannelLoadEnum.SixtyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.7) and < (byte)(MaxPlayersPerChannel * 0.8) => (byte)ChannelLoadEnum.SeventyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.8) and < (byte)(MaxPlayersPerChannel * 0.9) => (byte)ChannelLoadEnum.EightyPercent,
                >= (byte)(MaxPlayersPerChannel * 0.9) and < MaxPlayersPerChannel => (byte)ChannelLoadEnum.NinetyPercent,
                _ => (byte)ChannelLoadEnum.Full
            };
        }

        #endregion

        // ---------------------------------------------------------------------------
        // DIGIMON EXP CONFIGURATION
        // ---------------------------------------------------------------------------

        #region DigimonExp_Config

        public const int DigimonMaxSkillMastery = 60;
        public const int DigimonAttributeMaxExp = 10000;
        public const int DigimonElementMaxExp = 10000;

        #endregion

        // ---------------------------------------------------------------------------
        // KILL SPAWN CONFIGURATION
        // ---------------------------------------------------------------------------

        #region KillSpawn_Config

        public const int KillSpawnShowCount = 10;

        #endregion

        // ---------------------------------------------------------------------------
        // ENCYCLOPEDIA CONFIGURATION
        // ---------------------------------------------------------------------------

        #region Encyclopedia_Config

        public const int DeckRewardItemId = 97206;
        public const int DeckRewardItemAmount = 10;

        #endregion

        // ---------------------------------------------------------------------------
        // TAMER SKILL CONFIGURATION
        // ---------------------------------------------------------------------------

        #region TamerSkill_Config

        #endregion

        // ---------------------------------------------------------------------------
        // VIP CONFIGURATION
        // ---------------------------------------------------------------------------

        #region Vip_Configuration

        private static readonly Dictionary<AccountAccessLevelEnum, int> _dropMultipliers = new Dictionary<AccountAccessLevelEnum, int>()
        {
            { AccountAccessLevelEnum.Vip1, 1 },
            { AccountAccessLevelEnum.Vip2, 3 },
            { AccountAccessLevelEnum.Vip3, 4 },
            { AccountAccessLevelEnum.Vip4, 5 },
            { AccountAccessLevelEnum.Vip5, 6 },
        };

        private static readonly Dictionary<AccountAccessLevelEnum, int> _bitMultipliers = new Dictionary<AccountAccessLevelEnum, int>()
        {
            { AccountAccessLevelEnum.Vip1, 1 },
            { AccountAccessLevelEnum.Vip2, 3 },
            { AccountAccessLevelEnum.Vip3, 4 },
            { AccountAccessLevelEnum.Vip4, 5 },
            { AccountAccessLevelEnum.Vip5, 6 },
        };

        public static int GetBitMultiplier(AccountAccessLevelEnum accessLevel)
        {
            if (_bitMultipliers.TryGetValue(accessLevel, out int multiplier))
            {
                return multiplier;
            }

            return 1;
        }

        public static int GetDropMultiplier(AccountAccessLevelEnum accessLevel)
        {
            if (_dropMultipliers.TryGetValue(accessLevel, out int multiplier))
            {
                return multiplier;
            }

            return 1;
        }

        // Use method:
        // ==> int vipMultiplier = UtilitiesFunctions.GetBitMultiplier(targetClient.AccessLevel);
        // ==> int vipMultiplier = UtilitiesFunctions.GetDropMultiplier(targetClient.AccessLevel);

        #endregion

        // ---------------------------------------------------------------------------

        #region IReadOnlyDictionary Extensions

        public  static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey? key, TValue defaultValue = default)
        { 
            if (dictionary == null || key == null || !dictionary.ContainsKey(key))
            {
                return defaultValue;
            }
            return dictionary[key];
        }


        #endregion


        // ---------------------------------------------------------------------------

        public static List<int> IncreasePerLevelStun = new List<int>()
        {
            7501411, 7500811, 7500511
        };

        public class fPos
        {
            public int x;
            public int y;

            public fPos()
            {
                x = 0;
                y = 0;
            }

            public fPos(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public void Set(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public void Set(fPos other)
            {
                x = other.x;
                y = other.y;
            }

            public static fPos operator -(fPos a, fPos b)
            {
                return new fPos(a.x - b.x, a.y - b.y);
            }

            public static fPos operator +(fPos a, fPos b)
            {
                return new fPos(a.x + b.x, a.y + b.y);
            }

            public static fPos operator *(fPos a, int factor)
            {
                return new fPos(a.x * factor, a.y * factor);
            }

            public static fPos operator /(fPos a, int divisor)
            {
                if (divisor == 0)
                    return new fPos(0, 0);

                return new fPos(a.x / divisor, a.y / divisor);
            }

            public int Length()
            {
                return (int)Math.Sqrt(x * x + y * y);
            }

            public int Unitize()
            {
                int length = Length();
                if (length > 1e-06)
                {
                    int recip = 1 / length;
                    x *= recip;
                    y *= recip;
                }
                else
                {
                    x = 0;
                    y = 0;
                    length = 0;
                }

                return length;
            }
        }

        public static byte[] GroupPackets(params byte[][] packets)
        {
            var resultArray = new byte[packets.Sum(a => a.Length)];

            var offset = 0;

            foreach (var packet in packets)
            {
                Buffer.BlockCopy(packet, 0, resultArray, offset, packet.Length);
                offset += packet.Length;
            }

            return resultArray;
        }

        public static ItemListMovimentationEnum SwitchItemList(int originSlot, int destinationSlot)
        {
            if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                &&
                destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.InventoryToInventory;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.EquipmentMinSlot, GeneralSizeEnum.EquipmentMaxSlot))
            {
                return ItemListMovimentationEnum.InventoryToEquipment;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.WarehouseMinSlot, GeneralSizeEnum.WarehouseMaxSlot))
            {
                return ItemListMovimentationEnum.InventoryToWarehouse;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.AccountWarehouseMinSlot,
                         GeneralSizeEnum.AccountWarehouseMaxSlot))
            {
                return ItemListMovimentationEnum.InventoryToAccountWarehouse;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.EquipmentMinSlot, GeneralSizeEnum.EquipmentMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.EquipmentToInventory;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.WarehouseMinSlot, GeneralSizeEnum.WarehouseMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.WarehouseMinSlot, GeneralSizeEnum.WarehouseMaxSlot))
            {
                return ItemListMovimentationEnum.WarehouseToWarehouse;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.WarehouseMinSlot, GeneralSizeEnum.WarehouseMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.WarehouseToInventory;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.WarehouseMinSlot, GeneralSizeEnum.WarehouseMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.AccountWarehouseMinSlot,
                         GeneralSizeEnum.AccountWarehouseMaxSlot))
            {
                return ItemListMovimentationEnum.WarehouseToAccountWarehouse;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.AccountWarehouseMinSlot,
                         GeneralSizeEnum.AccountWarehouseMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.AccountWarehouseMinSlot,
                         GeneralSizeEnum.AccountWarehouseMaxSlot))
            {
                return ItemListMovimentationEnum.AccountWarehouseToAccountWarehouse;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.AccountWarehouseMinSlot,
                         GeneralSizeEnum.AccountWarehouseMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.AccountWarehouseToInventory;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.AccountWarehouseMinSlot,
                         GeneralSizeEnum.AccountWarehouseMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.WarehouseMinSlot, GeneralSizeEnum.WarehouseMaxSlot))
            {
                return ItemListMovimentationEnum.AccountWarehouseToWarehouse;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                     &&
                     destinationSlot == GeneralSizeEnum.XaiSlot.GetHashCode())
            {
                return ItemListMovimentationEnum.InventoryToEquipment;
            }
            else if (originSlot == GeneralSizeEnum.XaiSlot.GetHashCode()
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.EquipmentToInventory;
            }
            else if (originSlot == GeneralSizeEnum.DigiviceSlot.GetHashCode()
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.DigiviceToInventory;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                     &&
                     destinationSlot == GeneralSizeEnum.DigiviceSlot.GetHashCode())
            {
                return ItemListMovimentationEnum.InventoryToDigivice;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.ChipsetMinSlot, GeneralSizeEnum.ChipsetMaxSlot))
            {
                return ItemListMovimentationEnum.InventoryToChipset;
            }
            else if (originSlot.IsBetween(GeneralSizeEnum.ChipsetMinSlot, GeneralSizeEnum.ChipsetMaxSlot)
                     &&
                     destinationSlot.IsBetween(GeneralSizeEnum.InventoryMinSlot, GeneralSizeEnum.InventoryMaxSlot))
            {
                return ItemListMovimentationEnum.ChipsetToInventory;
            }
            else
            {
                return ItemListMovimentationEnum.InvalidMovimentation;
            }
        }

        public static int RemainingTimeSeconds(int seconds)
        {
            return (int)DateTimeOffset.Now
                .AddSeconds(DateTime.Now.AddSeconds(seconds).Subtract(DateTime.Now).TotalSeconds).ToUnixTimeSeconds();
        }

        public static int RemainingTimeMinutes(int minutes)
        {
            if (minutes == 0)
                return 0;

            return (int)DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeSeconds();
        }

        // ---------------------------------------------------------------------------

        public static long CurrentRemainingTimeToResetDay()
        {
            // Obter o próximo reset time para o mesmo dia
            var nextResetTime = DateTime.Today.AddDays(1) - DateTime.Now;

            // Calcular e retornar o Unix timestamp do próximo reset
            return DateTimeOffset.UtcNow.Add(nextResetTime).ToUnixTimeSeconds();
        }

        public static long CurrentRemainingTimeToResetHour()
        {
            var hourlyResetTime = DateTimeOffset.UtcNow
                .AddSeconds(DateTime.Now
                    .AddMinutes(60 - DateTime.Now.Minute)
                    .Subtract(DateTime.Now)
                    .TotalSeconds
                ).ToUnixTimeSeconds();

            return hourlyResetTime;
        }

        // ---------------------------------------------------------------------------

        public static int GetUtcSeconds(this DateTime? dateTime)
        {
            if (dateTime == null)
                return 0;
            else
                return (int)DateTimeOffset.UtcNow.AddSeconds(dateTime.Value.Subtract(DateTime.Now).TotalSeconds)
                    .ToUnixTimeSeconds();
        }

        public static int GetUtcSecondsBuff(this DateTime? dateTime)
        {
            if (dateTime == null)
                return 0;
            else
                return (int)(dateTime.Value - DateTime.UtcNow).TotalSeconds;
        }

        // ---------------------------------------------------------------------------

        public static int GetUtcSeconds(this DateTime dateTime)
        {
            return (int)DateTimeOffset.UtcNow.AddSeconds(dateTime.Subtract(DateTime.Now).TotalSeconds)
                .ToUnixTimeSeconds();
        }

        public static bool HasAttributeAdvantage(this DigimonAttributeEnum hitter, DigimonAttributeEnum target)
        {
            return hitter switch
            {
                DigimonAttributeEnum.Data =>
                    target == DigimonAttributeEnum.Vaccine ||
                    target == DigimonAttributeEnum.None,

                DigimonAttributeEnum.Vaccine =>
                    target == DigimonAttributeEnum.Virus ||
                    target == DigimonAttributeEnum.None,

                DigimonAttributeEnum.Virus =>
                    target == DigimonAttributeEnum.Data ||
                    target == DigimonAttributeEnum.None,

                DigimonAttributeEnum.Unknown =>
                    target != DigimonAttributeEnum.Unknown, // ventaja sobre todos excepto sí mismo

                DigimonAttributeEnum.None =>
                    false,

                _ => false,
            };
        }


        public static bool HasElementAdvantage(this DigimonElementEnum hitter, DigimonElementEnum target)
        {
            return hitter switch
            {
                DigimonElementEnum.Ice => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Water,
                DigimonElementEnum.Water => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Fire,
                DigimonElementEnum.Fire => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Ice,
                DigimonElementEnum.Land => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Wind,
                DigimonElementEnum.Wind => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Wood,
                DigimonElementEnum.Wood => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Land,
                DigimonElementEnum.Light => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Dark,
                DigimonElementEnum.Dark => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Thunder,
                DigimonElementEnum.Thunder => target == DigimonElementEnum.Neutral ||
                                              target == DigimonElementEnum.Steel,
                DigimonElementEnum.Steel => target == DigimonElementEnum.Neutral || target == DigimonElementEnum.Light,
                _ => false,
            };
        }
                        
        public static bool HasAcessoryAttribute(this DigimonAttributeEnum hitter, AccessoryStatusTypeEnum accessory)
        {
            return accessory == AccessoryStatusTypeEnum.Data && hitter == DigimonAttributeEnum.Data ||
                   accessory == AccessoryStatusTypeEnum.Vacina && hitter == DigimonAttributeEnum.Vaccine ||
                   accessory == AccessoryStatusTypeEnum.Virus && hitter == DigimonAttributeEnum.Virus ||
                   accessory == AccessoryStatusTypeEnum.Unknown && hitter == DigimonAttributeEnum.Unknown;
        }

        public static bool HasAcessoryElement(this DigimonElementEnum hitter, AccessoryStatusTypeEnum accessory)
        {
            return accessory == AccessoryStatusTypeEnum.Ice && hitter == DigimonElementEnum.Ice ||
                   accessory == AccessoryStatusTypeEnum.Water && hitter == DigimonElementEnum.Water ||
                   accessory == AccessoryStatusTypeEnum.Fire && hitter == DigimonElementEnum.Fire ||
                   accessory == AccessoryStatusTypeEnum.Earth && hitter == DigimonElementEnum.Land ||
                   accessory == AccessoryStatusTypeEnum.Wind && hitter == DigimonElementEnum.Wind ||
                   accessory == AccessoryStatusTypeEnum.Wood && hitter == DigimonElementEnum.Wood ||
                   accessory == AccessoryStatusTypeEnum.Light && hitter == DigimonElementEnum.Light ||
                   accessory == AccessoryStatusTypeEnum.Dark && hitter == DigimonElementEnum.Dark ||
                   accessory == AccessoryStatusTypeEnum.Thunder && hitter == DigimonElementEnum.Thunder ||
                   accessory == AccessoryStatusTypeEnum.Steel && hitter == DigimonElementEnum.Steel;
        }

        public static DigimonElementEnum AccessoryStatusTypeEnumToElement(AccessoryStatusTypeEnum type)
            {
                return type switch
                {
                    AccessoryStatusTypeEnum.Fire => DigimonElementEnum.Fire,
                    AccessoryStatusTypeEnum.Water => DigimonElementEnum.Water,
                    AccessoryStatusTypeEnum.Wood => DigimonElementEnum.Wood,
                    AccessoryStatusTypeEnum.Thunder => DigimonElementEnum.Thunder,
                    AccessoryStatusTypeEnum.Earth => DigimonElementEnum.Land,
                    AccessoryStatusTypeEnum.Wind => DigimonElementEnum.Wind,
                    AccessoryStatusTypeEnum.Light => DigimonElementEnum.Light,
                    AccessoryStatusTypeEnum.Dark => DigimonElementEnum.Dark,
                    AccessoryStatusTypeEnum.Ice => DigimonElementEnum.Ice,
                    AccessoryStatusTypeEnum.Steel => DigimonElementEnum.Steel,
                    _ => DigimonElementEnum.None
                };
            }

        public static DigimonAttributeEnum AccessoryStatusTypeEnumToAttribute(AccessoryStatusTypeEnum type)
            {
                return type switch
                {
                    AccessoryStatusTypeEnum.Data => DigimonAttributeEnum.Data,
                    AccessoryStatusTypeEnum.Vacina => DigimonAttributeEnum.Vaccine,
                    AccessoryStatusTypeEnum.Virus => DigimonAttributeEnum.Virus,
                    AccessoryStatusTypeEnum.Unknown => DigimonAttributeEnum.Unknown,
                    _ => DigimonAttributeEnum.None,
                };
            }


        public static short GetLevelSize(int hatchLevel)
        {
            return hatchLevel switch
            {
                3 => UtilitiesFunctions.RandomShort(10000, 10000),
                4 => UtilitiesFunctions.RandomShort(11000, 12500),
                5 => UtilitiesFunctions.RandomShort(12000, 13000),
                _ => 0,
            };
        }

        public static int RandomInt(int minValue = 0, int maxValue = int.MaxValue)
        {
            return _random.Next(minValue, maxValue < int.MaxValue ? maxValue + 1 : int.MaxValue);
        }

        public static byte RandomByte(byte minValue = 0, byte maxValue = byte.MaxValue)
        {
            return (byte)_random.Next(minValue, maxValue < byte.MaxValue ? maxValue + 1 : byte.MaxValue);
        }

        public static short RandomShort(short minValue = 0, short maxValue = short.MaxValue)
        {
            return (short)_random.Next(minValue, maxValue < short.MaxValue ? maxValue + 1 : short.MaxValue);
        }

        /// <summary>
        /// Returns a random value between 0.0% and 100.0%
        /// </summary>
        public static double RandomDouble() => _random.NextDouble() * 100;

        public static bool IsBetween(this int baseValue, params int[] range)
        {
            return range.Contains(baseValue);
        }

        public static bool IsBetween(this int baseValue, int minimalRange, int maximumRange)
        {
            return baseValue >= minimalRange && baseValue <= maximumRange;
        }

        public static bool IsBetween(this int baseValue, Enum minimalRangeEnum, Enum maximumRangeEnum)
        {
            return baseValue.IsBetween(minimalRangeEnum.GetHashCode(), maximumRangeEnum.GetHashCode());
        }

        public static long CalculateDistance(int xa, int xb, int ya, int yb)
        {
            var distanceX = (long)Math.Pow(xb - xa, 2);
            var distanceY = (long)Math.Pow(yb - ya, 2);

            var result = (long)Math.Sqrt(distanceX + distanceY);

            return result;
        }

        public static double CalculateDistanceD(int x1, int y1, int x2, int y2)
        {
            var deltaX = x2 - x1;
            var deltaY = y2 - y1;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public static fPos Lerp(fPos start, fPos end, float t)
        {
            float x = start.x + (end.x - start.x) * t;
            float y = start.y + (end.y - start.y) * t;
            return new fPos((int)x, (int)y);
        }

        public static void Aguardar(int milissegundos)
        {
            if (milissegundos > 0)
            {
                Stopwatch tempStopwatch = new Stopwatch();
                tempStopwatch.Start();

                while (tempStopwatch.ElapsedMilliseconds < milissegundos)
                {
                    // Aguardar até que o tempo especificado seja atingido
                }

                tempStopwatch.Stop();
            }
        }

        public static int MapGroup(int mapId)
        {
            // D-Terminal Underground
            if (mapId == 13 || mapId == 95)
            {
                return 3; // Dats Center
            }
            // Four Holy Beasts & Shadow Labyrinth
            else if (mapId is >= 1600 and <= 1615 || mapId is >= 2001 and <= 2002)
            {
                return 2; // D-terminal
            }
            // Battle/Event Maps
            else if (mapId == 17 || mapId == 50 || mapId == 18)
            {
                return 3; // Dats
            }
            // Seven Devil Bases
            else if (mapId is >= 20 and <= 27)
            {
                return 2; // D-terminal
            }
            // Susanomon Dungeon
            else if (mapId == 89)
            {
                return 3; // Dats Center
            }
            // Training Ground
            else if (mapId == 88)
            {
                return 3; // Dats Center
            }
            // Yokohama Burning Time (Weekdays & Weekends)
            else if (mapId is >= 96 and <= 99 or >= 101 and <= 103)
            {
                return 3; // Dats Center
            }
            // Odaiba Dungeons
            else if (mapId == 205 || mapId == 213)
            {
                return 204; // Tokyo Tower Observatory
            }
            else if (mapId == 210 || mapId == 214)
            {
                return 209; // Fuji TV Rooftop
            }
            else if (mapId == 211 || mapId == 215)
            {
                return 208; // Odaiba
            }
            // Kaiser's Laboratory
            else if (mapId is >= 1110 and <= 1112)
            {
                return 1109; // Dark Tower Wasteland
            }
            // File Island Dungeons
            else if (mapId == 1304)
            {
                return 1303; // Lost Historic Site
            }
            else if (mapId == 1310)
            {
                return 3;
            }
            else if (mapId == 1308 || mapId == 1311)
            {
                return 1306; // Infinite Mountain
            }
            else if (mapId == 1309)
            {
                return 1305; // File Island Waterfront
            }
            // Server Continent Dungeons
            else if (mapId is >= 1403 and <= 1406)
            {
                return 1402; // Server Continent Pyramid
            }
            // Royal Base
            else if (mapId == 1701 || mapId == 1702 || mapId == 1703 || mapId == 1704 || mapId == 1705 || mapId == 1706)
            {
                return 1700; // Verdandi Terminal
            }
            // Tamers Dungeons
            else if (mapId == 1809 || mapId == 1810)
            {
                return 1807; // Zhuqiaomon's Resting Area
            }
            else if (mapId == 1911)
            {
                return 1902; // Forest of Beginning (1902)
            }
            // PVP/Special Events
            else if (mapId == 9101)
            {
                return 9101; // PVP Area
            }
            else if (mapId == 9861)
            {
                return 3; // Dats
            }
            else if (mapId == 9862)
            {
                return 9862; // Stadium of strikes
            }
            else if (mapId == 9863)
            {
                return 1; // Event Map
            }
            else if (mapId == 9864)
            {
                return 1; // Event Map
            }
            // Attribute Arena
            else if (mapId == 3000)
            {
                return 1; // Event Map
            }
            // GM Event Room
            else if (mapId == 51)
            {
                return 4; // Yggdrasill's Room
            }
            else
            {
                return 3; // Map doesn't belong to any known group
            }
        }

        /// <summary>
        /// Check if this item is clone
        /// </summary>
        /// <param name="itemSection"></param>
        /// <returns></returns>
        public static bool IsCloneItem(int itemSection)
        {
            return itemSection.IsBetween(5511, 5512, 5513, 5514, 5515, 5521, 5522, 5523, 5524, 5525, 5536, 5537, 5538,
                5539, 5540, 5540, 5531, 5532, 5533, 5534, 5535, 5501, 5502, 5503, 5504, 5505);
        }
    }
}