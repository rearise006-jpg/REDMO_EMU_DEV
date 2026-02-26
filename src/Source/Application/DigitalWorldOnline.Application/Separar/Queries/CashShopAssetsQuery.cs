using MediatR;
using DigitalWorldOnline.Commons.DTOs.Assets;
using DigitalWorldOnline.Commons.Models.Assets;

namespace DigitalWorldOnline.Application.Separar.Queries
{
    public class CashShopAssetsQuery : IRequest<List<CashShopAssetDTO>>
    {

    }
}