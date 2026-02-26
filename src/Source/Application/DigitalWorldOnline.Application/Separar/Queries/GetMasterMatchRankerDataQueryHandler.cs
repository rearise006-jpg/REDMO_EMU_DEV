using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetMasterMatchRankerDataQueryHandler : IRequestHandler<GetMasterMatchRankerDataQuery, MastersMatchRankerDTO>
    {
        private readonly IServerQueriesRepository _repository;

        public GetMasterMatchRankerDataQueryHandler(IServerQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<MastersMatchRankerDTO> Handle(GetMasterMatchRankerDataQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetMasterMatchRankerDataAsync(request.CharacterId);
        }
    }
}