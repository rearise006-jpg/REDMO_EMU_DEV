using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Application_Game.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using MediatR;
using Microsoft.Identity.Client;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class ChannelsPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.Channels;

        private readonly ILogger _logger;
        private readonly ISender _sender;

        public ChannelsPacketProcessor(ILogger logger, ISender sender)
        {
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            if (!client.DungeonMap)
            {
                // 📌 MapConfig'i query ile al
                var mapConfig = await _sender.Send(new GameMapConfigByMapIdQuery(client.Tamer.Location.MapId));
                var liveMapChannels = await _sender.Send(new ChannelByMapIdQuery(client.Tamer.Location.MapId));

                var channelsToSend = new Dictionary<byte, byte>();

                // Sabit 3 yerine config'ten gelen değeri kullan
                // 📌 mapConfig.Channels kullan
                for (byte i = 0; i < mapConfig.Channels; i++)
                {
                    if (liveMapChannels != null && liveMapChannels.TryGetValue(i, out byte playerCount))
                    {
                        channelsToSend.Add(i, playerCount.GetChannelLoad());
                    }
                    else
                    {
                        channelsToSend.Add(i, (byte)ChannelLoadEnum.Empty);
                    }
                }

                if (channelsToSend.Any())
                {
                    client.Send(new AvailableChannelsPacket(channelsToSend).Serialize());
                }
                else
                {
                    _logger.Warning($"No channels to send for map {client.Tamer.Location.MapId}.");
                }
            }
            else
            {
                var channels = new Dictionary<byte, byte>
                {
                    { 0, (byte)ChannelLoadEnum.Empty }
                };
            }
        }
    }
}