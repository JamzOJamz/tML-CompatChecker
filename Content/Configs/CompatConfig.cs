using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace CompatChecker.Content.Configs;

public class CompatConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("Checks")]
    [DefaultValue(true)]
    public bool CheckMultiplayerCompat { get; set; }

    [Header("Preferences")]
    [DefaultValue(true)]
    public bool DrawEnabledModListOnMainMenu { get; set; }
    
    [DefaultValue(true)]
    public bool EnabledModListShowVersions { get; set; }

    [DefaultValue(true)] public bool ShowRecommendedFixes { get; set; }

    [Header("DeveloperOptions")]
    [DefaultValue(false)]
    public bool DisplayGitHubIssues { get; set; }

    [DefaultValue(false)] public bool ListLocalMods { get; set; }
}