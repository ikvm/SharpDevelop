using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ICSharpCode.AiAgent
{
    public class SkillManager
    {
        private static SkillManager _instance;
        private readonly List<IAiSkill> _skills;

        public static SkillManager Instance => _instance ?? (_instance = new SkillManager());

        public IReadOnlyList<IAiSkill> Skills => _skills.AsReadOnly();

        private SkillManager()
        {
            _skills = new List<IAiSkill>
            {
                new CodeGenerationSkill(),
                new ExplainCodeSkill(),
                new OptimizeCodeSkill(),
                new RefactorCodeSkill(),
                new DebugCodeSkill(),
                new LocalToolSkill()
            };
        }

        public IAiSkill GetSkill(string id)
        {
            return _skills.FirstOrDefault(s => s.Id == id);
        }

        public void RegisterSkill(IAiSkill skill)
        {
            var existing = _skills.FirstOrDefault(s => s.Id == skill.Id);
            if (existing != null)
                _skills.Remove(existing);
            _skills.Add(skill);
        }

        public bool RemoveSkill(string id)
        {
            var skill = GetSkill(id);
            if (skill != null)
                return _skills.Remove(skill);
            return false;
        }
    }
}