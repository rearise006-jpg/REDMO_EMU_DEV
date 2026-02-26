using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Routines.Commands
{
    public class ExecuteSeasonalColiseumRoutineCommandHandler : IRequestHandler<ExecuteSeasonalColiseumRoutineCommand>
    {
        private readonly IRoutineRepository _repository;

        public ExecuteSeasonalColiseumRoutineCommandHandler(IRoutineRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(ExecuteSeasonalColiseumRoutineCommand request, CancellationToken cancellationToken)
        {
            await _repository.ExecuteSeasonalColiseumAsync();

            return Unit.Value;
        }
    }
}