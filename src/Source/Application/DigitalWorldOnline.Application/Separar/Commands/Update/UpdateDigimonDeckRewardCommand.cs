using DigitalWorldOnline.Commons.Models.Digimon;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonDeckRewardCommand : IRequest
    {
        public DigimonModel Digimon { get; private set; }

        public UpdateDigimonDeckRewardCommand(DigimonModel digimon)
        {
            Digimon = digimon;
        }
    }
}