using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonSkillMemoryCommandHandler : IRequestHandler<UpdateDigimonSkillMemoryCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateDigimonSkillMemoryCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateDigimonSkillMemoryCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateDigimonSkillMemoryAsync(request.Evolution);

            return Unit.Value;
        }
    }
}