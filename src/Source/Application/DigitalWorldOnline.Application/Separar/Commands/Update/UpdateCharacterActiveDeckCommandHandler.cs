using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterActiveDeckCommandHandler : IRequestHandler<UpdateCharacterActiveDeckCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateCharacterActiveDeckCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateCharacterActiveDeckCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateCharacterActiveDeckAsync(request.Character);

            return Unit.Value;
        }
    }
}