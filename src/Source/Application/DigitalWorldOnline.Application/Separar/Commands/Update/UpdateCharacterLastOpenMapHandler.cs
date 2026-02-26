using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterLastOpenMapHandler : IRequestHandler<UpdateCharacterLastOpenMapCommand, bool>
    {
        private readonly ICharacterCommandsRepository _repository;

        public UpdateCharacterLastOpenMapHandler(ICharacterCommandsRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> Handle(UpdateCharacterLastOpenMapCommand request, CancellationToken cancellationToken)
        {
            return await _repository.UpdateCharacterLastOpenMapAsync(
                request.CharacterId,
                request.MapId,
                request.X,
                request.Y
            );
        }
    }
}