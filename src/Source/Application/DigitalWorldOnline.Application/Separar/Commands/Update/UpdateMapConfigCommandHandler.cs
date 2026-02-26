using MediatR;
using AutoMapper;
using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Repositories.Admin;

namespace DigitalWorldOnline.Application.Admin.Commands
{
    /// <summary>
    /// Handler for UpdateMapConfigCommand - Updates map configuration in database
    /// </summary>
    public class UpdateMapConfigCommandHandler : IRequestHandler<UpdateMapConfigCommand, Unit>
    {
        private readonly IAdminCommandsRepository _repository;
        private readonly IMapper _mapper;

        public UpdateMapConfigCommandHandler(
            IAdminCommandsRepository repository,
            IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        /// <summary>
        /// Handles the UpdateMapConfigCommand request
        /// </summary>
        /// <param name="request">The command containing map configuration to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Unit (void result)</returns>
        public async Task<Unit> Handle(UpdateMapConfigCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (request?.MapConfig == null)
                {
                    throw new ArgumentNullException(nameof(request.MapConfig), "Map configuration cannot be null");
                }

                // ✅ FIX: DTO'yu direkt repository'ye gönder
                // Repository şimdi MapConfigDTO'yu kabul ediyor
                await _repository.UpdateMapConfigAsync(request.MapConfig);

                return Unit.Value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error updating map config: {ex.Message}", ex);
            }
        }
    }
}