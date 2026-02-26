using DigitalWorldOnline.Commons.Readers;

namespace DigitalWorldOnline.Game
{
    /// <summary>
    /// Extension methods for GamePacketReader to support additional data types
    /// </summary>
    public static class GamePacketReaderExtensions
    {
        /// <summary>
        /// Reads an unsigned short (ushort) from the packet
        /// Converts from signed short to unsigned short
        /// </summary>
        public static ushort ReadUShort(this GamePacketReader reader)
        {
            return (ushort)reader.ReadShort();
        }

        /// <summary>
        /// ✅ FIXED: Reads an unsigned 32-bit integer (uint) from the packet
        /// Converts from signed int to unsigned int
        /// Used for reading mini-game success counts and other uint values
        /// </summary>
        public static uint ReadUInt32(this GamePacketReader reader)
        {
            return (uint)reader.ReadInt();
        }

        /// <summary>
        /// Reads a signed 32-bit integer (int) from the packet
        /// Direct wrapper for ReadInt()
        /// </summary>
        public static int ReadInt32(this GamePacketReader reader)
        {
            return reader.ReadInt();
        }

        /// <summary>
        /// Reads a byte from the packet
        /// </summary>
        public static byte ReadByte(this GamePacketReader reader)
        {
            return reader.ReadByte();
        }

        /// <summary>
        /// Reads a string from the packet
        /// </summary>
        public static string ReadString(this GamePacketReader reader)
        {
            return reader.ReadString();
        }
    }
}