using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class MsgRaidMonsterDiePacket : PacketWriter
    {
        private const int PacketNumber = 16006;

        public MsgRaidMonsterDiePacket(int targetHandler, string attackerName, int attackerType, int itemId)
        {
            Type(PacketNumber);
            Type(1605);
            WriteInt(targetHandler);      // nMonsterIdx
            WriteString(attackerName);      // wsTamerName
            WriteInt(attackerType);      // nDigimon
            WriteInt(itemId);         // nItemIDX
        }
    }
}
