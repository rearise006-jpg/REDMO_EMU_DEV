using DigitalWorldOnline.Commons.DTOs.Events;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetArenaOldRankingQueryHandler : IRequestHandler<GetArenaOldRankingQuery, ArenaRankingDTO>
    {
        private readonly IServerQueriesRepository _repository;

        public GetArenaOldRankingQueryHandler(IServerQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<ArenaRankingDTO> Handle(GetArenaOldRankingQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetArenaOldRankingAsync(request.Ranking);
        }
    }
}