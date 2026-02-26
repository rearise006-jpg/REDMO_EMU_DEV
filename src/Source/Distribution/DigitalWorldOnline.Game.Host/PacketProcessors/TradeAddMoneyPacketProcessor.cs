using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class TradeAddMoneyacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.TradeAddMoney;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public TradeAddMoneyacketProcessor(MapServer mapServer, DungeonsServer dungeonsServer, EventServer eventServer, PvpServer pvpServer, ILogger logger, ISender sender)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);
            var targetMoney = packet.ReadInt();

            GameClient? targetClient;

            var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));

            switch (mapConfig!.Type)
            {
                case MapTypeEnum.Dungeon:
                    targetClient = _dungeonServer.FindClientByTamerHandleAndChannel(client.Tamer.TargetTradeGeneralHandle, client.TamerId);
                    break;
                case MapTypeEnum.Event:
                    targetClient = _eventServer.FindClientByTamerHandleAndChannel(client.Tamer.TargetTradeGeneralHandle, client.TamerId);
                    break;
                case MapTypeEnum.Pvp:
                    targetClient = _pvpServer.FindClientByTamerHandleAndChannel(client.Tamer.TargetTradeGeneralHandle, client.TamerId);
                    break;
                default:
                    targetClient = _mapServer.FindClientByTamerHandleAndChannel(client.Tamer.TargetTradeGeneralHandle, client.TamerId);
                    break;
            }

            // Check if the player is trying to change the amount (not just add more)
            if (client.Tamer.TradeInventory.Bits > 0 && targetMoney != client.Tamer.TradeInventory.Bits)
            {
                // Clear the trade and notify both clients
                client.Tamer.ClearTrade();
                targetClient?.Tamer.ClearTrade();

                targetClient?.Send(new TradeInventoryUnlockPacket(client.Tamer.TargetTradeGeneralHandle));
                targetClient?.Send(new TradeCancelPacket(client.Tamer.GeneralHandler));
                client.Send(new TradeCancelPacket(client.Tamer.GeneralHandler));

                _logger.Information($"Trade cleared: player tried to change trade money from {client.Tamer.TradeInventory.Bits} to {targetMoney}.");
                return;
            }

            // Usual validation
            if (client.Tamer.Inventory.Bits < targetMoney || client.Tamer.TradeInventory.Bits + targetMoney > client.Tamer.Inventory.Bits)
            {
                // Clear the trade and notify both clients
                client.Tamer.ClearTrade();
                targetClient?.Tamer.ClearTrade();

                targetClient?.Send(new TradeInventoryUnlockPacket(client.Tamer.TargetTradeGeneralHandle));
                targetClient?.Send(new TradeCancelPacket(client.Tamer.GeneralHandler));
                client.Send(new TradeCancelPacket(client.Tamer.GeneralHandler));

                _logger.Error($"Trade invalidated: client attempted to add invalid money (TargetMoney: {targetMoney}, CurrentBits: {client.Tamer.TradeInventory.Bits}).");
                return;
            }

            // Add the money to the trade inventory and synchronize trade states
            client.Tamer.TradeInventory.AddBits(targetMoney);

            // Notify both clients about the trade update
            targetClient?.Send(new TradeInventoryUnlockPacket(client.Tamer.TargetTradeGeneralHandle));
            client.Send(new TradeInventoryUnlockPacket(client.Tamer.TargetTradeGeneralHandle));

            // Notify about the money explicitly added to the trade
            client.Send(new TradeAddMoneyPacket(client.Tamer.GeneralHandler, targetMoney));
            targetClient?.Send(new TradeAddMoneyPacket(client.Tamer.GeneralHandler, targetMoney));
        }

    }
}