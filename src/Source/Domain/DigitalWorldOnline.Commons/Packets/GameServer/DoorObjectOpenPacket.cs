using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class DoorObjectOpenPacket : PacketWriter
    {
        private const int PacketNumber = 16007;

        /// <summary>
        /// Destroys the target mob.
        /// </summary>
        /// <param name="mob">The mob to destroy.</param>
        public DoorObjectOpenPacket(int nFactID, byte door)
        {
            Type(PacketNumber);
            WriteInt(nFactID);
            WriteByte(door);
        }
    }
}
