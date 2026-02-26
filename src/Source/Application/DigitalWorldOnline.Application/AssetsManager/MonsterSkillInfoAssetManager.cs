using System.Collections.ObjectModel;
using System.Xml.Linq;
using DigitalWorldOnline.Commons.Models.Assets;

namespace DigitalWorldOnline.Application.AssetsManager
{
    public sealed class MonsterSkillInfoAssetManager
    {
        private static readonly Lazy<MonsterSkillInfoAssetManager> _instance = new(() => new MonsterSkillInfoAssetManager());
        public static MonsterSkillInfoAssetManager Instance => _instance.Value;

        private readonly Dictionary<int, MonsterSkillInfoAssetModel> _monsterSkills = new();

        private MonsterSkillInfoAssetManager()
        {
            LoadItems();
        }

        private void LoadItems()
        {
            string fileName = "Monster_MonstersSkill.xml";
            string folderName = Path.GetFileNameWithoutExtension("Skill");
            string folderPath = Path.Combine(AppContext.BaseDirectory, "Data", folderName);
            string filePath = Path.Combine(folderPath, fileName);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"MonsterSkills.xml not found: {filePath}");

            var xml = XDocument.Load(filePath);
            var skillNodes = xml.Descendants("MonsterSkill");

            foreach (var node in skillNodes)
            {
                try
                {
                    var skillId = int.Parse(node.Element("Skill_IDX")?.Value ?? "0");

                    var model = new MonsterSkillInfoAssetModel
                    {
                        SkillId = skillId,
                        Type = int.Parse(node.Element("MonsterID")?.Value ?? "0"),
                        CastingTime = int.Parse(node.Element("CastTime")?.Value ?? "0"),
                        Cooldown = int.Parse(node.Element("CoolTime")?.Value ?? "0"),
                        TargetCount = byte.Parse(node.Element("Target_Cnt")?.Value ?? "0"),
                        TargetMin = byte.Parse(node.Element("Target_MinCnt")?.Value ?? "0"),
                        TargetMax = byte.Parse(node.Element("Target_MaxCnt")?.Value ?? "0"),
                        UseTerms = byte.Parse(node.Element("UseTerms")?.Value ?? "0"),
                        SkillType = int.Parse(node.Element("Skill_Type")?.Value ?? "0"),
                        MinValue = int.Parse(node.Element("Eff_Val_Min")?.Value ?? "0"),
                        MaxValue = int.Parse(node.Element("Eff_Val_Max")?.Value ?? "0"),
                        RangeId = int.Parse(node.Element("RangeIDX")?.Value ?? "0"),
                        AnimationDelay = float.Parse(node.Element("Ani_Delay")?.Value?.Replace(',', '.') ?? "0"),
                        ActiveType = byte.Parse(node.Element("Activetype")?.Value ?? "0"),
                        NoticeTime = float.Parse(node.Element("NoticeTime")?.Value?.Replace(',', '.') ?? "0")
                    };

                    _monsterSkills[skillId] = model;
                }
                catch (Exception ex)
                {
                    // Log but continue processing other skills
                    Console.WriteLine($"Error parsing monster skill: {ex.Message}");
                }
            }
        }

        public MonsterSkillInfoAssetModel GetById(int id)
        {
            _monsterSkills.TryGetValue(id, out var skill);
            return skill;
        }


        public ReadOnlyDictionary<int, MonsterSkillInfoAssetModel> GetAllItems()
        {
            return new ReadOnlyDictionary<int, MonsterSkillInfoAssetModel>(_monsterSkills);
        }

        public Dictionary<int, MonsterSkillAssetModel> GetAllMonsterSkillsAsDictionary()
        {
            return _monsterSkills.Values
                .Select(skillInfo => new MonsterSkillAssetModel
                {
                    Type = skillInfo.Type,
                    SkillId = skillInfo.SkillId
                })
                .ToDictionary(skill => skill.SkillId);
        }
    }
}