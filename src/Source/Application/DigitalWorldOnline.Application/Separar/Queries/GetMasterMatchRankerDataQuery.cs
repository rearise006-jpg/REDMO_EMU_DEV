using DigitalWorldOnline.Commons.DTOs.Mechanics;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetMasterMatchRankerDataQuery : IRequest<MastersMatchRankerDTO>
    {
        public long CharacterId { get; private set; }

        public GetMasterMatchRankerDataQuery(long characterId)
        {
            CharacterId = characterId;
        }
    }
}