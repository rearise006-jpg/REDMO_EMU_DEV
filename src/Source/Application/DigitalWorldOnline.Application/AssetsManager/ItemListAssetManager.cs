using System.Xml.Linq;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Collections.Immutable;

namespace DigitalWorldOnline.Application.AssetsManager
{
    public sealed class ItemlistAssetManager
    {
        private static readonly Lazy<ItemlistAssetManager> _instance = new(() => new ItemlistAssetManager());
        public static ItemlistAssetManager Instance => _instance.Value;

        private readonly Dictionary<int, ItemAssetModel> _items = new Dictionary<int, ItemAssetModel>();

        private ItemlistAssetManager()
        {
            LoadItems();
        }

        private void LoadItems()
        {
            string fileName = "ItemList_ItemList.xml";
            string folderName = Path.GetFileNameWithoutExtension("Items");
            string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", folderName);
            string filePath = Path.Combine(folderPath, fileName);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"ItemList_ItemList.xml not found: {filePath}");

            var xml = XDocument.Load(filePath);
            var itemNodes = xml.Descendants("sINFO");

            foreach (var node in itemNodes)
            {
                try
                {
                    var itemId = int.Parse(node.Element("s_dwItemID")?.Value ?? "0");
                    var name = node.Element("s_szName")?.Value ?? string.Empty;
                    var @class = int.Parse(node.Element("s_nClass")?.Value ?? "0");
                    var type = int.Parse(node.Element("s_nType_L")?.Value ?? "0");
                    var typeN = int.Parse(node.Element("s_nTypeValue")?.Value ?? "0");
                    var applyValueMin = short.Parse(node.Element("s_btApplyRateMin")?.Value ?? "0");
                    var applyValueMax = short.Parse(node.Element("s_btApplyRateMax")?.Value ?? "0");
                    var applyElement = short.Parse(node.Element("s_btApplyElement")?.Value ?? "0");
                    var section = int.Parse(node.Element("s_nSection")?.Value ?? "0");
                    var sellType = int.Parse(node.Element("s_nSellType")?.Value ?? "0");
                    var boundType = int.Parse(node.Element("s_nBelonging")?.Value ?? "0");
                    var useTimeType = int.Parse(node.Element("s_btUseTimeType")?.Value ?? "0");
                    var skillCode = long.Parse(node.Element("s_dwSkill")?.Value ?? "0");
                    var tamerMinLevel = byte.Parse(node.Element("s_nTamerReqMinLevel")?.Value ?? "0");
                    var tamerMaxLevel = byte.Parse(node.Element("s_nTamerReqMaxLevel")?.Value ?? "0");
                    var digimonMinLevel = byte.Parse(node.Element("s_nDigimonReqMinLevel")?.Value ?? "0");
                    var digimonMaxLevel = byte.Parse(node.Element("s_nDigimonReqMaxLevel")?.Value ?? "0");
                    var sellPrice = long.Parse(node.Element("s_dwSale")?.Value ?? "0");
                    var scanPrice = int.Parse(node.Element("s_dwScanPrice")?.Value ?? "0");
                    var digicorePrice = int.Parse(node.Element("s_dwDigiCorePrice")?.Value ?? "0");
                    var eventPriceId = int.Parse(node.Element("s_nEventItemType")?.Value ?? "0");
                    var eventPriceAmount = int.Parse(node.Element("s_dwEventItemPrice")?.Value ?? "0");
                    var usageTimeMinutes = int.Parse(node.Element("s_nUseTime_Min")?.Value ?? "0");
                    var overlap = short.Parse(node.Element("s_nOverlap")?.Value ?? "1");
                    var target = (ItemConsumeTargetEnum)int.Parse(node.Element("s_nUseCharacter")?.Value ?? "0");

                    // let me update the model. ok


                    _items.Add(itemId, new ItemAssetModel(
                        itemId,
                        name,
                        @class,
                        type,
                        typeN,
                        applyValueMin,
                        applyValueMax,
                        applyElement,
                        section,
                        sellType,
                        boundType,
                        useTimeType,
                        skillCode,
                        tamerMinLevel,
                        tamerMaxLevel,
                        digimonMinLevel,
                        digimonMaxLevel,
                        sellPrice,
                        scanPrice,
                        digicorePrice,
                        eventPriceId,
                        eventPriceAmount,
                        usageTimeMinutes,
                        overlap,
                        target
                    ));
                }
                catch (Exception ex)
                {
                    // Log but continue processing other items
                    Console.WriteLine($"Error parsing item: {ex.Message}");
                }
            }
        }

        public ItemAssetModel GetByID(int id)
        {
            _items.TryGetValue(id, out var item);
            return item;

        }

        public ReadOnlyDictionary<int,ItemAssetModel>  GetAllItems()
        {
            return new ReadOnlyDictionary<int, ItemAssetModel>(_items); 
        }

        //this class should be ready now.
        // you there ?

    }
}