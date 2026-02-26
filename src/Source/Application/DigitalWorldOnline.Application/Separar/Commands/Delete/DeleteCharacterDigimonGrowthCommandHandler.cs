using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteCharacterDigimonGrowthCommandHandler : IRequestHandler<DeleteCharacterDigimonGrowthCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public DeleteCharacterDigimonGrowthCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(DeleteCharacterDigimonGrowthCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteCharacterDigimonGrowthAsync(request.GrowthSlot);

            return Unit.Value;
        }
    }
}
