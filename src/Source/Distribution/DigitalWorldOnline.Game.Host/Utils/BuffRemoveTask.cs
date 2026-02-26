using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;

namespace DigitalWorldOnline.Game.Utils
{
    public class BuffRemoveTask
    {
        private readonly Timer _timer;
        private readonly GameClient _client;
        private readonly int _attackerHandler;
        private readonly int _buffId;
        private readonly IMapServer _server;

        public BuffRemoveTask(GameClient client, int attackerHandler, int buffId, int buffDuration, IMapServer server)
        {
            int delayMilliseconds = buffDuration;
            _client = client;
            _attackerHandler = attackerHandler;
            _buffId = buffId;
            _server = server;

            _timer = new Timer(Execute, null, delayMilliseconds, Timeout.Infinite);
        }

        private void Execute(object state)
        {
            _server.BroadcastForTamerViewsAndSelf(_client.TamerId, new RemoveBuffPacket(_attackerHandler, _buffId).Serialize());

            if(_client.Tamer.BuffList.ActiveBuffs.Any(x => x.BuffId == _buffId) )
                _client.Tamer.BuffList.Remove(_buffId);
            
            if (_client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffId == _buffId))
                _client.Partner.BuffList.Remove(_buffId);
            
            if(_client.Tamer.TargetMob != null && _client.Tamer.TargetMob.DebuffList.ActiveBuffs.Any(x => x.BuffId == _buffId))
                _client.Tamer.TargetMob.DebuffList.Remove(_buffId);
            
            _server.BroadcastForTamerViewsAndSelf(_client.TamerId, new UpdateStatusPacket(_client.Tamer).Serialize());

            _timer.Dispose();
        }
    }
}
