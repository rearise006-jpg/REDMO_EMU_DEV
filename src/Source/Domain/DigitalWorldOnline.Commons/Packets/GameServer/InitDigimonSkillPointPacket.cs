using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class InitDigimonSkillPointPacket : PacketWriter
    {
        private const int PacketNumber = 3213;

        public InitDigimonSkillPointPacket(DigimonModel partnerEvolution)
        {
            Type(PacketNumber);
            WriteByte((byte)partnerEvolution.Evolutions.Count);

            for (int i = 0; i < partnerEvolution.Evolutions.Count; i++)
            {
                var form = partnerEvolution.Evolutions[i];
                WriteBytes(form.ToArray());
            }
        }
    }
}