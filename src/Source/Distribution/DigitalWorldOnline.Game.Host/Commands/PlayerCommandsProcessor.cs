using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using DigitalWorldOnline.Commons.Enums.Account;
using MediatR;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace DigitalWorldOnline.Game.Commands
{
    public sealed class PlayerCommandsProcessor : IDisposable
    {
        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";

        private readonly PartyManager _partyManager;
        private readonly StatusManager _statusManager;
        private readonly ExpManager _expManager;
        private readonly AssetsLoader _assets;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IConfiguration _configuration;

        private Dictionary<string, (Func<GameClient, string[], Task> Command, List<AccountAccessLevelEnum> AccessLevels)> commands;

        public PlayerCommandsProcessor(PartyManager partyManager, StatusManager statusManager, ExpManager expManager, AssetsLoader assets,
            MapServer mapServer, DungeonsServer dungeonsServer, PvpServer pvpServer,
            ILogger logger, ISender sender, IConfiguration configuration)
        {
            _partyManager = partyManager;
            _expManager = expManager;
            _statusManager = statusManager;
            _assets = assets;
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
            _configuration = configuration;
            InitializeCommands();
        }

        // Comands and permissions
        private void InitializeCommands()
        {
            commands = new Dictionary<string, (Func<GameClient, string[], Task> Command, List<AccountAccessLevelEnum> AccessLevels)>
            {
                { "clear", (ClearCommand, null) },
                { "battlelog", (BattleLogCommand, null) },
                { "stats", (StatsCommand, null) },
                { "time", (TimeCommand, null) },
                { "deckload", (DeckLoadCommand, null) },
                { "pvp", (PvpCommand, null) },
                { "magnetic", (MagneticCommand, null) },
                { "raidstime", (RaidsTimeCommand, null) },
                { "help", (HelpCommand, null) }, // Help está disponível para todos
            };
        }

        public async Task ExecuteCommand(GameClient client, string message)
        {
            var command = Regex.Replace(message.Trim().ToLower(), @"\s+", " ").Split(' ');

            if (commands.TryGetValue(command[0], out var commandInfo))
            {
                if (commandInfo.AccessLevels?.Contains(client.AccessLevel) != false)
                {
                    //_logger.Information($"Sending Command!! [PlayerCommand]");
                    await commandInfo.Command(client, command);
                }
                else
                {
                    _logger.Warning($"Tamer {client.Tamer.Name} tryed to use the command {message} without permission !! [PlayerCommand]");
                    client.Send(new SystemMessagePacket("You do not have permission to use this command."));
                }
            }
            else
            {
                client.Send(new SystemMessagePacket($"Invalid Command !! Type !help"));
            }
        }

        #region Commands

        private async Task ClearCommand(GameClient client, string[] command)
        {
            var regex = @"^clear\s+(inv|cash|gift)$";
            var match = Regex.Match(string.Join(" ", command), regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket("Unknown command.\nType !clear {inv|cash|gift}\n"));
                return;
            }

            if (command[1] == "inv")
            {
                client.Tamer.Inventory.Clear();
                client.Send(new SystemMessagePacket($" Inventory slots cleaned."));
                client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));
                await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
            }
            else if (command[1] == "cash")
            {
                client.Tamer.AccountCashWarehouse.Clear();
                client.Send(new SystemMessagePacket($" CashStorage slots cleaned."));
                client.Send(new LoadAccountWarehousePacket(client.Tamer.AccountCashWarehouse));
                await _sender.Send(new UpdateItemsCommand(client.Tamer.AccountCashWarehouse));
            }
            else if (command[1] == "gift")
            {
                client.Tamer.GiftWarehouse.Clear();
                client.Send(new SystemMessagePacket($" GiftStorage slots cleaned."));
                client.Send(new LoadGiftStoragePacket(client.Tamer.GiftWarehouse));
                await _sender.Send(new UpdateItemsCommand(client.Tamer.GiftWarehouse));
            }
        }

        private async Task BattleLogCommand(GameClient client, string[] command)
        {
            var regex = @"^battlelog\s+(on|off)\s*$";
            var match = Regex.Match(string.Join(" ", command), regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket($"Unknown command. Type !battlelog (on/off)."));
                return;
            }

            string action = match.Groups[1].Value.ToLower();

            switch (action)
            {
                case "on":
                    if (!AttackManager.IsBattle)
                    {
                        AttackManager.SetBattleStatus(true);
                        client.Send(new NoticeMessagePacket($"Battle log is now active!"));
                    }
                    else
                    {
                        client.Send(new NoticeMessagePacket($"Battle log is already active..."));
                    }
                    break;

                case "off":
                    if (AttackManager.IsBattle)
                    {
                        AttackManager.SetBattleStatus(false);
                        client.Send(new NoticeMessagePacket($"Battle log is now inactive!"));
                    }
                    else
                    {
                        client.Send(new NoticeMessagePacket($"Battle log is already inactive..."));
                    }
                    break;

                default:
                    client.Send(new SystemMessagePacket($"Invalid command. Use !battlelog (on/off)"));
                    break;
            }
        }

        private async Task StatsCommand(GameClient client, string[] command)
        {
            string message = string.Join(" ", command);

            var regex = @"^stats\s*$";
            var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket($"Unknown command.\nType !stats"));
                return;
            }

            client.Send(new SystemMessagePacket($"Critical Damage: {client.Tamer.Partner.CD / 100}%\n" +
                $"Attribute Damage: {client.Tamer.Partner.ATT / 100}%\n" +
                $"Digimon SKD: {client.Tamer.Partner.SKD}\n" +
                $"Digimon SCD: {client.Tamer.Partner.SCD / 100}%\n" +
                $"Tamer BonusEXP: {client.Tamer.BonusEXP}%\n" +
                $"Tamer Move Speed: {client.Tamer.MS}"));
        }

        private async Task TimeCommand(GameClient client, string[] command)
        {
            string message = string.Join(" ", command);

            var regex = @"^time\s*$";
            var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket($"Unknown command.\nType !time"));
                return;
            }

            client.Send(new SystemMessagePacket($"Server Time is: {DateTime.UtcNow}"));
        }

        private async Task DeckLoadCommand(GameClient client, string[] command)
        {
            string message = string.Join(" ", command);

            var regex = @"^deckload\s*$";
            var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket($"Unknown command.\nType !deckload"));
                return;
            }

            var evolution = client.Partner.Evolutions[0];

            _logger.Information(
                $"Evolution ID: {evolution.Id} | Evolution Type: {evolution.Type} | Evolution Unlocked: {evolution.Unlocked}");

            var evoInfo = _assets.EvolutionInfo.FirstOrDefault(x => x.Type == client.Partner.BaseType)?.Lines
                .FirstOrDefault(x => x.Type == evolution.Type);

            // --- CREATE DB ----------------------------------------------------------------------------------------

            var digimonEvolutionInfo = _assets.EvolutionInfo.First(x => x.Type == client.Partner.BaseType);

            var digimonEvolutions = client.Partner.Evolutions;

            var encyclopediaExists =
                client.Tamer.Encyclopedia.Exists(x => x.DigimonEvolutionId == digimonEvolutionInfo.Id);

            if (!encyclopediaExists)
            {
                if (digimonEvolutionInfo != null)
                {
                    var newEncyclopedia = CharacterEncyclopediaModel.Create(client.TamerId,
                        digimonEvolutionInfo.Id, client.Partner.Level, client.Partner.Size, 0, 0, 0, 0, 0,
                        false, false);

                    digimonEvolutions?.ForEach(x =>
                    {
                        var evolutionLine = digimonEvolutionInfo.Lines.FirstOrDefault(y => y.Type == x.Type);

                        byte slotLevel = 0;

                        if (evolutionLine != null)
                            slotLevel = evolutionLine.SlotLevel;

                        newEncyclopedia.Evolutions.Add(
                            CharacterEncyclopediaEvolutionsModel.Create(newEncyclopedia.Id, x.Type, slotLevel,
                                Convert.ToBoolean(x.Unlocked)));
                    });

                    var encyclopediaAdded =
                        await _sender.Send(new CreateCharacterEncyclopediaCommand(newEncyclopedia));

                    client.Tamer.Encyclopedia.Add(encyclopediaAdded);
                }
            }

            // --- UNLOCK -------------------------------------------------------------------------------------------

            var encyclopedia =
                client.Tamer.Encyclopedia.First(x => x.DigimonEvolutionId == evoInfo.EvolutionId);

            if (encyclopedia != null)
            {
                var encyclopediaEvolution =
                    encyclopedia.Evolutions.First(x => x.DigimonBaseType == evolution.Type);

                if (!encyclopediaEvolution.IsUnlocked)
                {
                    encyclopediaEvolution.Unlock();

                    await _sender.Send(new UpdateCharacterEncyclopediaEvolutionsCommand(encyclopediaEvolution));

                    int LockedEncyclopediaCount = encyclopedia.Evolutions.Count(x => x.IsUnlocked == false);

                    if (LockedEncyclopediaCount <= 0)
                    {
                        encyclopedia.SetRewardAllowed();
                        await _sender.Send(new UpdateCharacterEncyclopediaCommand(encyclopedia));
                    }
                }
            }

            // ------------------------------------------------------------------------------------------------------

            client.Send(new SystemMessagePacket($"Encyclopedia verifyed and updated !!"));
        }

        private async Task PvpCommand(GameClient client, string[] command)
        {
            string message = string.Join(" ", command);

            var regex = @"pvp\s+(on|off)";
            var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket($"Unknown command.\nType !pvp (on/off)"));
                return;
            }

            if (client.Tamer.InBattle)
            {
                client.Send(new SystemMessagePacket($"You can't turn off pvp on battle !"));
                return;
            }

            string action = match.Groups[1].Value.ToLower();

            switch (action)
            {
                case "on":
                    {
                        if (client.Tamer.PvpMap == false)
                        {
                            client.Tamer.PvpMap = true;
                            client.Send(new NoticeMessagePacket($"PVP turned on !!"));
                        }
                        else
                        {
                            client.Send(new NoticeMessagePacket($"PVP is already on ..."));
                        }
                    }
                    break;

                case "off":
                    {
                        if (client.Tamer.PvpMap == true)
                        {
                            client.Tamer.PvpMap = false;
                            client.Send(new NoticeMessagePacket($"PVP turned off !!"));
                        }
                        else
                        {
                            client.Send(new NoticeMessagePacket($"PVP is already off ..."));
                        }
                    }
                    break;
            }
        }

        private async Task MagneticCommand(GameClient client, string[] command)
        {
            if (command.Length < 3)
            {
                client.Send(new SystemMessagePacket($"Unknown command.\nType !magnetic (cracked|attribute|gear|digieggs|seal) (on|off)"));
                return;
            }

            string subCommand = command[1].ToLower();
            string action = command[2].ToLower();

            if (action != "on" && action != "off")
            {
                client.Send(new SystemMessagePacket($"Invalid action. Use 'on' or 'off'."));
                return;
            }

            bool isEnabled = action == "on";

            switch (subCommand)
            {
                case "cracked":
                    // Toggle collection of items with section 8000 & 9100
                    client.Tamer.MagneticCracked = isEnabled;
                    client.Send(new NoticeMessagePacket($"Magnetic Cracked Aura turned {action}!"));
                    _logger.Information($"Tamer {client.Tamer.Name} set Magnetic Cracked Aura to {action}");
                    break;

                case "attribute":
                    // Toggle collection of items with section 12200, 12700, 12300, 12400, 17000
                    client.Tamer.MagneticAttribute = isEnabled;
                    client.Send(new NoticeMessagePacket($"Magnetic Attribute Aura turned {action}!"));
                    _logger.Information($"Tamer {client.Tamer.Name} set Magnetic Attribute Aura to {action}");
                    break;

                case "gear":
                    // Toggle collection of items with section 2901, 2902, 17000, 3001, 3002
                    client.Tamer.MagneticGear = isEnabled;
                    client.Send(new NoticeMessagePacket($"Magnetic Gear Aura turned {action}!"));
                    _logger.Information($"Tamer {client.Tamer.Name} set Magnetic Gear Aura to {action}");
                    break;

                case "digieggs":
                    // Toggle collection of items with section 9200, 9300, 9400, 9100
                    client.Tamer.MagneticDigiEggs = isEnabled;
                    client.Send(new NoticeMessagePacket($"Magnetic DigiEggs Aura turned {action}!"));
                    _logger.Information($"Tamer {client.Tamer.Name} set Magnetic DigiEggs Aura to {action}");
                    break;

                case "seal":
                    // Toggle collection of items with section 19000
                    client.Tamer.MagneticSeal = isEnabled;
                    client.Send(new NoticeMessagePacket($"Magnetic Seal Aura turned {action}!"));
                    _logger.Information($"Tamer {client.Tamer.Name} set Magnetic Seal Aura to {action}");
                    break;

                default:
                    client.Send(new SystemMessagePacket($"Invalid subcommand. Use 'cracked', 'attribute', 'gear', 'digieggs', or 'seal'."));
                    break;
            }
        }

        private async Task RaidsTimeCommand(GameClient client, string[] command)
        {
            string message = string.Join(" ", command);

            var regex = @"^raidstime\s+(file|lost|silent|silver|tva|stadium|odaiba|shibuya|minato|big|valley|all)\s*$";
            var match = Regex.Match(message, regex, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                client.Send(new SystemMessagePacket("Unknown command.\nType !raidstime <area> where area is one of:\nFile, Lost, Silent, Silver, TVA, Stadium, Odaiba, Shibuya, Minato, Big, Valley, All"));
                return;
            }

            string area = match.Groups[1].Value.ToLower();

            // Define raid boss types for each area
            Dictionary<string, int[]> areaToMobTypes = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "file", new[] { 74112, 45151 } },
                { "lost", new[] { 45153 } },
                { "silent", new[] { 45155, 45154 } },
                { "silver", new[] { 45150 } },
                { "tva", new[] { 66919 } },
                { "stadium", new[] { 74024 } },
                { "odaiba", new[] { 75137, 75136 } },
                { "shibuya", new[] { 75070, 75042, 75030 } },
                { "minato", new[] { 75096 } },
                { "big", new[] { 75128, 75129 } },
                { "valley", new[] { 75023, 75013 } }
            };

            // Get raid information for specified area
            List<(string Name, DateTime? ResurrectionTime, byte Channel, int Type, bool IsAlive)> raidInfo = new();

           // _logger.Information($"[RaidsTime] Searching for {area} raids");

            // STEP 1: First check in-memory loaded maps (for real-time data)
            foreach (var map in _mapServer.Maps)
            {
                SearchRaidsInMap(map, area, areaToMobTypes, ref raidInfo);
            }

            // STEP 2: If no raids found in memory and not searching all, query the database directly
            if (raidInfo.Count == 0 && area != "all")
            {
                //_logger.Information($"[RaidsTime] No loaded maps with {area} raids found. Querying database directly.");

                try
                {
                    // Get relevant map IDs for the area
                    var mapIds = GetRelevantMapIds(area);

                    foreach (var mapId in mapIds)
                    {
                        // Get the map config ID first
                        var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(mapId));

                        if (mapConfig != null)
                        {
                            // Get channel information for this map
                            var channelCount = UtilitiesFunctions.DefaultMapChannelsCount;

                            // Get the mobs for this map
                            var mapMobs = await _sender.Send(new MapMobConfigsQuery(mapConfig.Id));

                            if (mapMobs != null && mapMobs.Any())
                            {
                                foreach (var mob in mapMobs)
                                {
                                    // Check if this mob is a raid we're looking for
                                    if (areaToMobTypes.TryGetValue(area, out var mobTypes) && mobTypes.Contains(mob.Type))
                                    {
                                        // For each channel, add an entry - starting from 0 to match game's channel numbering
                                        for (byte channel = 0; channel < channelCount; channel++)
                                        {
                                            bool isAlive = mob.ResurrectionTime == null || mob.ResurrectionTime <= DateTime.Now;

                                            // Create an entry for this mob on this channel
                                            raidInfo.Add((
                                                Name: string.IsNullOrEmpty(mob.Name) ? $"Raid #{mob.Type}" : mob.Name,
                                                ResurrectionTime: mob.ResurrectionTime,
                                                Channel: channel,
                                                Type: mob.Type,
                                                IsAlive: isAlive
                                            ));
                                        }

                                        //_logger.Information($"[RaidsTime] Found raid from database: {mob.Name} (Type: {mob.Type})");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[RaidsTime] Error querying database: {ex.Message}");
                }
            }

            if (raidInfo.Count == 0)
            {
                client.Send(new SystemMessagePacket($"No raid information available for {area}.\n" +
                    "Possible reasons:\n" +
                    "1. The raid bosses haven't spawned yet\n" +
                    "2. The raid bosses have been defeated recently and will respawn later"));
                return;
            }

            // Group identical mobs within same channel to make output more compact
            var compactRaidInfo = raidInfo
                .GroupBy(r => new { r.Channel, r.Type, r.IsAlive, StatusTime = r.IsAlive ? DateTime.MinValue : r.ResurrectionTime })
                .Select(g => new
                {
                    Channel = g.Key.Channel,
                    Type = g.Key.Type,
                    Name = g.First().Name,
                    IsAlive = g.Key.IsAlive,
                    ResurrectionTime = g.Key.StatusTime,
                    Count = g.Count()
                })
                .OrderByDescending(r => r.IsAlive)
                .ThenBy(r => r.Channel)
                .ThenBy(r => r.ResurrectionTime)
                .ToList();

            // Build the message
            StringBuilder sb = new StringBuilder();

            if (area == "all")
                sb.AppendLine("Raid times for all areas:");
            else
                sb.AppendLine($"Raid times for {area}:");

            // Group raids by channel for cleaner output
            var raidsByChannel = compactRaidInfo.GroupBy(r => r.Channel);

            foreach (var channelGroup in raidsByChannel.OrderBy(g => g.Key))
            {
                sb.AppendLine($"Ch {channelGroup.Key}:");

                foreach (var raid in channelGroup)
                {
                    string status;
                    if (raid.IsAlive)
                    {
                        status = "Ready";
                    }
                    else if (raid.ResurrectionTime.HasValue)
                    {
                        TimeSpan timeLeft = raid.ResurrectionTime.Value - DateTime.Now;
                        status = FormatCompactTimeSpan(timeLeft);
                    }
                    else
                    {
                        status = "Ready"; // Instead of "Unknown", assume available
                    }

                    string nameDisplay = raid.Name;
                    if (nameDisplay.Length > 15)
                    {
                        // Truncate long names to save space
                        nameDisplay = nameDisplay.Substring(0, 12) + "...";
                    }

                    // Show multiple instances of the same raid with a count
                    string countDisplay = raid.Count > 1 ? $" x{raid.Count}" : "";

                    sb.AppendLine($"• {nameDisplay}{countDisplay}: {status}");
                }
            }

            // If the message is too long, split it
            string finalMessage = sb.ToString();
            if (finalMessage.Length > 1000)
            {
                var parts = SplitMessage(finalMessage, 1000);
                foreach (var part in parts)
                {
                    client.Send(new SystemMessagePacket(part));
                }
            }
            else
            {
                client.Send(new SystemMessagePacket(finalMessage));
            }

            _logger.Information($"[RaidsTime] Found {raidInfo.Count} raids for {area}");
        }

        // Shorter time format to save space
        private string FormatCompactTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds <= 0)
                return "Ready";

            return timeSpan.TotalHours >= 1
                ? $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m"
                : $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        // Split long messages into smaller chunks
        private List<string> SplitMessage(string message, int maxLength)
        {
            List<string> parts = new List<string>();

            while (message.Length > maxLength)
            {
                // Find a good breaking point (newline)
                int splitPoint = message.LastIndexOf('\n', maxLength);
                if (splitPoint <= 0) splitPoint = maxLength;

                parts.Add(message.Substring(0, splitPoint));
                message = message.Substring(splitPoint).TrimStart();
            }

            if (message.Length > 0)
                parts.Add(message);

            return parts;
        }

        // Helper method to search raids in a map
        private void SearchRaidsInMap(GameMap map, string area, Dictionary<string, int[]> areaToMobTypes, ref List<(string Name, DateTime? ResurrectionTime, byte Channel, int Type, bool IsAlive)> raidInfo)
        {
            foreach (var mob in map.Mobs)
            {
                bool isRelevant = false;

                // For "all" option, include all mob types from our dictionary
                if (area == "all")
                {
                    isRelevant = areaToMobTypes.Values.Any(types => types.Contains(mob.Type));
                }
                // For specific areas, check if the mob type matches what we're looking for
                else if (areaToMobTypes.TryGetValue(area, out var mobTypes))
                {
                    isRelevant = mobTypes.Contains(mob.Type);
                }

                // Only add relevant mobs to our list
                if (isRelevant)
                {
                    bool isAlive = !mob.Dead && !mob.ResurrectionTime.HasValue;
                    raidInfo.Add((mob.Name, mob.ResurrectionTime, map.Channel, mob.Type, isAlive));
                }
            }
        }

        // Helper method to get relevant map IDs for each area
        private List<int> GetRelevantMapIds(string area)
        {
            switch (area.ToLower())
            {
                case "file": return new List<int> { 1305 };
                case "lost": return new List<int> { 1304 };
                case "silent": return new List<int> { 1303 };
                case "silver": return new List<int> { 1302 };
                case "tva": return new List<int> { 9863 };
                case "stadium": return new List<int> { 9862 };
                case "odaiba": return new List<int> { 208 };
                case "shibuya": return new List<int> { 202 };
                case "minato": return new List<int> { 206 };
                case "big": return new List<int> { 207 };
                case "valley": return new List<int> { 201 };
                default: return new List<int>();
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds <= 0)
                return "Available now";

            return timeSpan.TotalHours >= 1
                ? $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s"
                : $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        // --- HELP ---------------------------------------------------------------
        private async Task HelpCommand(GameClient client, string[] command)
        {
            if (command.Length == 1)
            {
                client.Send(new SystemMessagePacket("Commands:\n1. !clear\n2. !stats\n3. !time\nType !help {command} for more details.", ""));
            }
            else if (command.Length == 2)
            {
                if (command[1] == "inv")
                {
                    client.Send(new SystemMessagePacket("Command !clear inv: Clear your inventory"));
                }
                else if (command[1] == "cash")
                {
                    client.Send(new SystemMessagePacket("Command !clear cash: Clear your CashStorage"));
                }
                else if (command[1] == "gift")
                {
                    client.Send(new SystemMessagePacket("Command !clear gift: Clear your GiftStorage"));
                }
                else if (command[1] == "stats")
                {
                    client.Send(new SystemMessagePacket("Command !stats: Show hidden stats"));
                }
                else if (command[1] == "time")
                {
                    client.Send(new SystemMessagePacket("Command !time: Shows the server time"));
                }
                else if (command[1] == "pvp")
                {
                    client.Send(new SystemMessagePacket("Command !pvp (on/off): Turn on/off pvp mode"));
                }
                else if (command[1] == "magnetic")
                {
                    client.Send(new SystemMessagePacket("Commands:\n1. !magnetic\nType !help {command} for more details.", ""));
                }
                else if (command[1] == "raidstime")
                {
                    client.Send(new SystemMessagePacket("Command !raidstime <area>: Shows raid boss resurrection times for the specified area.\nAvailable areas: File, Lost, Silent, Silver, TVA, Stadium, Odaiba, Shibuya, Minato, Big, Valley"));
                }
                else
                {
                    client.Send(new SystemMessagePacket("Invalid Command !! Type !help to see the commands.", ""));
                }
            }
            else
            {
                client.Send(new SystemMessagePacket("Invalid Command !! Type !help"));
            }
        }

        #endregion

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
