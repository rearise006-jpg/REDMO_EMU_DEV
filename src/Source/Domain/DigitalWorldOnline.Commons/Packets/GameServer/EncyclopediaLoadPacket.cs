using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class EncyclopediaLoadPacket : PacketWriter
    {
        private const int PacketNumber = 3234;

        public EncyclopediaLoadPacket(List<DigimonModel> bestDigimons)
        {
            Type(PacketNumber);

            WriteInt(bestDigimons.Count);

            foreach (var digimon in bestDigimons)
            {
                ulong unlockedBits = 0;

                WriteInt(digimon.BaseType);
                WriteShort(digimon.Level);

                for (int j = 0; j < digimon.Evolutions.Count; j++)
                {
                    if (digimon.Evolutions[j].Unlocked > 0)
                        unlockedBits |= (1UL << j);
                }

                WriteInt64((long)unlockedBits);
                WriteShort(digimon.Digiclone.ATLevel);
                WriteShort(digimon.Digiclone.CTLevel);
                WriteShort(digimon.Digiclone.BLLevel);
                WriteShort(digimon.Digiclone.EVLevel);
                WriteShort(digimon.Digiclone.HPLevel);
                WriteShort(digimon.Size);
                WriteByte((byte)digimon.DeckRewardReceived);
            }
        }
    }
}