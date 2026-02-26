using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class ItemSocketClearPacket : PacketWriter
    {
        // Using a different packet number to avoid conflict with ItemSocketOutPacket
        private const int PacketNumber = 3928;

        public ItemSocketClearPacket(int Money)
        {
            Type(PacketNumber);
            // Client's RecvSocketClearSuccess calls g_pGameIF->GetEI_Attach()->RecvServerDelete()
            // Include money value for client to update currency display
            WriteInt(Money);
        }
    }
}