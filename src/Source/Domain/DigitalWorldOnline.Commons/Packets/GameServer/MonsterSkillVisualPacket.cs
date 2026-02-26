using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Writers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DigitalWorldOnline.Commons.Packets.GameServer
{
    public class MonsterSkillVisualPacket : PacketWriter
    {
        private const int PacketNumber = 1123;

        /// <summary>
        /// Constructs a MonsterSkillVisualPacket for sending monster skill usage to the client.
        /// </summary>
        /// <param name="handler">The target handler to set</param>
        /// <param name="monsterUid">The UID of the monster using the skill.</param>
        /// <param name="skillId">The skill index being used.</param>
        /// <param name="effectTargets">Optional: List of effect targets (for ATTACH_SEED type skills).</param>
        public MonsterSkillVisualPacket(
            uint monsterUid,
            uint skillId,
            List<(uint TargetUid, int PosX, int PosY)>? effectTargets = null)
        {
            Type(PacketNumber);
            WriteUInt(monsterUid); // Monster UID
            WriteUInt(skillId);    // Skill Index

            if (effectTargets != null)
            {
                WriteUInt((uint)effectTargets.Count); // Effect Target Count
                foreach (var target in effectTargets)
                {
                    WriteUInt(target.TargetUid); // Target UID
                    WriteInt(target.PosX);       // PosX
                    WriteInt(target.PosY);       // PosY
                }
            }
        }
    }
}
