using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DigimonTranscendenceReceiveExpPacket : PacketWriter
    {
        private const int PacketNumber = 16039;

        public DigimonTranscendenceReceiveExpPacket(int result, AcademyInputType inputType, byte targetSlot, short digimonCount, List<short> targetDeleteSlots,short itemSlot,ItemModel targetItem,short successRate,long chargeExp,long targetPartnerFinalExp)
        {
            Type(PacketNumber);
            WriteInt(result); // 0 = Sucess, 1 = Fail

            if (result == 0)
            {
                WriteByte((byte)inputType);
                WriteByte(targetSlot);
                WriteShort(digimonCount);

                foreach (var targetToDeleteSlot in targetDeleteSlots)
                {
                    WriteShort(targetToDeleteSlot);
                }

                WriteShort(1);                      // 2 bytes : DelItemCount
                WriteShort(itemSlot);               // 2 bytes : InvenIdx
                WriteBytes(targetItem.ToArray());   // 68 bytes : ItemData
                WriteInt(successRate);              // 4 bytes : SuccessRate
                WriteInt64(chargeExp);              // 8 bytes : ChargeEXP
                WriteInt64(targetPartnerFinalExp);  // 8 bytes : TotalEXP
            }
        }
    }
}