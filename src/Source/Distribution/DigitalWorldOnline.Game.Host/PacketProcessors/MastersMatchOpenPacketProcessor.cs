using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.GameHost.EventsServer;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using AutoMapper;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class MastersMatchOpenPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.MastersMatchOpen;

        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;

        private readonly Random rand = new Random();

        public MastersMatchOpenPacketProcessor(ILogger logger, ISender sender, IMapper mapper, MapServer mapServer, DungeonsServer dungeonServer, EventServer eventServer, PvpServer pvpServer)
        {
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var userID = packet.ReadInt();
            var npcId = packet.ReadInt();

            byte newTeam = 0;
            var randomValue = rand.Next(0, 3);

            if (randomValue > 1)
                newTeam = (byte)MastersMatchTeamEnum.B;
            else
                newTeam = (byte)MastersMatchTeamEnum.A;

            MastersMatchTeamEnum playerAssignedTeam = (MastersMatchTeamEnum)newTeam;

            var tamerName = client.Tamer.Name;
            var characterId = client.TamerId;

            MastersMatchDTO masterMatch = await _sender.Send(new GetMastersMatchDataQuery());

            if (masterMatch != null)
            {
                var teamADonations = masterMatch.TeamADonations;
                var teamBDonations = masterMatch.TeamBDonations;

                int myDonations = 0;
                short myRank = 0;
                byte myTeam = (byte)MastersMatchTeamEnum.None;

                var myRankerInfo = masterMatch.Rankers.FirstOrDefault(r => r.CharacterId == characterId);

                if (myRankerInfo != null)
                {
                    myDonations = myRankerInfo.Donations;
                    myRank = myRankerInfo.Rank;
                    myTeam = (byte)myRankerInfo.Team;
                }
                else
                {
                    myDonations = 0;
                    myRank = 0;
                    myTeam = (byte)playerAssignedTeam;

                    if (characterId != 0)
                    {
                        MastersMatchRankerDTO createdRanker = await _sender.Send(new CreateMasterMatchPlayerCommand(characterId, tamerName, playerAssignedTeam));

                        if (createdRanker != null)
                        {
                            masterMatch.Rankers.Add(createdRanker);
                        }
                        else
                        {
                            _logger.Error($"Invalid CharacterId (0) for tamer: {tamerName}. Cannot register player for Masters Match.");
                            return;
                        }
                    }
                    else
                    {
                        _logger.Error($"Invalid CharacterId (0) for tamer: {tamerName}. Cannot register player for Masters Match.");
                        return;
                    }
                }

                List<MastersMatchRankerDTO> top10TeamA = masterMatch.Rankers.Where(r => r.Team == MastersMatchTeamEnum.A).Take(10).ToList();
                List<MastersMatchRankerDTO> top10TeamB = masterMatch.Rankers.Where(r => r.Team == MastersMatchTeamEnum.B).Take(10).ToList();

                client.Send(new MastersMatchOpenPacket(teamADonations, teamBDonations, myDonations, myRank, myTeam, top10TeamA, top10TeamB));
            }
        }
    }
}