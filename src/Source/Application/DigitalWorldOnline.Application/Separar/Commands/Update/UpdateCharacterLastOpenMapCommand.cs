using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateCharacterLastOpenMapCommand : IRequest<bool>
    {
        public long CharacterId { get; set; }
        public short MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public UpdateCharacterLastOpenMapCommand(long characterId, short mapId, int x, int y)
        {
            CharacterId = characterId;
            MapId = mapId;
            X = x;
            Y = y;
        }
    }
}