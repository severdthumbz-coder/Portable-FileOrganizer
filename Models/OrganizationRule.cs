using System;
using System.Collections.Generic;

namespace FileOrganizer.Models
{
    /// <summary>
    /// The kind of test a single rule condition performs against a file.
    /// </summary>
    public enum RuleConditionType
    {
        /// <summary>File extension equals a value (e.g. ".pdf"). Case-insensitive.</summary>
        ExtensionEquals,
        /// <summary>File name contains a substring. Case-insensitive.</summary>
        NameContains,
        /// <summary>File name matches a .NET regular expression.</summary>
        NameMatchesRegex,
        /// <summary>File size (bytes) is greater than a value.</summary>
        SizeGreaterThan,
        /// <summary>File size (bytes) is less than a value.</summary>
        SizeLessThan,
        /// <summary>File was modified before a given date.</summary>
        ModifiedBefore,
        /// <summary>File was modified after a given date.</summary>
        ModifiedAfter,
        /// <summary>The file's text content contains a substring (PDF/Office/text files). Case-insensitive.</summary>
        ContentContains,
        /// <summary>The file's text content matches a regular expression (PDF/Office/text files).</summary>
        ContentMatchesRegex
    }

    /// <summary>
    /// How the conditions in a rule combine.
    /// </summary>
    public enum RuleMatchMode
    {
        /// <summary>All conditions must match (AND).</summary>
        All,
        /// <summary>Any condition may match (OR).</summary>
        Any
    }

    /// <summary>
    /// A single test within a rule. Value is stored as a string and parsed
    /// according to ConditionType (numbers for size, dates for modified, text otherwise).
    /// </summary>
    public class RuleCondition
    {
        public RuleConditionType ConditionType { get; set; } = RuleConditionType.ExtensionEquals;
        public string Value { get; set; } = string.Empty;

        public string ConditionDisplay
        {
            get
            {
                switch (ConditionType)
                {
                    case RuleConditionType.ExtensionEquals: return "Extension is";
                    case RuleConditionType.NameContains: return "Name contains";
                    case RuleConditionType.NameMatchesRegex: return "Name matches regex";
                    case RuleConditionType.SizeGreaterThan: return "Size greater than (bytes)";
                    case RuleConditionType.SizeLessThan: return "Size less than (bytes)";
                    case RuleConditionType.ModifiedBefore: return "Modified before";
                    case RuleConditionType.ModifiedAfter: return "Modified after";
                    case RuleConditionType.ContentContains: return "Content contains";
                    case RuleConditionType.ContentMatchesRegex: return "Content matches regex";
                    default: return ConditionType.ToString();
                }
            }
        }
    }

    /// <summary>
    /// A user-defined organization rule: when a file matches the conditions,
    /// it is routed to DestinationFolder. Rules are evaluated top-down; the first
    /// matching rule wins (see RuleEngine).
    /// </summary>
    public class OrganizationRule
    {
        public string Name { get; set; } = "New Rule";
        public bool IsEnabled { get; set; } = true;
        public RuleMatchMode MatchMode { get; set; } = RuleMatchMode.All;
        public List<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();

        /// <summary>Absolute destination folder for files matching this rule.</summary>
        public string DestinationFolder { get; set; } = string.Empty;

        /// <summary>Move (true) or Copy (false) matched files.</summary>
        public bool IsMove { get; set; } = true;

        /// <summary>How to handle a name collision at the destination.</summary>
        public FileConflictResolution ConflictResolution { get; set; } = FileConflictResolution.Skip;

        // Convenience display helpers for the UI grid.
        public string OperationDisplay => IsMove ? "Move" : "Copy";
        public int ConditionCount => Conditions?.Count ?? 0;
        public string ConditionSummary =>
            (Conditions == null || Conditions.Count == 0)
                ? "(no conditions — matches nothing)"
                : $"{Conditions.Count} condition(s), match {MatchMode}";
    }
}
