using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.Models.Character;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterFortuneEventCommand : IRequest
    {
        public CharacterFortuneEventModel FortuneEvent { get; set; }

        public UpdateCharacterFortuneEventCommand(CharacterFortuneEventModel fortuneEvent)
        {
            FortuneEvent = fortuneEvent;
        }
    }
}
