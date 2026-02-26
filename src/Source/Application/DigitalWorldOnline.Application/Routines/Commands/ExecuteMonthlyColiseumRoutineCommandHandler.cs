using DigitalWorldOnline.Commons.Interfaces;
using MediatR;

namespace DigitalWorldOnline.Application.Routines.Commands
{
    public class ExecuteMonthlyColiseumRoutineCommandHandler : IRequestHandler<ExecuteMonthlyColiseumRoutineCommand>
    {
        private readonly IRoutineRepository _repository;

        public ExecuteMonthlyColiseumRoutineCommandHandler(IRoutineRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(ExecuteMonthlyColiseumRoutineCommand request, CancellationToken cancellationToken)
        {
            await _repository.ExecuteMonthlyColiseumAsync();

            return Unit.Value;
        }
    }
}