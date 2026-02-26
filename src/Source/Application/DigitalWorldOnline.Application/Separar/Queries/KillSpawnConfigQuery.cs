using MediatR;
using DigitalWorldOnline.Commons.DTOs.Config;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class KillSpawnConfigQuery : IRequest<List<KillSpawnConfigDTO>>
    {
    }
}