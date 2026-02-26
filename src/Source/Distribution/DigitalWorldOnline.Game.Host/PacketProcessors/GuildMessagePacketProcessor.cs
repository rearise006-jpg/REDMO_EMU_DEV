using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Chat;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;
using System.IO;
using DigitalWorldOnline.GameHost.EventsServer;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class GuildMessagePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.GuildMessage;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public GuildMessagePacketProcessor(MapServer mapServer, ILogger logger, ISender sender, DungeonsServer dungeonServer, EventServer eventServer)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _eventServer = eventServer;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var message = packet.ReadString();

            if (client.Tamer.Guild != null)
            {
                foreach (var memberId in client.Tamer.Guild.GetGuildMembersIdList())
                {
                    var targetMessage = _mapServer.FindClientByTamerId(memberId);
                    var targetDungeon = _dungeonServer.FindClientByTamerId(memberId);
                    var targetEvent = _eventServer.FindClientByTamerId(memberId);

                    if (targetMessage != null)
                        targetMessage.Send(new GuildMessagePacket(client.Tamer.Name, message).Serialize());

                    if (targetDungeon != null)
                        targetDungeon.Send(new GuildMessagePacket(client.Tamer.Name, message).Serialize());

                    if (targetEvent != null)
                        targetEvent.Send(new GuildMessagePacket(client.Tamer.Name, message).Serialize());
                }


                await _sender.Send(new CreateChatMessageCommand(ChatMessageModel.Create(client.TamerId, message)));
            }
            else
            {
                client.Send(new SystemMessagePacket($"You need to be in a guild to send guild messages."));
                //_logger.Warning($"Character {client.TamerId} sent guild message but was not in a guild.");
            }
        }
    }
}