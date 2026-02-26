using System.Xml.Linq;
using DigitalWorldOnline.Commons.Models.Assets;

public sealed class TamerSkillAssetManager
{
    private static readonly Lazy<TamerSkillAssetManager> _instance = new(() => new TamerSkillAssetManager());

    public static TamerSkillAssetManager Instance => _instance.Value;

    private readonly Dictionary<int, TamerSkillModel> _tamerSkills = new();

    private TamerSkillAssetManager()
    {
        LoadItems();
    }

    private void LoadItems()
    {
        string fileName = "Skill_TamerSkill.xml";
        string folderName = Path.GetFileNameWithoutExtension(fileName);
        string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Skill");
        string filePath = Path.Combine(folderPath, fileName);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"O arquivo XML de habilidades de Tamer não foi encontrado: {filePath}");
        
        var xml = XDocument.Load(filePath);
        var skillNodes = xml.Descendants("TamerSkill");

        List<TamerSkillModel> skills = new();

        foreach (var node in skillNodes)
        {
            var model = new TamerSkillModel
            {
                SkillId = int.Parse(node.Element("s_nIndex")?.Value ?? "0"),
                SkillCode = int.Parse(node.Element("s_dwSkillCode")?.Value ?? "0"),
                Unknown = int.Parse(node.Element("unknow")?.Value ?? "0"),
                Type = int.Parse(node.Element("s_nType")?.Value ?? "0"),
                Unknown1 = int.Parse(node.Element("unknow1")?.Value ?? "0"),
                Factor1 = int.Parse(node.Element("s_dwFactor1")?.Value ?? "0"),
                Factor2 = int.Parse(node.Element("s_dwFactor2")?.Value ?? "0"),
                TamerSeqID = int.Parse(node.Element("s_dwTamer_SeqID")?.Value ?? "0"),
                DigimonSeqID = int.Parse(node.Element("s_dwDigimon_SeqID")?.Value ?? "0"),
                UseState = int.Parse(node.Element("s_nUseState")?.Value ?? "0"),
                UseAreaCheck = int.Parse(node.Element("s_nUse_Are_Check")?.Value ?? "0"),
                Available = int.Parse(node.Element("s_nAvailable")?.Value ?? "0"),
                Unknown3 = int.Parse(node.Element("unknow3")?.Value ?? "0")
            };

            skills.Add(model);
        }

        foreach (var skill in skills)
            _tamerSkills[skill.SkillId] = skill;
    }

    public TamerSkillModel? GetById(int index)
    {
        _tamerSkills.TryGetValue(index, out var skill);
        return skill;
    }

    public List<TamerSkillModel> GetAllSkills() => _tamerSkills.Values.ToList();
}
