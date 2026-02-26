using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.AuthenticationServer
{
    public class LoginRequestAnswerPacket : PacketWriter
    {
        private const int PacketNumber = 3301;

        public LoginRequestAnswerPacket(LoginResultEnum result, SecondaryPasswordScreenEnum subType)
        {
            Type(PacketNumber);
            WriteInt((int)result);
            WriteByte((byte)subType);
        }
    }
}
