using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Create
{
    public class CreateMasterMatchPlayerCommandHandler : IRequestHandler<CreateMasterMatchPlayerCommand, MastersMatchRankerDTO>
    {
        private readonly IServerCommandsRepository _repository;

        public CreateMasterMatchPlayerCommandHandler(IServerCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<MastersMatchRankerDTO> Handle(CreateMasterMatchPlayerCommand request, CancellationToken cancellationToken)
        {
            return await _repository.CreateMasterMatchPlayerCommandAsync(request.CharacterId, request.TamerName, request.AssignedTeam);
        }
    }
}
