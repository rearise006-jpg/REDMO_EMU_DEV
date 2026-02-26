using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterFortuneEventCommandHandler : IRequestHandler<UpdateCharacterFortuneEventCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateCharacterFortuneEventCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateCharacterFortuneEventCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateCharacterFortuneEventAsync(request.FortuneEvent);

            return Unit.Value;
        }
    }
}
