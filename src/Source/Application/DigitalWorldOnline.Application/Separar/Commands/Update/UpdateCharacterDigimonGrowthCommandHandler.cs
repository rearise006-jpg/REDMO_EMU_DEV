using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterDigimonGrowthCommandHandler : IRequestHandler<UpdateCharacterDigimonGrowthCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateCharacterDigimonGrowthCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateCharacterDigimonGrowthCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateCharacterDigimonGrowthAsync(request.CharacterDigimonGrowth);

            return Unit.Value;
        }
    }
}