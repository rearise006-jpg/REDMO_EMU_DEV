using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ItemRemovePacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ItemRemove;

        private readonly ISender _sender;
        private readonly ILogger _logger;
        private readonly MapServer _mapServer;  // Assuming you need the MapServer for Discord logging

        public ItemRemovePacketProcessor(
            ISender sender,
            MapServer mapServer,  // Pass MapServer into the constructor
            ILogger logger)
        {
            _sender = sender;
            _logger = logger;
            _mapServer = mapServer;  // Initialize _mapServer
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var slot = packet.ReadShort();
            var posx = packet.ReadInt();
            var posy = packet.ReadInt();
            var amount = packet.ReadShort();

            _logger.Verbose($"Processing ItemRemove for Tamer {client.TamerId}. Slot: {slot}, Amount: {amount}, Position: ({posx}, {posy}).");

            var targetItem = client.Tamer.Inventory.FindItemBySlot(slot);

            if (targetItem?.ItemId > 0)
            {
                var itemName = targetItem.ItemInfo?.Name ?? "Unknown Item";  // Obtém o nome do item, caso exista
                client.Tamer.Inventory.RemoveOrReduceItem(targetItem, amount, slot);

                await _sender.Send(new UpdateItemCommand(targetItem));
                
            }
        }
    }
}