using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Delete
{
    public class DeleteDigimonSkillMemoryCommand : IRequest
    {
        public long SkillId { get; set; }
        public long EvolutionId { get; set; }

        public DeleteDigimonSkillMemoryCommand(long skillId, long evolutionId)
        {
            SkillId = skillId;
            EvolutionId = evolutionId;
        }
    }
}
