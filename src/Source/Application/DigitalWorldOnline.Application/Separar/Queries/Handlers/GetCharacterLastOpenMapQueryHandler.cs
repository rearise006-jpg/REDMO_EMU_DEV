/*using DigitalWorldOnline.Application.Separar.Commands.Update;  // atau Queries
using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Infrastructure;  // ✅ DatabaseContext burada
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalWorldOnline.Application.Separar.Queries.Handlers
{
    public class GetCharacterLastOpenMapQueryHandler : IRequestHandler<GetCharacterLastOpenMapQuery, LastOpenMapDTO>
    {
        private readonly DatabaseContext _context;  // ✅ GameDbContext → DatabaseContext
        private readonly ILogger _logger;

        public GetCharacterLastOpenMapQueryHandler(DatabaseContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<LastOpenMapDTO> Handle(GetCharacterLastOpenMapQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var character = await _context.Character
                    .FirstOrDefaultAsync(x => x.Id == request.CharacterId, cancellationToken);

                if (character == null)
                {
                    _logger.Warning($"[GetCharacterLastOpenMap] Character {request.CharacterId} not found");
                    return null;
                }

                return new LastOpenMapDTO
                {
                    MapId = (short)character.LastOpenMapId,
                    X = character.LastOpenMapX,
                    Y = character.LastOpenMapY
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"[GetCharacterLastOpenMap] Exception: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}*/