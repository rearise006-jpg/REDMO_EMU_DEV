using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.Items
{
    public class TamerOptionPacket : PacketWriter
    {
        private const int PacketNumber = 1313;

        public TamerOptionPacket(int tamerHandler, int option)
        {

            Type(PacketNumber);
            WriteInt(tamerHandler);
            WriteInt(option);
        }
    }
}