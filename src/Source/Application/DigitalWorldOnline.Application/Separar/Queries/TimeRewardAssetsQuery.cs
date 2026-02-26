using MediatR;
using DigitalWorldOnline.Commons.DTOs.Assets;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.DTOs.Events;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class TimeRewardAssetsQuery : IRequest<List<TimeRewardAssetDTO>>
    {

    }

    public class TimeRewardEventsQuery : IRequest<List<TimeRewardDTO>>
    {

    }
}