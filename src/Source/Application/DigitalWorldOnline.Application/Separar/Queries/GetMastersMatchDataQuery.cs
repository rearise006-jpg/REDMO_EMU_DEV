using DigitalWorldOnline.Commons.DTOs.Mechanics;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class GetMastersMatchDataQuery : IRequest<MastersMatchDTO>
    {
        public GetMastersMatchDataQuery() { }
    }
}