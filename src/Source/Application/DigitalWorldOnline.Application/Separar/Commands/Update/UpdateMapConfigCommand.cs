using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Models.Config;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateMapConfigCommand : IRequest
    {
        public MapConfigDTO MapConfig { get; set; }

        public UpdateMapConfigCommand(MapConfigDTO mapConfig)
        {
            MapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
        }
    }
}