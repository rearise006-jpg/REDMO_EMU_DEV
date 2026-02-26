/*using DigitalWorldOnline.Application.Separar.Commands.Update;  // atau Queries
using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Infrastructure;  // ✅ DatabaseContext burada
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalWorldOnline.Application.Separar.Commands.Update.Handlers
{
    public class UpdateCharacterLastOpenMapHandler : IRequestHandler<UpdateCharacterLastOpenMapCommand, bool>
    {
        private readonly DatabaseContext _context;  // ✅ GameDbContext → DatabaseContext
        private readonly ILogger _logger;

        public UpdateCharacterLastOpenMapHandler(DatabaseContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateCharacterLastOpenMapCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Character tablosunu al
                var character = await _context.Character
                    .FirstOrDefaultAsync(x => x.Id == request.CharacterId, cancellationToken);

                if (character == null)
                {
                    _logger.Warning($"[UpdateCharacterLastOpenMap] Character {request.CharacterId} not found");
                    return false;
                }

                // ✅ Son açık harita bilgisini kaydet
                character.LastOpenMapId = request.MapId;
                character.LastOpenMapX = request.X;
                character.LastOpenMapY = request.Y;

                _context.Character.Update(character);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.Information($"[UpdateCharacterLastOpenMap] Saved for CharacterId={request.CharacterId}: MapId={request.MapId}, X={request.X}, Y={request.Y}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[UpdateCharacterLastOpenMap] Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}*/