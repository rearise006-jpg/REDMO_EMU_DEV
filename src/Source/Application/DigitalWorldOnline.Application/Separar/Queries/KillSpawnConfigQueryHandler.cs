using DigitalWorldOnline.Commons.DTOs.Config;
using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class KillSpawnConfigQueryHandler : IRequestHandler<KillSpawnConfigQuery, List<KillSpawnConfigDTO>>
    {
        private readonly IServerQueriesRepository _repository;

        public KillSpawnConfigQueryHandler(IServerQueriesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<KillSpawnConfigDTO>> Handle(KillSpawnConfigQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetKillSpawnConfigAsync();
        }
    }
}