using DigitalWorldOnline.Commons.DTOs.Events;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetLastArenaRankingQueryHandler : IRequestHandler<GetLastArenaRankingQuery, ArenaRankingDTO>
    {
        private readonly IServerQueriesRepository _repository;

        public GetLastArenaRankingQueryHandler(IServerQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<ArenaRankingDTO> Handle(GetLastArenaRankingQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetLastArenaRankingAsync(request.Ranking);
        }
    }
}