using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteDigimonSkillMemoryCommandHandler : IRequestHandler<DeleteDigimonSkillMemoryCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public DeleteDigimonSkillMemoryCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(DeleteDigimonSkillMemoryCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteDigimonSkillMemoryAsync(request.SkillId, request.EvolutionId);

            return Unit.Value;
        }
    }
}
