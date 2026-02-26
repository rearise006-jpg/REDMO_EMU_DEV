using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteCharacterDigimonGrowthCommand : IRequest
    {
        public int GrowthSlot { get; set; }

        public DeleteCharacterDigimonGrowthCommand(int growthSlot)
        {
            GrowthSlot = growthSlot;
        }
    }
}
