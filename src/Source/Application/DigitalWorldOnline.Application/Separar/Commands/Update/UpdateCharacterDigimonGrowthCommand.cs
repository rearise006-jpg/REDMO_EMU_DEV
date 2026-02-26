using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterDigimonGrowthCommand : IRequest
    {
        public CharacterDigimonGrowthSystemModel CharacterDigimonGrowth { get; }

        public UpdateCharacterDigimonGrowthCommand(CharacterDigimonGrowthSystemModel characterDigimonGrowth)
        {
            CharacterDigimonGrowth = characterDigimonGrowth;
        }
    }
}