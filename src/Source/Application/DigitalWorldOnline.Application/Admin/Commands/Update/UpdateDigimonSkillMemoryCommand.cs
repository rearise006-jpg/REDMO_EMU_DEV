using DigitalWorldOnline.Commons.Models.Digimon;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonSkillMemoryCommand : IRequest
    {
        public DigimonSkillMemoryModel Evolution { get; set; }

        public UpdateDigimonSkillMemoryCommand(DigimonSkillMemoryModel evolution)
        {
            Evolution = evolution;
        }
    }
}