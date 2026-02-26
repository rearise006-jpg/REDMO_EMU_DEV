using DigitalWorldOnline.Commons.DTOs.Events;
using DigitalWorldOnline.Commons.Enums;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetArenaOldRankingQuery : IRequest<ArenaRankingDTO>
    {
        public ArenaRankingEnum Ranking { get; set; }

        public GetArenaOldRankingQuery(ArenaRankingEnum arenaRankingEnum)
        {
            Ranking = arenaRankingEnum;
        }
    }
}
