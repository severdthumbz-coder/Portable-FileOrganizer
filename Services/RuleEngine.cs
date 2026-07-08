using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileOrganizer.Models;

namespace FileOrganizer.Services
{
    /// <summary>
    /// The routing decision for a single file: which rule matched and where it goes.
    /// </summary>
    public class RuleMatchResult
    {
        public bool Matched { get; set; }
        public OrganizationRule Rule { get; set; }
        public string DestinationPath { get; set; }   // full destination file path
    }

    /// <summary>
    /// Evaluates files against a set of OrganizationRules. Rules are evaluated
    /// top-down; the first enabled rule whose conditions match wins (first-match-wins),
    /// which lets users order specific rules above general ones.
    /// </summary>
    public class RuleEngine
    {
        private readonly List<OrganizationRule> _rules;

        public RuleEngine(IEnumerable<OrganizationRule> rules)
        {
            _rules = (rules ?? Enumerable.Empty<OrganizationRule>()).ToList();
        }

        /// <summary>
        /// Evaluates a file path against the rules. Returns the first matching rule's
        /// routing decision, or a non-match result if nothing applies.
        /// </summary>
        public RuleMatchResult Evaluate(string filePath)
        {
            var result = new RuleMatchResult { Matched = false };

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return result;

            FileInfo info;
            try
            {
                info = new FileInfo(filePath);
            }
            catch
            {
                return result;
            }

            foreach (var rule in _rules)
            {
                if (rule == null || !rule.IsEnabled) continue;
                if (rule.Conditions == null || rule.Conditions.Count == 0) continue;
                if (string.IsNullOrWhiteSpace(rule.DestinationFolder)) continue;

                if (RuleMatches(rule, info))
                {
                    var destPath = Path.Combine(rule.DestinationFolder, info.Name);
                    result.Matched = true;
                    result.Rule = rule;
                    result.DestinationPath = destPath;
                    return result; // first match wins
                }
            }

            return result;
        }

        private bool RuleMatches(OrganizationRule rule, FileInfo info)
        {
            if (rule.MatchMode == RuleMatchMode.All)
                return rule.Conditions.All(c => ConditionMatches(c, info));
            else
                return rule.Conditions.Any(c => ConditionMatches(c, info));
        }

        private bool ConditionMatches(RuleCondition condition, FileInfo info)
        {
            if (condition == null) return false;
            var value = condition.Value ?? string.Empty;

            try
            {
                switch (condition.ConditionType)
                {
                    case RuleConditionType.ExtensionEquals:
                    {
                        var ext = info.Extension; // includes leading dot, e.g. ".pdf"
                        var target = value.StartsWith(".") ? value : "." + value;
                        return string.Equals(ext, target, StringComparison.OrdinalIgnoreCase);
                    }

                    case RuleConditionType.NameContains:
                        return info.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

                    case RuleConditionType.NameMatchesRegex:
                        if (string.IsNullOrEmpty(value)) return false;
                        return Regex.IsMatch(info.Name, value, RegexOptions.IgnoreCase);

                    case RuleConditionType.SizeGreaterThan:
                        return long.TryParse(value, out var gt) && info.Length > gt;

                    case RuleConditionType.SizeLessThan:
                        return long.TryParse(value, out var lt) && info.Length < lt;

                    case RuleConditionType.ModifiedBefore:
                        return DateTime.TryParse(value, out var before) && info.LastWriteTime < before;

                    case RuleConditionType.ModifiedAfter:
                        return DateTime.TryParse(value, out var after) && info.LastWriteTime > after;

                    default:
                        return false;
                }
            }
            catch
            {
                // A malformed regex or unparsable value simply fails to match,
                // rather than throwing and aborting the whole evaluation.
                return false;
            }
        }
    }
}
