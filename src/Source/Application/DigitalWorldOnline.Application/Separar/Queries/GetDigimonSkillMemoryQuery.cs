using DigitalWorldOnline.Commons.DTOs.Digimon;
using MediatR;

public class GetDigimonSkillMemoryQuery : IRequest<List<DigimonSkillMemoryDTO>>
{
    public long EvolutionId { get; set; }

    public GetDigimonSkillMemoryQuery(long evolutionId)
    {
        EvolutionId = evolutionId;
    }
}
