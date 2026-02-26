using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ItemSocketInPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.ItemSocketIn;

        private readonly ILogger _logger;
        private readonly ISender _sender;

        public ItemSocketInPacketProcessor(ILogger logger, ISender sender)
        {
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            _ = packet.ReadInt();
            var vipEnabled = packet.ReadByte();
            int npcId = packet.ReadInt();
            short sourceSlot = packet.ReadShort();
            short destinationSlot = packet.ReadShort();
            byte socketOrder = packet.ReadByte();

            try
            {
                var itemInfo = client.Tamer.Inventory.FindItemBySlot(sourceSlot);
                var destinationInfo = client.Tamer.Inventory.FindItemBySlot(destinationSlot);

                if (itemInfo != null && destinationInfo != null)
                {
                    destinationInfo.SocketStatus = destinationInfo.SocketStatus.OrderBy(x => x.Slot).ToList();
                    destinationInfo.AccessoryStatus = destinationInfo.AccessoryStatus.OrderBy(x => x.Slot).ToList();

                    var attributeApply = itemInfo.AccessoryStatus.First(x => x.Value > 0);

                    destinationInfo.SocketStatus[socketOrder].SetType(attributeApply.Type);
                    destinationInfo.SocketStatus[socketOrder].SetAttributeId((short)itemInfo.ItemId);
                    destinationInfo.SocketStatus[socketOrder].SetValue(itemInfo.Power);

                    destinationInfo.AccessoryStatus[socketOrder].SetType(attributeApply.Type);
                    destinationInfo.AccessoryStatus[socketOrder].SetValue(attributeApply.Value);

                    client.Tamer.Inventory.RemoveOrReduceItem(itemInfo, 1, sourceSlot);
                    client.Tamer.Inventory.RemoveBits(itemInfo.ItemInfo.ScanPrice / 2);

                    client.Send(new ItemSocketInPacket((int)client.Tamer.Inventory.Bits));

                    await _sender.Send(new UpdateItemSocketStatusCommand(destinationInfo));
                    await _sender.Send(new UpdateItemAccessoryStatusCommand(destinationInfo));
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                    await _sender.Send(new UpdateItemListBitsCommand(client.Tamer.Inventory));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ItemSocketIn] :: {ex.Message}");
            }
        }
    }
}