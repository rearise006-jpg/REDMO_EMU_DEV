using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigimonEffectPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.DigimonEffect;

        private readonly ISender _sender;
        private readonly ILogger _logger;

        private readonly AssetsLoader _assets;

        private readonly MapServer _mapServer;

        public DigimonEffectPacketProcessor(ISender sender, ILogger logger, MapServer mapServer, AssetsLoader assets)
        {
            _sender = sender;
            _logger = logger;
            _mapServer = mapServer;
            _assets = assets;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var itemSlot = packet.ReadShort();
            var itemType = packet.ReadInt();
            var itemCount = packet.ReadShort();
            var readByte = packet.ReadByte();

            try
            {
                var result = 0;
                byte effectType = 1;

                var targetItem = client.Tamer.Inventory.FindItemBySlot(itemSlot);

                if (targetItem == null)
                {
                    _logger.Error($"Invalid item at slot {itemSlot} for tamer id {client.TamerId}.");
                    return;
                }

                if (targetItem.ItemInfo.Type == 301)
                {
                    client.Tamer.Inventory.RemoveOrReduceItem(targetItem, 1, itemSlot);
                    await _sender.Send(new UpdateItemCommand(targetItem));
                }

                var evoInfo = _assets.EvolutionInfo.FirstOrDefault(x => x.Type == client.Partner.BaseType)?
                    .Lines.FirstOrDefault(x => x.Type == client.Partner.CurrentType);

                if (evoInfo == null)
                {
                    _logger.Error($"evoInfo not found !!");
                    return;
                }

                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new DigimonEffectPacket(result, itemSlot, itemType, itemCount, evoInfo.SlotLevel, effectType).Serialize());
                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new DigimonEffectChangePacket(client.Partner.GeneralHandler, itemType).Serialize());
            }
            catch (Exception ex)
            {
                _logger.Error($"[DigimonEffect] :: {ex.Message}");
            }
        }
    }
}
