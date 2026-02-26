using MediatR;
using DigitalWorldOnline.Commons.Interfaces;

namespace DigitalWorldOnline.Application_Game.Separar.Queries
{
    public class ChannelByMapIdQueryHandler : IRequestHandler<ChannelByMapIdQuery, IDictionary<byte, byte>>
    {
        private readonly IMapServer _mapServer;

        public ChannelByMapIdQueryHandler(IMapServer mapServer)
        {
            _mapServer = mapServer;
        }

        public Task<IDictionary<byte, byte>> Handle(ChannelByMapIdQuery request, CancellationToken cancellationToken)
        {
            var channelsData = _mapServer.GetLiveChannelsAndPlayerCountsForMap(request.MapId);

            return Task.FromResult<IDictionary<byte, byte>>(channelsData);
        }
    }
}
