using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DungeonFailPacket : PacketWriter
    {
        private const int PacketNumber = 4121;

        public DungeonFailPacket(int result)
        {
            Type(PacketNumber);
            WriteInt(result); // nResult
        }
    }
}
