using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class RandomMonsterEndPacket : PacketWriter
    {
        private const int PacketNumber = 16006;

        public RandomMonsterEndPacket(int attackerType, int mapId)
        {
            Type(PacketNumber);
            Type(1609);
            WriteInt(attackerType); // MonIDX
            WriteInt(mapId);        // MapID
        }
    }
}