using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class CharacterDigimonsGrowthByIdQueryHandler : IRequestHandler<CharacterDigimonsGrowthByIdQuery, List<CharacterDigimonGrowthSystemDTO>>
    {
        private readonly ICharacterQueriesRepository _repository;

        public CharacterDigimonsGrowthByIdQueryHandler(ICharacterQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CharacterDigimonGrowthSystemDTO>> Handle(CharacterDigimonsGrowthByIdQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetCharacterDigimonGrowthAsync(request.CharacterId);
        }
    }
}
