using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class PartyRequestResponsePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PartyRequestResponse;

        private readonly PartyManager _partyManager;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;

        private readonly ILogger _logger;

        public PartyRequestResponsePacketProcessor(PartyManager partyManager, MapServer mapServer, DungeonsServer dungeonServer, EventServer eventServer, ILogger logger)
        {
            _partyManager = partyManager;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _eventServer = eventServer;
            _logger = logger;
        }

        public Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var inviteResult = packet.ReadInt();
            var leaderName = packet.ReadString();

            try
            {
                var leaderClient = _mapServer.FindClientByTamerName(leaderName) ??
                    _dungeonServer.FindClientByTamerName(leaderName) ??
                    _eventServer.FindClientByTamerName(leaderName);

                if (leaderClient == null)
                {
                    _logger.Error($"Unable to find party leader with name {leaderName}.");
                    client.Send(new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.CantAccept, client.Tamer.Name));

                    return Task.CompletedTask;
                }

                if (inviteResult == (int)PartyRequestFailedResultEnum.AlreadyInparty)
                {
                    leaderClient.Send(new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.AlreadyInparty, client.Tamer.Name));
                    return Task.CompletedTask;
                }
                else if (inviteResult == (int)PartyRequestFailedResultEnum.Rejected)
                {
                    leaderClient.Send(new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.Rejected, client.Tamer.Name));
                    return Task.CompletedTask;
                }
                else if (inviteResult == (int)PartyRequestFailedResultEnum.Disconnected)
                {
                    leaderClient.Send(new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.Disconnected, client.Tamer.Name));
                    return Task.CompletedTask;
                }
                else if (inviteResult == (int)PartyRequestFailedResultEnum.CantAccept)
                {
                    leaderClient.Send(new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.CantAccept, client.Tamer.Name));
                    return Task.CompletedTask;
                }

                var party = _partyManager.FindParty(leaderClient.TamerId);

                // Party doesnt exist yet, creating party
                if (party == null)
                {
                    party = _partyManager.CreateParty(leaderClient.Tamer, client.Tamer);

                    if (leaderClient.DungeonMap)
                    {
                        var targetMap = _dungeonServer.Maps.FirstOrDefault(x => x.DungeonId == leaderClient.TamerId);

                        if (targetMap != null)
                        {
                            targetMap.SetId(party.Id);
                        }
                    }

                    leaderClient.Send(
                        UtilitiesFunctions.GroupPackets(
                            new PartyCreatedPacket(party.Id, party.LootType).Serialize(),
                            new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.Accept, client.Tamer.Name).Serialize(),
                            new PartyMemberJoinPacket(party[client.TamerId], leaderClient.Tamer).Serialize(),
                            new PartyMemberInfoPacket(party[client.TamerId]).Serialize(),
                            new PartyMemberMovimentationPacket(party[client.TamerId]).Serialize()
                        ));

                    client.Send(new PartyMemberListPacket(party, client.TamerId));
                }
                // Party already exist, add tamer
                else
                {
                    party.AddMember(client.Tamer);

                    client.Send(new PartyMemberListPacket(party, client.TamerId, (byte)(party.Members.Count - 1)));
                    leaderClient.Send(new PartyRequestSentFailedPacket(PartyRequestFailedResultEnum.Accept, client.Tamer.Name));

                    foreach (var target in party.Members.Values)
                    {
                        var targetClient = _mapServer.FindClientByTamerId(target.Id) ??
                            _dungeonServer.FindClientByTamerId(target.Id) ??
                            _eventServer.FindClientByTamerId(target.Id);

                        //if (targetClient == null) targetClient = _dungeonServer.FindClientByTamerId(target.Id);

                        if (targetClient == null) continue;

                        if (target.Id != client.Tamer.Id)
                        {
                            targetClient.Send(new PartyMemberJoinPacket(party[client.TamerId], targetClient.Tamer));
                            targetClient.Send(new PartyMemberInfoPacket(party[client.TamerId]));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[PartyRequestResponse] :: {ex.Message}");
            }

            return Task.CompletedTask;
        }
    }
}