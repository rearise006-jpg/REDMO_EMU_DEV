using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonTranscendenceSuccessPacket : PacketWriter
    {
        private const int PacketNumber = 16040;

        public DigimonTranscendenceSuccessPacket(int Result, byte targetSlot, DigimonHatchGradeEnum scale, long price, long tamerMoney, long exp)
        {
            Type(PacketNumber);
            WriteInt(Result);               // result: 0 - succes, 1 - fail

            if (Result == 0)
            {
                WriteByte(targetSlot);      // Digimon on Digivice Pos: 0, 1, 2, 3
                WriteByte((byte)scale);     // HatchLevel
                WriteInt64(price);          // Digimon Transcendence Cost
                WriteInt64(tamerMoney);     // Tamer Money
                WriteInt64(exp);            // Curent Exp
            }
        }
    }
}