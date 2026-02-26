using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterActiveDeckCommand : IRequest
    {
        public CharacterModel Character { get; private set; }

        public UpdateCharacterActiveDeckCommand(CharacterModel character)
        {
            Character = character;
        }
    }
}