using OnboardingKit;

namespace FileOrganizer
{
    /// <summary>
    /// FileOrganizer's guided-tour steps and legend rows (Build 1.5.0). This is the only
    /// app-specific content the OnboardingKit needs.
    ///
    /// TabIndex maps to the TabControl order in MainWindow.xaml:
    ///   0 Configuration · 1 Operations · 2 Statistics · 3 Exceptions · 4 Automation
    ///   5 Search · 6 History · 7 Duplicates · 8 Help
    /// Steps are ordered by the typical workflow, not tab order (e.g. Help/Settings near the end).
    /// </summary>
    public static class OnboardingContent
    {
        public static readonly TourStep[] TourSteps =
        {
            new("Welcome to File Organizer 👋",
                "A one-minute tour of the app. Press → or Enter for the next step, ← to go back, Esc to skip."),

            new("Configuration",
                "Start here: pick your source and destination folders, scan mode, copy engine, and how files are organized. Save your setup once and it's reused on every launch.",
                TabIndex: 0),

            new("Operations",
                "Scan your files into a queue, run a Dry Run to preview exactly what will move, then do a Live Copy or Move when it looks right.",
                TabIndex: 1),

            new("Exceptions",
                "Add files, folders, or patterns you never want touched (system folders, active projects). Exceptions apply to every scan, so set them once.",
                TabIndex: 3),

            new("Automation",
                "Turn a proven setup into hands-off rules: watch folders to sort files as they arrive, or schedule a sweep on a timer.",
                TabIndex: 4),

            new("Duplicates",
                "Scan for duplicate files, auto-select which copies to remove with a smart strategy, then review before deleting or moving them.",
                TabIndex: 7),

            new("Search & History",
                "Find files by name or by what's inside them on the Search tab, and review or re-run past operations from History.",
                TabIndex: 5),

            new("Help & these helpers",
                "The Help tab has the full guide and the ❔ Legend. You can turn these onboarding helpers (the ⓘ hints, the ❔ button and this tour) off there any time.",
                TabIndex: 8),

            new("You're all set ✓",
                "That's the tour. The ❔ Legend (top-right) explains every icon and colour, and you can replay this tour any time from the Help tab."),
        };

        public static readonly LegendItem[] LegendItems =
        {
            new("ⓘ", "Hover for a short explanation of a section"),
            new("❔", "Opens this legend of icons and colours"),
            new("✓", "Success — detected, verified, or completed"),
            new("⚠", "Warning — needs your attention (e.g. low disk space)"),
            new("❌", "Not found / failed (e.g. copy engine not installed)"),
            new("", "Accent — the active selection or detected value", Swatch: "#0078D4"),
        };
    }
}
