using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Assets;
using System.Xml.Linq;

public sealed class SkillAssetManager
{
    private static readonly Lazy<SkillAssetManager> _instance = new(() => new SkillAssetManager());

    public static SkillAssetManager Instance => _instance.Value;

    private readonly Dictionary<int, SkillInfoAssetModel> _skills = new();

    private SkillAssetManager()
    {
        LoadItems();
    }

    private void LoadItems()
    {
        string fileName = "Skill_Skill.xml";
        string folderName = Path.GetFileNameWithoutExtension("Skill");
        string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", folderName);
        string filePath = Path.Combine(folderPath, fileName);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"O arquivo XML de skills não foi encontrado: {filePath}");

        var xml = XDocument.Load(filePath);

        var skills = xml.Descendants("SkillData").Select(x =>
        {
            var skillId = int.Parse(x.Element("s_dwID")?.Value ?? "0");
            var name = x.Element("s_szName")?.Value ?? string.Empty;
            var family = (DigimonFamilyEnum)int.Parse(x.Element("s_nFamilyType")?.Value ?? "0");
            var attribute = (DigimonAttributeEnum)int.Parse(x.Element("s_nAttributeType")?.Value ?? "0");
            var nature = (DigimonNatureEnum)int.Parse(x.Element("s_nNatureType")?.Value ?? "0");
            var useDS = int.Parse(x.Element("s_nUseDS")?.Value ?? "0");
            var useHP = int.Parse(x.Element("s_nUseHP")?.Value ?? "0");
            var castingTime = float.Parse(x.Element("s_fCastingTime")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            var cooldown = int.Parse(x.Element("s_fCooldownTime")?.Value ?? "0");
            var maxLevel = byte.Parse(x.Element("s_nMaxLevel")?.Value ?? "0");
            var levelupPoint = byte.Parse(x.Element("s_nLevelupPoint")?.Value ?? "0");
            var target = byte.Parse(x.Element("s_nTarget")?.Value ?? "0");
            var attSphere = int.Parse(x.Element("s_nAttSphere")?.Value ?? "0");
            var minDmg = int.Parse(x.Element("s_fAttRange_MinDmg")?.Value ?? "0");
            var maxDmg = int.Parse(x.Element("s_fAttRange_MaxDmg")?.Value ?? "0");
            var range = int.Parse(x.Element("s_fAttRange")?.Value ?? "0");
            var limitLevel = byte.Parse(x.Element("s_nLimitLevel")?.Value ?? "0");
            var memorySkill = int.Parse(x.Element("s_nMemorySkill")?.Value ?? "0");
            var reqItem = int.Parse(x.Element("s_nReq_Item")?.Value ?? "0");

            var skillApplyList = x.Element("SkillApply")?
                .Elements("IncreaseApply")
                .Select(apply => new SkillCodeApplyAssetModel
                {
                    Attribute = (SkillCodeApplyAttributeEnum)int.Parse(apply.Element("s_nA")?.Value ?? "0"),
                    Chance = int.Parse(apply.Element("s_nInvoke_Rate")?.Value ?? "0"),
                    Value = int.Parse(apply.Element("s_nB")?.Value ?? "0"),
                    AdditionalValue = int.Parse(apply.Element("s_nC")?.Value ?? "0"),
                    BuffCode = int.Parse(apply.Element("s_nBuffCode")?.Value ?? "0"),
                    Type = (SkillCodeApplyTypeEnum)int.Parse(apply.Element("s_nID")?.Value ?? "0"),
                    IncreaseValue = int.Parse(apply.Element("s_nIncrease_B_Point")?.Value ?? "0"),
                }).ToList() ?? new List<SkillCodeApplyAssetModel>();

            return new SkillInfoAssetModel(
                skillId,
                name,
                family,
                attribute,
                nature,
                useDS,
                useHP,
                castingTime,
                cooldown,
                maxLevel,
                levelupPoint,
                target,
                attSphere,
                minDmg,
                maxDmg,
                range,
                limitLevel,
                memorySkill,
                reqItem,
                skillApplyList
            );
        });
        foreach (var skill in skills)
            _skills[skill.SkillId] = skill;

    }

    public void UpdateCastingTimes(string dbXmlPath, string skillXmlPath, string outputPath)
    {
        var dbXml = XDocument.Load(dbXmlPath);
        var skillXml = XDocument.Load(skillXmlPath);

        var dbRecords = dbXml.Descendants("DATA_RECORD")
            .Select(x => new
            {
                SkillId = string.Join("", x.Element("SkillId")?.Value.Split(',') ?? Array.Empty<string>()),
                CastingTime = x.Element("CastingTime")?.Value
            })
            .GroupBy(r => r.SkillId)
            .ToDictionary(g => g.Key, g => g.First().CastingTime);

        var duplicates = dbXml.Descendants("DATA_RECORD")
            .Select(x => string.Join("", x.Element("SkillId")?.Value.Split(',') ?? Array.Empty<string>()))
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicates)
        {
            Console.WriteLine($"SkillId duplicado: {dup.Key} ({dup.Count()} vezes)");
        }

        int updatedCount = 0;

        foreach (var skill in skillXml.Descendants("SkillData"))
        {
            var skillIdElement = skill.Element("s_dwID");
            var castingTimeElement = skill.Element("s_fCastingTime");

            if (skillIdElement == null || castingTimeElement == null)
                continue;

            var skillId = skillIdElement.Value.Trim();

            if (dbRecords.TryGetValue(skillId, out var newCastingTime) && !string.IsNullOrWhiteSpace(newCastingTime))
            {
                castingTimeElement.Value = newCastingTime.Trim();
                updatedCount++;
            }
        }

        skillXml.Save(outputPath);
    }

    public SkillInfoAssetModel? GetItemById(int id)
    {
        _skills.TryGetValue(id, out var skill);
        return skill;
    }

    public List<SkillInfoAssetModel> GetAllItems() => _skills.Values.ToList();
}
