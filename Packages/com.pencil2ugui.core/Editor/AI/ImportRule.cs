using System.Collections.Generic;

namespace Design2Ugui.AI
{
    public class ImportRule
    {
        public string pattern;
        public string componentType;
        public Dictionary<string, string> properties;
    }

    public class RuleSet
    {
        public List<ImportRule> namingRules = new List<ImportRule>();
        public Dictionary<string, string> layoutMappings = new Dictionary<string, string>();
        public List<string> optimizations = new List<string>();
    }
}
