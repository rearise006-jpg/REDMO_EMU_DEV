using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Chat;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Game.Commands;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class TamerOptionPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TamerOption;

        private readonly GameMasterCommandsProcessor _gmCommands;
        private readonly PlayerCommandsProcessor _playerCommands;
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public TamerOptionPacketProcessor(
            GameMasterCommandsProcessor gmCommands,
            MapServer mapServer,
            ILogger logger,
            ISender sender, DungeonsServer dungeonServer, PlayerCommandsProcessor playerCommands)
        {
            _gmCommands = gmCommands;
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _logger = logger;
            _sender = sender;
            _playerCommands = playerCommands;

        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            var option = packet.ReadInt();
            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new TamerOptionPacket(client.Tamer.GeneralHandler, option).Serialize());

        }
    }
}