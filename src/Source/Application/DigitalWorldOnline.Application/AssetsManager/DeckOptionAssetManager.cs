using System.Xml.Linq;
using DigitalWorldOnline.Commons.Models.Assets;

public sealed class DeckOptionAssetManager
{
    private static readonly Lazy<DeckOptionAssetManager> _instance = new(() => new DeckOptionAssetManager());
    public static DeckOptionAssetManager Instance => _instance.Value;

    private readonly Dictionary<short, DeckOptionModel> _deckOptions = new();

    private DeckOptionAssetManager()
    {
        LoadDeckOptions();
    }

    private void LoadDeckOptions()
    {
        string fileName = "DigimonBook_DeckOption.xml";
        string folderName = Path.GetFileNameWithoutExtension("DigimonBook");
        string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", folderName);
        string filePath = Path.Combine(folderPath, fileName);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DeckOption.xml not found: {filePath}");

        var xml = XDocument.Load(filePath);
        var nodes = xml.Descendants("DeckOption");

        List<DeckOptionModel> options = new();

        foreach (var node in nodes)
        {
            var model = new DeckOptionModel
            {
                GroupIdx = short.Parse(node.Element("s_nGroupIdx")?.Value ?? "0"),
                GroupName = node.Element("s_szGroupName")?.Value ?? string.Empty,
                Explain = node.Element("s_szExplain")?.Value ?? string.Empty,
                Condition = node.Element("s_nCondition")?.Elements("condition").Select(e => short.Parse(e.Value)).ToArray()!,
                AT_Type = node.Element("s_nAT_Type")?.Elements("atType").Select(e => short.Parse(e.Value)).ToArray()!,
                Option = node.Element("s_nOption")?.Elements("option").Select(e => short.Parse(e.Value)).ToArray()!,
                Val = node.Element("s_nVal")?.Elements("value").Select(e => short.Parse(e.Value)).ToArray()!,
                Prob = node.Element("s_nProb")?.Elements("prob").Select(e => int.Parse(e.Value)).ToArray()!,
                Time = node.Element("s_nTime")?.Elements("time").Select(e => int.Parse(e.Value)).ToArray()!,
            };

            options.Add(model);
        }

        foreach (var opt in options)
            _deckOptions[opt.GroupIdx] = opt;
    }

    public DeckOptionModel? GetItem(short groupIdx)
    {
        _deckOptions.TryGetValue(groupIdx, out var model);
        return model;
    }

    public List<DeckOptionModel> GetAllItems()
    {
        return _deckOptions.Values.ToList();
    }
}
