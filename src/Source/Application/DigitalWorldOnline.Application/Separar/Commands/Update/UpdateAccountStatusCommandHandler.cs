using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateAccountStatusCommandHandler : IRequestHandler<UpdateAccountStatusCommand>
    {
        private readonly IAccountCommandsRepository _repository;

        public UpdateAccountStatusCommandHandler(IAccountCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateAccountStatusCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateAccountStatusAsync(request.AccountId, request.Status);

            return Unit.Value;
        }
    }
}