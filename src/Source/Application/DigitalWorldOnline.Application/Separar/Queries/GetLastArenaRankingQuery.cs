using DigitalWorldOnline.Commons.DTOs.Events;
using DigitalWorldOnline.Commons.Enums;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetLastArenaRankingQuery : IRequest<ArenaRankingDTO>
    {
        public ArenaRankingEnum Ranking { get; set; }

        public GetLastArenaRankingQuery(ArenaRankingEnum arenaRankingEnum)
        {
            Ranking = arenaRankingEnum;
        }
    }
}
