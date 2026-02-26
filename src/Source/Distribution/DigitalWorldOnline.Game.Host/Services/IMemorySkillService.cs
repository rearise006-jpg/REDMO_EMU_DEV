using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.GameHost;
using System.Threading.Tasks;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Interfaces;

namespace DigitalWorldOnline.Game.PacketProcessors;

public interface IMemorySkillService
{
    Task HandleMemorySkillUseAsync(GameClient client, IMapServer server, BuffInfoAssetModel? buffInfo, SkillInfoAssetModel? skillInfo, int skillCode, int targetHandler);
}
