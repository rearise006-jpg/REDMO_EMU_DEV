using DigitalWorldOnline.Commons.Models.TamerShop;

namespace DigitalWorldOnline.Commons.Models.Map
{
    public sealed partial class GameMap
    {
        public bool ViewingConsignedShop(long consignedShopKey, long tamerTarget)
        {
            if (!ConsignedShopView.ContainsKey(consignedShopKey))
                ConsignedShopView.Add(consignedShopKey, new List<long>());

            return ConsignedShopView
                .FirstOrDefault(x => x.Key == consignedShopKey).Value?
                .Contains(tamerTarget) ??
                false;
        }

        public void ShowConsignedShop(long consignedShopKey, long tamerTarget)
        {
            ConsignedShopView
                .FirstOrDefault(x => x.Key == consignedShopKey).Value?
                .Add(tamerTarget);
        }

        public void HideConsignedShop(long consignedShopKey, long tamerTarget)
        {
            ConsignedShopView
                .FirstOrDefault(x => x.Key == consignedShopKey).Value?
                .Remove(tamerTarget);
        }

        public void UpdateConsignedShops(List<ConsignedShop> consignedShops)
        {
            // Eski kapalı shop'ları tracking et
            ConsignedShopsToRemove.Clear();
            ConsignedShopsToRemove
                .AddRange(ConsignedShops
                .Except(consignedShops)
                .ToList());

            // ConsignedShopView'den silinmiş shop'ları da temizle
            var shopsToClean = ConsignedShopsToRemove
                .Select(s => s.Id)
                .ToList();

            foreach (var shopId in shopsToClean)
            {
                if (ConsignedShopView.ContainsKey(shopId))
                {
                    ConsignedShopView.Remove(shopId);  // ← EKLE
                }
            }

            ConsignedShops.Clear();
            ConsignedShops = consignedShops;
        }
    }
}