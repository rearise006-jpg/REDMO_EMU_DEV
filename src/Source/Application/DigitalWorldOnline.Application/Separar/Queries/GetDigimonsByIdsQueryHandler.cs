using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetDigimonsByIdsQueryHandler : IRequestHandler<GetDigimonsByIdsQuery, List<DigimonDTO>>
    {
        private readonly ICharacterQueriesRepository _repository;

        public GetDigimonsByIdsQueryHandler(ICharacterQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<DigimonDTO>> Handle(GetDigimonsByIdsQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetDigimonsByIdsAsync(request.DigimonIds);
        }
    }
}
