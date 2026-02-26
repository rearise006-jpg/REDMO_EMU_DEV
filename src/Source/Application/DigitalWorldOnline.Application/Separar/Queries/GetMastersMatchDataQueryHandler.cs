using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetMastersMatchDataQueryHandler : IRequestHandler<GetMastersMatchDataQuery, MastersMatchDTO>
    {
        private readonly IServerQueriesRepository _repository;

        public GetMastersMatchDataQueryHandler(IServerQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<MastersMatchDTO> Handle(GetMastersMatchDataQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetMasterMatchDataAsync();
        }
    }
}