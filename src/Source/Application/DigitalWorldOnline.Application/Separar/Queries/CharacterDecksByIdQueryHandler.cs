using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class CharacterDecksByIdQueryHandler : IRequestHandler<CharacterDecksByIdQuery, List<CharacterActiveDeckDTO>>
    {
        private readonly ICharacterQueriesRepository _repository;

        public CharacterDecksByIdQueryHandler(ICharacterQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CharacterActiveDeckDTO>> Handle(CharacterDecksByIdQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetCharacterDecksByIdAsync(request.CharacterId);
        }
    }
}
