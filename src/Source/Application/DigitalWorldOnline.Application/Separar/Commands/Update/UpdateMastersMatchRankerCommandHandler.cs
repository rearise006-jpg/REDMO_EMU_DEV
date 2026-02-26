using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateMastersMatchRankerCommandHandler : IRequestHandler<UpdateMastersMatchRankerCommand>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateMastersMatchRankerCommandHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(UpdateMastersMatchRankerCommand request, CancellationToken cancellationToken)
        {
            await _repository.UpdateMasterMatchRankerAsync(request.CharacterId, request.TamerName, request.Team, request.DonatedAmount);

            return Unit.Value;
        }
    }
}