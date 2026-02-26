using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Routines.Commands
{
    public class ExecuteWeeklyColiseumRoutineCommandHandler : IRequestHandler<ExecuteWeeklyColiseumRoutineCommand>
    {
        private readonly IRoutineRepository _repository;

        public ExecuteWeeklyColiseumRoutineCommandHandler(IRoutineRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(ExecuteWeeklyColiseumRoutineCommand request, CancellationToken cancellationToken)
        {
            await _repository.ExecuteWeeklyColiseumAsync();

            return Unit.Value;
        }
    }
}