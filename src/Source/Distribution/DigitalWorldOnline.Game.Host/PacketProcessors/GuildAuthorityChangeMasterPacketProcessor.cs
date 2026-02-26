using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class GuildAuthorityChangeMasterPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.GuildAuthorityChangeToMaster;

        private readonly MapServer _mapServer;
        private readonly EventServer _eventServer;
        private readonly DungeonsServer _dungeonsServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public GuildAuthorityChangeMasterPacketProcessor(
            MapServer mapServer,
            EventServer eventServer,
            DungeonsServer dungeonsServer,
            PvpServer pvpServer,
            ILogger logger,
            ISender sender,
            IMapper mapper)
        {
            _mapServer = mapServer;
            _eventServer = eventServer;
            _dungeonsServer = dungeonsServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var targetName = packet.ReadString();
            var targetClient = _mapServer.FindClientByTamerName(targetName) ??
                               _dungeonsServer.FindClientByTamerName(targetName) ??
                               _eventServer.FindClientByTamerName(targetName);

            _logger.Information($"Searching character by name {targetName}...");
            var targetCharacter = await _sender.Send(new CharacterByNameQuery(targetName));
            if (targetCharacter == null)
            {
                _logger.Warning($"Character not found with name {targetName}.");
                client.Send(new SystemMessagePacket($"Character not found with name {targetName}."));
                return;
            }

            if (targetClient == null)
            {
                client.Send(new SystemMessagePacket($" {targetClient} <Target Client not found"));
                return;
            }

            _logger.Information($"Querying guild with TamerId: {targetClient.TamerId}");
            var targetGuild = _mapper.Map<GuildModel>(await _sender.Send(new GuildByCharacterIdQuery(targetClient.TamerId)));
            _logger.Information($"Result: {targetGuild?.Name ?? "NULL"}");

            if (targetGuild == null)
            {
                _logger.Warning($"Character {targetName} does not belong to a guild.");
                client.Send(new SystemMessagePacket($"Character {targetName} does not belong to a guild."));
                return;
            }
            _logger.Information($"Searching character by name {targetGuild.Name}...");
            foreach (var guildMember in targetGuild.Members)
            {
                _logger.Information($"This is Guildmember checks {guildMember.Id}");
                if (guildMember.CharacterInfo == null)
                {
                    var guildMemberClient = _mapServer.FindClientByTamerId(guildMember.CharacterId);
                    if (guildMemberClient != null)
                    {
                        guildMember.SetCharacterInfo(guildMemberClient.Tamer);
                    }
                    else
                    {
                        guildMember.SetCharacterInfo(
                            _mapper.Map<CharacterModel>(
                                await _sender.Send(new CharacterByIdQuery(guildMember.CharacterId))));
                    }
                }
            }

            _logger.Information($"Finding member with Character ID: {targetCharacter.Id}");

            var targetMember = targetGuild.FindMember(targetClient.TamerId);
            if (targetMember != null)
            {
                var newAuthority = GuildAuthorityTypeEnum.Master;
                var currentMaster = targetGuild.Members.FirstOrDefault(m => m.Authority == GuildAuthorityTypeEnum.Master);

                if (currentMaster != null)
                {
                    _logger.Information($"Demoting previous master {currentMaster.CharacterId} to normal member.");

                    // Change their authority to Member
                    currentMaster.SetAuthority(GuildAuthorityTypeEnum.Member);

                    // Save the update to the database
                    await _sender.Send(new UpdateGuildMemberAuthorityCommand(currentMaster));
                }

                targetMember.SetAuthority(newAuthority);
                var newEntry = targetGuild.AddHistoricEntry((GuildHistoricTypeEnum)newAuthority, targetGuild.Master,
                    targetMember);
                _logger.Information($"Inside Target character...");
                targetGuild.Members
                    .ForEach(guildMember =>
                    {
                        _logger.Debug(
                            $"Sending guild authority change packet for character {guildMember.CharacterId}...");
                        _mapServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                            new GuildPromotionDemotionPacket(packet.Type, targetName,
                                targetGuild.FindAuthority(newAuthority).Duty).Serialize());
                        _dungeonsServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                            new GuildPromotionDemotionPacket(packet.Type, targetName,
                                targetGuild.FindAuthority(newAuthority).Duty).Serialize());
                        _eventServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                            new GuildPromotionDemotionPacket(packet.Type, targetName,
                                targetGuild.FindAuthority(newAuthority).Duty).Serialize());
                        _pvpServer.BroadcastForUniqueTamer(guildMember.CharacterId,
                            new GuildPromotionDemotionPacket(packet.Type, targetName,
                                targetGuild.FindAuthority(newAuthority).Duty).Serialize());
                    });

                _logger.Debug($"Saving historic entry for guild {targetGuild.Id}...");
                await _sender.Send(new CreateGuildHistoricEntryCommand(newEntry, targetGuild.Id));

                _logger.Debug($"Updating member authority for member {targetMember.Id} and guild {targetGuild.Id}...");
                await _sender.Send(new UpdateGuildMemberAuthorityCommand(targetMember));
            }
        }
    }
}