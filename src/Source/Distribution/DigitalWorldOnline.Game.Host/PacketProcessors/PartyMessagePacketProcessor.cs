using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Chat;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;
using System.Linq.Expressions;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class PartyMessagePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PartyMessage;

        private readonly PartyManager _partyManager;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public PartyMessagePacketProcessor(PartyManager partyManager, MapServer mapServer, DungeonsServer dungeonsServer,
            EventServer eventServer, PvpServer pvpServer, ILogger logger, ISender sender)
        {
            _partyManager = partyManager;
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var message = packet.ReadString();

            try
            {
                var party = _partyManager.FindParty(client.TamerId);

                if (party == null)
                {
                    client.Send(new SystemMessagePacket($"You need to be in a party to send party messages."));
                    return;
                }

                foreach (var memberId in party.GetMembersIdList())
                {
                    var targetMessage = _mapServer.FindClientByTamerId(memberId)
                        ?? _dungeonServer.FindClientByTamerId(memberId)
                        ?? _eventServer.FindClientByTamerId(memberId)
                        ?? _pvpServer.FindClientByTamerId(memberId);


                    if (targetMessage == null)
                    {
                        _logger.Warning($"Party member {memberId} not found in any server.");
                        continue;
                    }

                    if (targetMessage != null && targetMessage.IsConnected)
                    {
                        targetMessage.Send(new PartyMessagePacket(client.Tamer.Name, message).Serialize());
                    }
                    else
                    {
                        _logger.Warning($"Party member {memberId} is not connected or does not exist.");
                    }

                }

                await _sender.Send(new CreateChatMessageCommand(ChatMessageModel.Create(client.TamerId, message)));

            }
            catch (Exception ex)
            {
                _logger.Information(ex, "Error processing party message packet PartyMessagePacketProcessor.");
            }

        }
    }
}