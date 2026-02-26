using MediatR;

namespace DigitalWorldOnline.Application_Game.Separar.Queries
{
    public class ChannelByMapIdQuery : IRequest<IDictionary<byte, byte>>
    {
        public short MapId { get; set; }

        public ChannelByMapIdQuery(short mapId)
        {
            MapId = mapId;
        }
    }
}

