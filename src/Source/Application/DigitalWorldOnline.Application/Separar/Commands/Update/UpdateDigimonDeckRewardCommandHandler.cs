using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonDeckRewardCommandHandler : IRequestHandler<UpdateDigimonDeckRewardCommand>
    {
        private readonly IServerCommandsRepository _repository;

        public UpdateDigimonDeckRewardCommandHandler(IServerCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateDigimonDeckRewardCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateDigimonDeckRewardAsync(request.Digimon);

            return Unit.Value;
        }
    }
}