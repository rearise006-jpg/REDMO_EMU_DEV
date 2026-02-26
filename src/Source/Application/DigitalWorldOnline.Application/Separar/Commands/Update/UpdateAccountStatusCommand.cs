using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateAccountStatusCommand : IRequest
    {
        public long AccountId { get; set; }
        public byte Status { get; set; }

        public UpdateAccountStatusCommand(long accountId, byte status)
        {
            AccountId = accountId;
            Status = status;
        }
    }
}