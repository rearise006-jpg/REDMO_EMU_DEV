using MediatR;
using DigitalWorldOnline.Commons.DTOs.Digimon;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetDigimonsByIdsQuery : IRequest<List<DigimonDTO>>
    {
        public List<long> DigimonIds { get; }

        public GetDigimonsByIdsQuery(List<long> digimonIds)
        {
            DigimonIds = digimonIds;
        }
    }
}
