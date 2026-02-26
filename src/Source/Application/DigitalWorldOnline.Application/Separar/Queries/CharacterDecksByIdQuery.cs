using DigitalWorldOnline.Commons.DTOs.Character;
using MediatR;

public class CharacterDecksByIdQuery : IRequest<List<CharacterActiveDeckDTO>>
{
    public long CharacterId { get; set; }

    public CharacterDecksByIdQuery(long characterid)
    {
        CharacterId = characterid;
    }
}
