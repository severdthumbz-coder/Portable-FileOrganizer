using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    ///
    /// Content conditions (ContentContains / ContentMatchesRegex) read the file's text via
    /// ContentExtractor. Extraction is LAZY (only performed if a content condition is actually
    /// evaluated) and CACHED per Evaluate call, so a rule set with several content conditions
    /// still reads each file at most once.
    /// </summary>
    public class RuleEngine
    {
        private readonly List<OrganizationRule> _rules;

        public RuleEngine(IEnumerable<OrganizationRule> rules)
        {
            _rules = (rules ?? Enumerable.Empty<OrganizationRule>()).ToList();
        }

        /// <summary>True if any enabled rule uses a content-based condition.</summary>
        public bool UsesContentConditions =>
            _rules.Any(r => r != null && r.IsEnabled && r.Conditions != null &&
                            r.Conditions.Any(c => IsContentCondition(c.ConditionType)));

        private static bool IsContentCondition(RuleConditionType t) =>
            t == RuleConditionType.ContentContains || t == RuleConditionType.ContentMatchesRegex;

        /// <summary>
        /// Evaluates a file path against the rules. Returns the first matching rule's
        /// routing decision, or a non-match result if nothing applies.
        /// </summary>
        public async Task<RuleMatchResult> EvaluateAsync(string filePath, CancellationToken cancellationToken = default)
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

            // Lazily-extracted, per-file text cache. Null until a content condition needs it.
            string cachedText = null;
            bool extractionAttempted = false;

            async Task<string> GetTextAsync()
            {
                if (!extractionAttempted)
                {
                    extractionAttempted = true;
                    var extraction = await ContentExtractor.ExtractAsync(filePath, cancellationToken);
                    cachedText = extraction.Success ? extraction.Text : null;
                }
                return cachedText;
            }

            foreach (var rule in _rules)
            {
                if (rule == null || !rule.IsEnabled) continue;
                if (rule.Conditions == null || rule.Conditions.Count == 0) continue;
                if (string.IsNullOrWhiteSpace(rule.DestinationFolder)) continue;

                if (await RuleMatchesAsync(rule, info, GetTextAsync))
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

        private async Task<bool> RuleMatchesAsync(OrganizationRule rule, FileInfo info, Func<Task<string>> getText)
        {
            if (rule.MatchMode == RuleMatchMode.All)
            {
                // Cheap (metadata) conditions first: if any fails, we never touch the disk for content.
                foreach (var c in rule.Conditions.Where(c => !IsContentCondition(c.ConditionType)))
                    if (!ConditionMatches(c, info)) return false;

                foreach (var c in rule.Conditions.Where(c => IsContentCondition(c.ConditionType)))
                    if (!await ContentConditionMatchesAsync(c, getText)) return false;

                return true;
            }
            else // Any
            {
                // Cheap conditions first: a metadata hit short-circuits before any extraction.
                foreach (var c in rule.Conditions.Where(c => !IsContentCondition(c.ConditionType)))
                    if (ConditionMatches(c, info)) return true;

                foreach (var c in rule.Conditions.Where(c => IsContentCondition(c.ConditionType)))
                    if (await ContentConditionMatchesAsync(c, getText)) return true;

                return false;
            }
        }

        private async Task<bool> ContentConditionMatchesAsync(RuleCondition condition, Func<Task<string>> getText)
        {
            var value = condition.Value ?? string.Empty;
            if (string.IsNullOrEmpty(value)) return false;

            var text = await getText();
            if (string.IsNullOrEmpty(text)) return false; // unreadable / unsupported -> no match

            try
            {
                switch (condition.ConditionType)
                {
                    case RuleConditionType.ContentContains:
                        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

                    case RuleConditionType.ContentMatchesRegex:
                        return Regex.IsMatch(text, value, RegexOptions.IgnoreCase);

                    default:
                        return false;
                }
            }
            catch
            {
                return false; // malformed regex fails to match rather than throwing
            }
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
                return false;
            }
        }
    }
}
