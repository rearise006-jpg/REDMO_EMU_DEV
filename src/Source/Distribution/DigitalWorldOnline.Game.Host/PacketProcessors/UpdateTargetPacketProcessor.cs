using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class UpdateTargetPacketProcessor : IGamePacketProcessor
    {
        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;

        private readonly ILogger _logger;
        private readonly ISender _sender;

        public GameServerPacketEnum Type => GameServerPacketEnum.UpdateTarget;

        public UpdateTargetPacketProcessor(MapServer mapServer, DungeonsServer dungeonServer, EventServer eventServer, PvpServer pvpServer, ILogger logger, ISender sender)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
        }

        public Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var attackerHandler = packet.ReadInt();
            var targetHandler = packet.ReadInt();

            //_logger.Information($"attackerHandler: {attackerHandler} | targetHandler: {targetHandler}");

            client.Tamer.UpdateTargetHandler(targetHandler);

            if (client.DungeonMap)
            {
                var isNormalMob = _dungeonServer.GetIMobByHandler(targetHandler, client.TamerId, false);
                var isSummonMob = _dungeonServer.GetIMobByHandler(targetHandler, client.TamerId, true);

                if (isNormalMob != null)
                {
                    client.Send(new MobTargetPacket(isNormalMob.GeneralHandler, isNormalMob.Level, isNormalMob.CurrentHpRate,
                        isNormalMob.TargetSummonHandler, isNormalMob.GetStartTimeUnixTimeSeconds(), isNormalMob.GetEndTimeUnixTimeSeconds()));
                }
                else if (isSummonMob != null)
                {
                    client.Send(new MobTargetPacket(isSummonMob.GeneralHandler, isSummonMob.Level, isSummonMob.CurrentHpRate,
                        isSummonMob.TargetSummonHandler, isSummonMob.GetStartTimeUnixTimeSeconds(), isSummonMob.GetEndTimeUnixTimeSeconds()));
                }
            }
            else
            {
                var isNormalMob = _mapServer.GetIMobByHandler(targetHandler, client.TamerId, false);
                var isSummonMob = _mapServer.GetIMobByHandler(targetHandler, client.TamerId, true);

                if (isNormalMob != null)
                {
                    client.Send(new MobTargetPacket(isNormalMob.GeneralHandler, isNormalMob.Level, isNormalMob.CurrentHpRate,
                        isNormalMob.TargetSummonHandler, isNormalMob.GetStartTimeUnixTimeSeconds(), isNormalMob.GetEndTimeUnixTimeSeconds()));
                }
                else if (isSummonMob != null)
                {
                    client.Send(new MobTargetPacket(isSummonMob.GeneralHandler, isSummonMob.Level, isSummonMob.CurrentHpRate,
                        isSummonMob.TargetSummonHandler, isSummonMob.GetStartTimeUnixTimeSeconds(), isSummonMob.GetEndTimeUnixTimeSeconds()));
                }
            }

            return Task.CompletedTask;
        }
    }
}