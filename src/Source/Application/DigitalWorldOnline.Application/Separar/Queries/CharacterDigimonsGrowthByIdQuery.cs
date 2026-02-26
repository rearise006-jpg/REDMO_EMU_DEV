using DigitalWorldOnline.Commons.DTOs.Character;
using MediatR;

public class CharacterDigimonsGrowthByIdQuery : IRequest<List<CharacterDigimonGrowthSystemDTO>>
{
    public long CharacterId { get; set; }

    public CharacterDigimonsGrowthByIdQuery(long characterid)
    {
        CharacterId = characterid;
    }
}
