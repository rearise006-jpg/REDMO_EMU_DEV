using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class PinochiGetInfoPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PinochiGetInfo;

        private readonly ISender _sender;

        public PinochiGetInfoPacketProcessor(ISender sender)
        {
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            client.Send(new PinochiGetInfoPacket(60, 0, 0));
        }
    }
}