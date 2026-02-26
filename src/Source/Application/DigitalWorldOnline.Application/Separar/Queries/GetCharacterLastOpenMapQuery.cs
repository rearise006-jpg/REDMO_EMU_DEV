using MediatR;
using DigitalWorldOnline.Commons.DTOs.Character;  // ✅ Buradan import et

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetCharacterLastOpenMapQuery : IRequest<LastOpenMapDTO>
    {
        public long CharacterId { get; set; }

        public GetCharacterLastOpenMapQuery(long characterId)
        {
            CharacterId = characterId;
        }
    }

    // ❌ KALDIR: Bu tanımı buradan sil
    // public class LastOpenMapDTO { ... }
}