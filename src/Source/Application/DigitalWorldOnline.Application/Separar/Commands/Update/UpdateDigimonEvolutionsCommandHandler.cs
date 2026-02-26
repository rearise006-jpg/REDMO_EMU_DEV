using AutoMapper;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.Interfaces;

namespace DigitalWorldOnline.Application.Separar.Handlers.Update
{
    public class UpdateDigimonEvolutionsCommandHandler : IRequestHandler<UpdateDigimonEvolutionsCommand, List<DigimonEvolutionDTO>>
    {
        private readonly ICharacterCommandsRepository _repository;
        private readonly IMapper _mapper;

        public UpdateDigimonEvolutionsCommandHandler(ICharacterCommandsRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<List<DigimonEvolutionDTO>> Handle(UpdateDigimonEvolutionsCommand request, CancellationToken cancellationToken)
        {
            // O repositório já deve retornar a lista atualizada dos DTOs
            return await _repository.UpdateDigimonEvolutionsAsync(request.DigimonId, request.Evolutions);
        }
    }
}
