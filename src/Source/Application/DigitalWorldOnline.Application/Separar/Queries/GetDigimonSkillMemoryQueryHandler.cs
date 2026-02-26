using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetDigimonSkillMemoryQueryHandler : IRequestHandler<GetDigimonSkillMemoryQuery, List<DigimonSkillMemoryDTO>>
    {
        private readonly ICharacterQueriesRepository _repository;

        public GetDigimonSkillMemoryQueryHandler(ICharacterQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<DigimonSkillMemoryDTO>> Handle(GetDigimonSkillMemoryQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetDigimonSkillMemoryAsync(request.EvolutionId);
        }
    }
}
