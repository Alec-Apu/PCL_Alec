namespace PCL.Core.UI.Navigation;

/// <summary>Stable page IDs shared with legacy navigation commands and persisted UI state.</summary>
public enum LauncherSection
{
    Launch = 0,
    Download = 1,
    Settings = 2,
    Tools = 3,
    Instances = 5
}

public static class LauncherNavigation
{
    /// <summary>Details belong to their owning section; transient pages retain the source section.</summary>
    public static LauncherSection ResolveSection(int page, int source = 0) => page switch
    {
        0 => LauncherSection.Launch,
        1 or 8 => LauncherSection.Download,
        2 => LauncherSection.Settings,
        3 => LauncherSection.Tools,
        5 or 7 or 12 => LauncherSection.Instances,
        6 or 9 or 10 => source is 6 or 9 or 10 ? LauncherSection.Launch : ResolveSection(source),
        _ => LauncherSection.Launch
    };
}
