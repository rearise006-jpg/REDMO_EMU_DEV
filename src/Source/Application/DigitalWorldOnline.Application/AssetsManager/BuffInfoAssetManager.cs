using System.Xml.Linq;
using DigitalWorldOnline.Commons.Models.Assets;

namespace DigitalWorldOnline.Application.AssetsManager
{
    public sealed class BuffInfoAssetManager
    {
        private static readonly Lazy<BuffInfoAssetManager> _instance = new(() => new BuffInfoAssetManager());

        public static BuffInfoAssetManager Instance => _instance.Value;

        private readonly Dictionary<int, BuffInfoAssetModel> _buffs = new();

        private BuffInfoAssetManager()
        {
            LoadItems();
        }

        private void LoadItems()
        {
            string fileName = "Buff.xml";
            string folderName = Path.GetFileNameWithoutExtension(fileName);
            string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", folderName);
            string filePath = Path.Combine(folderPath, fileName);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Buff.xml not found: {filePath}");
            
            var xml = XDocument.Load(filePath);
            var buffNodes = xml.Descendants("BuffData");

            List<BuffInfoAssetModel> buffs = new();

            foreach (var node in buffNodes)
            {
                var model = new BuffInfoAssetModel
                {
                    BuffId = int.Parse(node.Element("s_dwID")?.Value ?? "0"),
                    Name = node.Element("s_szName")?.Value ?? string.Empty,
                    Comment = node.Element("s_szComment")?.Value ?? string.Empty,
                    Icon = int.Parse(node.Element("s_nBuffIcon")?.Value ?? "0"),
                    Type = int.Parse(node.Element("s_nBuffType")?.Value ?? "0"),
                    LifeType = int.Parse(node.Element("s_nBuffLifeType")?.Value ?? "0"),
                    TimeType = int.Parse(node.Element("s_nBuffTimeType")?.Value ?? "0"),
                    MinLevel = int.Parse(node.Element("s_nMinLv")?.Value ?? "0"),
                    Class = int.Parse(node.Element("s_nBuffClass")?.Value ?? "0"),
                    Unknow = int.Parse(node.Element("unknow")?.Value ?? "0"),
                    SkillCode = int.Parse(node.Element("s_dwSkillCode")?.Value ?? "0"),
                    DigimonSkillCode = int.Parse(node.Element("s_dwDigimonSkillCode")?.Value ?? "0"),
                    Delete = int.Parse(node.Element("s_bDelete")?.Value ?? "0") == 1,
                    EffectFile = node.Element("s_szEffectFile")?.Value ?? string.Empty,
                    ConditionLevel = int.Parse(node.Element("s_nConditionLv")?.Value ?? "0"),
                    U = int.Parse(node.Element("u")?.Value ?? "0"),
                };

                buffs.Add(model);
            }

            foreach (var buff in buffs)
                _buffs[buff.BuffId] = buff;
        }

        public BuffInfoAssetModel? GetById(int id)
        {
            _buffs.TryGetValue(id, out var buff);
            return buff;
        }

        public List<BuffInfoAssetModel> GetAllBuffs() => _buffs.Values.ToList();
    }
}
