using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using Serilog;
using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Application_Game.Separar.Queries;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class InitialInformationPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.InitialInformation;

        private readonly PartyManager _partyManager;
        private readonly StatusManager _statusManager;
        private readonly MapServer _mapServer;
        private readonly PvpServer _pvpServer;
        private readonly EventServer _eventServer;
        private readonly DungeonsServer _dungeonsServer;

        private readonly AssetsLoader _assets;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public InitialInformationPacketProcessor(
            PartyManager partyManager,
            StatusManager statusManager,
            MapServer mapServer,
            PvpServer pvpServer,
            EventServer eventServer,
            DungeonsServer dungeonsServer,
            AssetsLoader assets,
            ILogger logger,
            ISender sender,
            IMapper mapper)
        {
            _partyManager = partyManager;
            _statusManager = statusManager;
            _mapServer = mapServer;
            _pvpServer = pvpServer;
            _eventServer = eventServer;
            _dungeonsServer = dungeonsServer;
            _assets = assets;
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var gamePort = packet.ReadInt();
            var accountId = packet.ReadUInt();
            var accessCode = packet.ReadUInt();

            try
            {
                var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(accountId)));
                client.SetAccountInfo(account);

                if (account.IsOnline == 0 && client.IsConnected)
                {
                    _sender.Send(new UpdateAccountStatusCommand(client.AccountId, 1));
                }

                CharacterModel? character = _mapper.Map<CharacterModel>(
                        await _sender.Send(new CharacterByIdQuery(account.LastPlayedCharacter)));

                if (character == null || character.Partner == null)
                {
                    _logger.Error($"Invalid character information for tamer id {account.LastPlayedCharacter}.");
                    return;
                }

                account.ItemList.ForEach(character.AddItemList);

                foreach (var digimon in character.Digimons)
                {
                    digimon.SetTamer(character);

                    digimon.SetBaseInfo(_statusManager.GetDigimonBaseInfo(digimon.CurrentType));
                    digimon.SetBaseStatus(_statusManager.GetDigimonBaseStatus(digimon.CurrentType, digimon.Level, digimon.Size));
                    digimon.SetTitleStatus(_statusManager.GetTitleStatus(character.CurrentTitle));
                    digimon.SetSealStatus(_assets.SealInfo);
                }

                var tamerLevelStatus = _statusManager.GetTamerLevelStatus(character.Model, character.Level);

                character.SetBaseStatus(_statusManager.GetTamerBaseStatus(character.Model));
                character.SetLevelStatus(tamerLevelStatus);

                character.NewViewLocation(character.Location.X, character.Location.Y);
                character.NewLocation(character.Location.MapId, character.Location.X, character.Location.Y);
                character.Partner.NewLocation(character.Location.MapId, character.Location.X, character.Location.Y);

                character.RemovePartnerPassiveBuff();
                character.SetPartnerPassiveBuff();
                character.Partner.SetTamer(character);

                await _sender.Send(new UpdateDigimonBuffListCommand(character.Partner.BuffList));

                if (character.Partner.CurrentType == character.Partner.BaseType)
                {
                    character.ActiveEvolution.SetDs(0);
                    character.ActiveEvolution.SetXg(0);

                    await _sender.Send(new UpdateCharacterActiveEvolutionCommand(character.ActiveEvolution));
                }

                foreach (var item in character.ItemList.SelectMany(x => x.Items).Where(x => x.ItemId > 0))
                    item.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(item.ItemId));

                foreach (var buff in character.BuffList.ActiveBuffs)
                    buff.SetBuffInfo(_assets.BuffInfo.FirstOrDefault(x =>
                        x.SkillCode == buff.SkillId || x.DigimonSkillCode == buff.SkillId));

                foreach (var buff in character.Partner.BuffList.ActiveBuffs)
                    buff.SetBuffInfo(_assets.BuffInfo.FirstOrDefault(x =>
                        x.SkillCode == buff.SkillId || x.DigimonSkillCode == buff.SkillId));

                // Get MasterMatch Info
                byte masterMatchTeam = 0;

                MastersMatchRankerDTO masterMatchRanker = await _sender.Send(new GetMasterMatchRankerDataQuery(character.Id));

                if (masterMatchRanker != null)
                {
                    masterMatchTeam = (byte)masterMatchRanker.Team;
                }

                // Get Channels for normal maps
                if (!client.DungeonMap)
                {
                    var channels = (Dictionary<byte, byte>)await _sender.Send(new ChannelByMapIdQuery(character.Location.MapId));
                    var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(character.Location.MapId));

                    byte targetChannel = UtilitiesFunctions.GetTargetChannel(character.Channel, channels, (byte)mapConfig.Channels);
                    character.SetCurrentChannel(targetChannel);
                }

                character.UpdateState(CharacterStateEnum.Loading);
                client.SetCharacter(character);

                client.SetSentOnceDataSent(character.InitialPacketSentOnceSent);

                await _sender.Send(new UpdateCharacterStateCommand(character.Id, CharacterStateEnum.Loading));

                // AddClient
                if (client.DungeonMap)
                {
                    client.Tamer.SetCurrentChannel(0);

                    _logger.Information($"Adding Tamer {character.Id}:{character.Name} to map {character.Location.MapId} Ch {character.Channel}... (Dungeon Map)");

                    await _dungeonsServer.AddClient(client);
                }
                else if (client.EventMap)
                {
                    _logger.Information($"Adding Tamer {character.Id}:{character.Name} to map {character.Location.MapId} Ch {character.Channel}... (Event Map)");

                    await _eventServer.AddClient(client);
                }
                else if (client.PvpMap)
                {
                    _logger.Information($"Adding Tamer {character.Id}:{character.Name} to map {character.Location.MapId} Ch {character.Channel}... (Pvp Map)");

                    await _pvpServer.AddClient(client);
                }
                else
                {
                    _logger.Information($"Adding Tamer {character.Id}:{character.Name} to map {character.Location.MapId} Ch {character.Channel}... (Default Map)");

                    await _mapServer.AddClient(client);
                    await _mapServer.EnsureConsignedShopsLoaded(character.Location.MapId, character.Channel);
                }

                while (client.Loading)
                    await Task.Delay(1000);

                character.SetGenericHandler(character.Partner.GeneralHandler);

                if (!client.DungeonMap)
                {
                    var region = _assets.Maps.FirstOrDefault(x => x.MapId == character.Location.MapId);

                    if (region != null)
                    {
                        if (character.MapRegions[region.RegionIndex].Unlocked != 0x80)
                        {
                            var characterRegion = character.MapRegions[region.RegionIndex];
                            characterRegion.Unlock();

                            await _sender.Send(new UpdateCharacterMapRegionCommand(characterRegion));
                        }
                    }
                }

                // DungeonDoors
                var mapObject = _assets.MapObjects.FirstOrDefault(mo => mo.MapId == client.Tamer.Location.MapId);

                client.Send(new InitialInfoPacket(character, null, masterMatchTeam, mapObject));

                var party = _partyManager.FindParty(client.TamerId);

                if (party != null)
                {
                    party.UpdateMember(party[client.TamerId], character);

                    var firstMemberLocation =
                        party.Members.Values.FirstOrDefault(x => x.Location.MapId == client.Tamer.Location.MapId);

                    if (firstMemberLocation != null)
                    {
                        character.SetCurrentChannel(firstMemberLocation.Channel);
                        client.Tamer.SetCurrentChannel(firstMemberLocation.Channel);
                    }

                    foreach (var target in party.Members.Values.Where(x => x.Id != client.TamerId))
                    {
                        var targetClient = _mapServer.FindClientByTamerId(target.Id);
                        if (targetClient == null) targetClient = _dungeonsServer.FindClientByTamerId(target.Id);

                        if (targetClient == null) continue;

                        KeyValuePair<byte, CharacterModel> partyMember =
                            party.Members.FirstOrDefault(x => x.Value.Id == client.TamerId);

                        targetClient.Send(
                            UtilitiesFunctions.GroupPackets(
                                new PartyMemberWarpGatePacket(partyMember, targetClient.Tamer).Serialize(),
                                new PartyMemberMovimentationPacket(partyMember).Serialize()
                            ));
                    }
                    await Task.Delay(600);

                    client.Send(new PartyMemberListPacket(party, character.Id));
                }

                await ReceiveArenaPoints(client);

                await _sender.Send(new ChangeTamerIdTPCommand(client.Tamer.Id, (int)0));
                await _sender.Send(new UpdateCharacterChannelCommand(character.Id, character.Channel));

                //_logger.Information($"***********************************************************************");
            }
            catch (Exception ex)
            {
                _logger.Error($"[InitialInformationPacketProcessor] :: {ex.Message}");
                _logger.Error($"Inner Stacktrace: {ex.ToString()}");
                _logger.Error($"Stacktrace: {ex.StackTrace}");

                client.Disconnect();
            }
        }

        private async Task ReceiveArenaPoints(GameClient client)
        {
            if (client.Tamer.Points.Amount > 0)
            {
                var newItem = new ItemModel();
                newItem.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(client.Tamer.Points.ItemId));

                newItem.ItemId = client.Tamer.Points.ItemId;
                newItem.Amount = client.Tamer.Points.Amount;

                if (newItem.IsTemporary)
                    newItem.SetRemainingTime((uint)newItem.ItemInfo.UsageTimeMinutes);

                var itemClone = (ItemModel)newItem.Clone();

                if (client.Tamer.Inventory.AddItem(newItem))
                {
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                }
                else
                {
                    newItem.EndDate = DateTime.Now.AddDays(7);

                    client.Tamer.GiftWarehouse.AddItem(newItem);
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.GiftWarehouse));
                }

                client.Tamer.Points.SetAmount(0);
                client.Tamer.Points.SetCurrentStage(0);

                await _sender.Send(new UpdateCharacterArenaPointsCommand(client.Tamer.Points));
            }
            else if (client.Tamer.Points.CurrentStage > 0)
            {
                client.Tamer.Points.SetCurrentStage(0);
                await _sender.Send(new UpdateCharacterArenaPointsCommand(client.Tamer.Points));
            }
        }

        public List<CharacterModel> GetDuplicateTamers(DigitalWorldOnline.Commons.Entities.GameServer server)
        {
            var clientsWithAccountIds = server.Clients
                .Where(client => client.AccountId > 0)
                .ToList();

            var accountCounts = clientsWithAccountIds.GroupBy(client => client.AccountId)
                                                     .ToDictionary(g => g.Key, g => g.Count());

            var groupedClients = clientsWithAccountIds
                .GroupBy(client => client.AccountId)
                .Where(g => g.Count() > 1)
                .ToList();

            return groupedClients.SelectMany(g => g.Select(client => client.Tamer)).Where(t => t != null).ToList();
        }

    }
}