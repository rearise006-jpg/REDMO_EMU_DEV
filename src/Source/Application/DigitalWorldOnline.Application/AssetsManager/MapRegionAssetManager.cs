using System.Xml.Linq;
using DigitalWorldOnline.Commons.Models.Assets;

public sealed class MapRegionAssetManager
{
    private static readonly Lazy<MapRegionAssetManager> _instance = new(() => new MapRegionAssetManager());

    public static MapRegionAssetManager Instance => _instance.Value;

    private readonly List<MapRegionAssetModel> _regions = new();

    private MapRegionAssetManager()
    {
        LoadRegions();
    }

    private void LoadRegions()
    {
        string fileName = "MapRegion.xml";
        string folderName = Path.GetFileNameWithoutExtension("Maps");
        string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", folderName);
        string filePath = Path.Combine(folderPath, fileName);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"MapRegion.xml not found: {filePath}");

        var xml = XDocument.Load(filePath);
        var mapInfoNodes = xml.Descendants("MapInfo");

        foreach (var info in mapInfoNodes)
        {
            var model = new MapRegionAssetModel
            {
                MapID = int.Parse(info.Element("s_dwMapID")?.Value ?? "0"),
                CenterX = int.Parse(info.Element("s_nCenterX")?.Value ?? "0"),
                CenterY = int.Parse(info.Element("s_nCenterY")?.Value ?? "0"),
                Radius = int.Parse(info.Element("s_nRadius")?.Value ?? "0"),
                FatigueType = byte.Parse(info.Element("s_nFatigue_Type")?.Value ?? "0"),
                FatigueDebuff = byte.Parse(info.Element("s_nFatigue_DeBuff")?.Value ?? "0"),
                FatigueStartTime = int.Parse(info.Element("s_nFatigue_StartTime")?.Value ?? "0"),
                FatigueAddTime = int.Parse(info.Element("s_nFatigue_AddTime")?.Value ?? "0"),
                FatigueAddPoint = int.Parse(info.Element("s_nFatigue_AddPoint")?.Value ?? "0"),
            };

            _regions.Add(model);
        }
    }


    public List<MapRegionAssetModel> GetAllRegions()
    {
        return _regions;
    }

    public List<MapRegionAssetModel> GetByMapId(int mapId)
    {
        return _regions.Where(r => r.MapID == mapId).ToList();
    }
}
