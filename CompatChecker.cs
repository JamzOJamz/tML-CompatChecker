using System;
using System.Linq;
using CompatChecker.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using ReLogic.OS;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace CompatChecker;

public class CompatChecker : Mod
{
    /*public CompatChecker() // Runs before any mod is loaded
    {
    }*/

    private const string DiscordURL = "https://discord.gg/F9bThEE9FV"; // Jamz's Mods, #cc-chat

    private readonly Item _fakeItem = new();
    private bool _lastHoveringBackToMainMenuText;
    private bool _lastHoveringHelpContributeButton;
    private bool _lastHoveringModsEnabledText;

    public override void Load()
    {
        On_Main.DrawVersionNumber += MainDrawVersionNumber_Detour;
    }

    private void MainDrawVersionNumber_Detour(On_Main.orig_DrawVersionNumber orig, Color menucolor, float upbump)
    {
        if (Main.menuMode != 0 || !ModContent.GetInstance<CompatConfig>().DrawEnabledModListOnMainMenu)
        {
            if (Main.menuMode == 888 && Main.MenuUI._currentState == Interface.modsMenu)
                DrawInModsMenu(menucolor);
            return;
        }

        var hasOverhaul = ModLoader.HasMod("TerrariaOverhaul");
        var enabledModsMessage =
            Language.GetTextValue("tModLoader.MenuModsEnabled", Math.Max(0, ModLoader.Mods.Length - 1)) + " \u2611";
        var enabledModsMessageSize = FontAssets.MouseText.Value.MeasureString(enabledModsMessage);

        // Set draw pos to right corner instead of left if Overhaul is loaded
        var drawPos = new Vector2(9, 12);
        if (hasOverhaul)
            drawPos.X = Main.screenWidth - 12 - enabledModsMessageSize.X;
        else if (Main.showFrameRate)
            drawPos.Y += 22;

        var hovered = Main.MouseScreen.Between(drawPos, drawPos + enabledModsMessageSize);
        if (hovered)
        {
            Main.LocalPlayer.mouseInterface = true;
            if (!_lastHoveringModsEnabledText)
                SoundEngine.PlaySound(SoundID.MenuTick);
            _fakeItem.SetDefaults(0, true);
            const string textValue = "[c/99E550:Enabled Mods] [c/A49DAE:(Added by Compatibility Checker)]";
            var tooltipValue = "";
            var alphaSortedMods = ModLoader.Mods.OrderBy(mod => mod.DisplayName).ToArray();
            foreach (var mod in alphaSortedMods)
            {
                if (mod.Name == "ModLoader") continue;
                var val = $"- {mod.DisplayName} v{mod.Version}";
                if (!CompatSystem.WorkshopModNames.Contains(mod.Name))
                    val += " [c/A49DAE:(Local)]";
                tooltipValue += val + "\n";
            }

            _fakeItem.SetNameOverride(textValue);
            _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
            _fakeItem.type = ItemID.IronPickaxe;
            _fakeItem.scale = 0f;
            _fakeItem.rare = ItemRarityID.Yellow;
            _fakeItem.value = -1;
            Main.HoverItem = _fakeItem;
            Main.instance.MouseText("");
            Main.mouseText = true;

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                SoundEngine.PlaySound(SoundID.MenuOpen);
                Main.mouseLeftRelease = false;
                Main.menuMode = Interface.modsMenuID;
            }

            _lastHoveringModsEnabledText = true;
        }
        else
        {
            _lastHoveringModsEnabledText = false;
        }

        DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, enabledModsMessage, drawPos,
            menucolor, 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f, true, 0.375f);

        /*DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
            DisplayNameClean, drawPos, Color.White, 0f, Vector2.Zero, 1.11f,
            SpriteEffects.None, 0f, true);
        drawPos.Y += 34;

        if (CompatSystem.CompatibilityData == null) // Loading state
        {
            var dots = new string('.', (int)(Main.timeForVisualEffects / 20 % 4));
            DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, "Loading" + dots, drawPos,
                menucolor * 0.8f, 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f, true);
        }
        else
        {
            var enabledModsMessage =
                Language.GetTextValue("tModLoader.MenuModsEnabled", Math.Max(0, ModLoader.Mods.Length - 1)) + " \u2630";
            var enabledModsMessageSize = FontAssets.MouseText.Value.MeasureString(enabledModsMessage);
            if (Main.MouseScreen.Between(drawPos, drawPos + enabledModsMessageSize))
            {
                Main.LocalPlayer.mouseInterface = true;
                _fakeItem.SetDefaults(0, true);
                const string textValue = "Enabled Mods";
                var tooltipValue = "";
                for (var i = 1; i < ModLoader.Mods.Length; i++)
                {
                    var mod = ModLoader.Mods[i];
                    tooltipValue += $"- {mod.DisplayName} v{mod.Version}\n";
                }

                _fakeItem.SetNameOverride(textValue);
                _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                _fakeItem.type = ItemID.IronPickaxe;
                _fakeItem.scale = 0f;
                _fakeItem.rare = ItemRarityID.Yellow;
                _fakeItem.value = -1;
                Main.HoverItem = _fakeItem;
                Main.instance.MouseText("");
                Main.mouseText = true;
            }

            DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, enabledModsMessage, drawPos,
                menucolor * 0.8f, 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f, true);
            drawPos.Y += 32;

            var data = CompatSystem.CompatibilityData;
            var verifiedMultiplayerSupportCount = data.Individual.MPCompatible.Length;
            if (verifiedMultiplayerSupportCount > 0)
            {
                var verifiedMultiplayerSupportMessage =
                    $"{verifiedMultiplayerSupportCount} Mod(s) Verified Multiplayer Compatible \u2713";
                var verifiedMultiplayerSupportMessageSize =
                    FontAssets.MouseText.Value.MeasureString(verifiedMultiplayerSupportMessage);
                if (Main.MouseScreen.Between(drawPos, drawPos + verifiedMultiplayerSupportMessageSize))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    _fakeItem.SetDefaults(0, true);
                    const string textValue = "Multiplayer Compatible Mods";
                    var tooltipValue = "";
                    foreach (var mod in data.Individual.MPCompatible)
                    {
                        var localMod = CompatSystem.ModIDLookup[mod.ModID];
                        tooltipValue += $"• {localMod.DisplayName} v{localMod.Version} \n";
                    }

                    _fakeItem.SetNameOverride(textValue);
                    _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                    _fakeItem.type = ItemID.IronPickaxe;
                    _fakeItem.scale = 0f;
                    _fakeItem.rare = ItemRarityID.Yellow;
                    _fakeItem.value = -1;
                    Main.HoverItem = _fakeItem;
                    Main.instance.MouseText("");
                    Main.mouseText = true;
                }

                DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
                    verifiedMultiplayerSupportMessage, drawPos,
                    new Color(0, 200, 81), 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f);
                drawPos.Y += 32;
            }

            var unstableInMultiplayerCount = data.Individual.MPUnstable.Length;
            if (unstableInMultiplayerCount > 0)
            {
                var unstableInMultiplayerMessage =
                    $"{unstableInMultiplayerCount} Mod(s) Unstable in Multiplayer \u26a0";
                var unstableInMultiplayerMessageSize =
                    FontAssets.MouseText.Value.MeasureString(unstableInMultiplayerMessage);
                if (Main.MouseScreen.Between(drawPos, drawPos + unstableInMultiplayerMessageSize))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    _fakeItem.SetDefaults(0, true);
                    const string textValue = "Mods Unstable in Multiplayer";
                    var tooltipValue = "";
                    foreach (var mod in data.Individual.MPUnstable)
                    {
                        var localMod = CompatSystem.ModIDLookup[mod.ModID];
                        var val = $"• {localMod.DisplayName} v{localMod.Version}";
                        if (!string.IsNullOrEmpty(mod.Note)) val += " — [c/FFBB33:" + mod.Note + "]";
                        tooltipValue += val + "\n";
                    }

                    _fakeItem.SetNameOverride(textValue);
                    _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                    _fakeItem.type = ItemID.IronPickaxe;
                    _fakeItem.scale = 0f;
                    _fakeItem.rare = ItemRarityID.Yellow;
                    _fakeItem.value = -1;
                    Main.HoverItem = _fakeItem;
                    Main.instance.MouseText("");
                    Main.mouseText = true;
                }

                DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, unstableInMultiplayerMessage,
                    drawPos,
                    new Color(255, 187, 51), 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f);
                drawPos.Y += 32;
            }

            var potentialIssuesCount = data.Individual.MPUnstable.Length + data.Individual.MPIncompatible.Length;
            var potentialIssuesMessage = potentialIssuesCount > 0
                ? $"{potentialIssuesCount} Potential Issue(s) \u26ec"
                : "No Issues Detected \u2713";
            //var potentialIssuesMessageSize = FontAssets.MouseText.Value.MeasureString(potentialIssuesMessage);
            //var drawPositives = Main.MouseScreen.Between(drawPos, drawPos + potentialIssuesMessageSize);
            DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, potentialIssuesMessage, drawPos,
                Color.LightGray, 0f, Vector2.Zero, 1.02f,
                SpriteEffects.None, 0f);
            drawPos.Y += 32;
        }*/

        orig(menucolor, upbump);
    }

    private void DrawInModsMenu(Color menucolor)
    {
        var drawPos = new Vector2(9, 12);
        if (ModLoader.HasMod("TerrariaOverhaul"))
            drawPos.Y += 205;
        else if (Main.showFrameRate)
            drawPos.Y += 22;

        DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
            DisplayNameClean, drawPos, Color.White, 0f, Vector2.Zero, 1.05f,
            SpriteEffects.None, 0f, true, 0.5f);
        drawPos.Y += 34;

        // Draw "Back to Main Menu" button in top right
        var anyModsNeedReload = Interface.modsMenu.items.Any(i => i.NeedsReload);
        const string backToMainMenuText = "Back to Main Menu";
        var backToMainMenuTextSize = FontAssets.MouseText.Value.MeasureString(backToMainMenuText) + new Vector2(5, 0);
        var topRightDrawPos = new Vector2(Main.screenWidth - 18 - backToMainMenuTextSize.X, 12);
        var backToMainMenuHovered = Main.MouseScreen.Between(topRightDrawPos, topRightDrawPos + backToMainMenuTextSize);
        if (backToMainMenuHovered)
        {
            Main.LocalPlayer.mouseInterface = true;
            if (!_lastHoveringBackToMainMenuText)
                SoundEngine.PlaySound(SoundID.MenuTick);
            _fakeItem.SetDefaults(0, true);
            _fakeItem.SetNameOverride("Click to quickly return to the main menu");
            _fakeItem.type = ItemID.IronPickaxe;
            _fakeItem.scale = 0f;
            _fakeItem.value = -1;
            Main.HoverItem = _fakeItem;
            Main.instance.MouseText("");
            Main.mouseText = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                Main.mouseLeftRelease = false;

                // To prevent entering the game with Configs that violate ReloadRequired
                if (ConfigManager.AnyModNeedsReload())
                {
                    Main.menuMode = Interface.reloadModsID;
                    return;
                }

                // If auto reloading required mods is enabled, check if any mods need reloading and reload as required
                if (ModLoader.autoReloadRequiredModsLeavingModsScreen && anyModsNeedReload)
                {
                    Main.menuMode = Interface.reloadModsID;
                    return;
                }

                ConfigManager.OnChangedAll();
                Main.menuMode = 0;
            }

            _lastHoveringBackToMainMenuText = true;
        }
        else
        {
            _lastHoveringBackToMainMenuText = false;
        }

        DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, backToMainMenuText,
            topRightDrawPos,
            backToMainMenuHovered ? Color.Yellow : menucolor, 0f, Vector2.Zero, 1.05f,
            SpriteEffects.None, 0f, !backToMainMenuHovered, 0.375f);

        // Draw note and button to help contribute to the compatibility data project
        var bottomDrawPos = new Vector2(9, Main.screenHeight - 35);

        const string helpContributeButton = "Click to Help Contribute Data \u26ec";
        var helpContributeButtonSize = FontAssets.MouseText.Value.MeasureString(helpContributeButton);
        var helpHovered = Main.MouseScreen.Between(bottomDrawPos, bottomDrawPos + helpContributeButtonSize);
        if (helpHovered)
        {
            Main.LocalPlayer.mouseInterface = true;
            if (!_lastHoveringHelpContributeButton)
                SoundEngine.PlaySound(SoundID.MenuTick);
            _fakeItem.SetDefaults(0, true);
            _fakeItem.SetNameOverride("Found an error or missing info? Help us fix it!");
            _fakeItem.ToolTip = new ItemTooltip($"[c/7F8CFF:{DiscordURL}]");
            _fakeItem.type = ItemID.IronPickaxe;
            _fakeItem.scale = 0f;
            _fakeItem.value = -1;
            Main.HoverItem = _fakeItem;
            Main.instance.MouseText("");
            Main.mouseText = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                SoundEngine.PlaySound(SoundID.MenuOpen);
                Main.mouseLeftRelease = false;
                try
                {
                    Platform.Get<IPathService>().OpenURL(DiscordURL);
                }
                catch
                {
                    Console.WriteLine("Failed to open link?!");
                }
            }

            _lastHoveringHelpContributeButton = true;
        }
        else
        {
            _lastHoveringHelpContributeButton = false;
        }

        DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
            helpContributeButton, bottomDrawPos, helpHovered ? new Color(244, 255, 0) : Color.LightGray, 0f,
            Vector2.Zero, 1f,
            SpriteEffects.None, 0f, alphaMult: 0.5f);
        var smallWidthRes = Main.screenWidth < 1780;
        bottomDrawPos.Y -= smallWidthRes ? 59 : 31;

        var noteText = smallWidthRes
            ? "Note: Compatibility info is suggestive\nand may be outdated/inaccurate."
            : "Note: Compatibility info is suggestive and may be outdated/inaccurate.";
        DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
            noteText, bottomDrawPos, Color.White, 0f,
            Vector2.Zero, 1f,
            SpriteEffects.None, 0f, true, 0.5f);

        if (CompatSystem.CompatibilityData == null || !string.IsNullOrEmpty(CompatSystem.RequestError) ||
            Interface.modsMenu.loading || anyModsNeedReload) // Loading state
        {
            if (!string.IsNullOrEmpty(CompatSystem.RequestError))
            {
                DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, CompatSystem.RequestError,
                    drawPos,
                    menucolor * 0.8f, 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f, true);
            }
            else
            {
                var dots = new string('.', (int)(Main.timeForVisualEffects / 20 % 4));
                var doingThing = CompatSystem.CompatibilityData != null && anyModsNeedReload
                    ? "Reload Required"
                    : "Loading";
                DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, doingThing + dots, drawPos,
                    Color.LightGray, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f, alphaMult: 0.55f);
            }
        }
        else
        {
            var compatConfig = ModContent.GetInstance<CompatConfig>();
            var data = CompatSystem.CompatibilityData;

            if (compatConfig.CheckMultiplayerCompat)
            {
                var clientSideMods = ModLoader.Mods.Where(mod => mod.Side == ModSide.Client).ToArray();
                var verifiedMultiplayerSupportMods = data.Individual.MPCompatible;
                // Calculate the full verified multiplayer support nids, which should be the verified multiplayer support mods plus any client side mods NOT in the verified list
                var fullVerifiedMultiplayerSupportIDs = verifiedMultiplayerSupportMods.Select(x => x.ModID).ToList();
                foreach (var mod in clientSideMods)
                {
                    if (!CompatSystem.ModIDByName.TryGetValue(mod.Name, out var modId)) continue;
                    if (verifiedMultiplayerSupportMods.Any(x => x.ModID == modId)) continue;
                    fullVerifiedMultiplayerSupportIDs.Add(modId);
                }

                var fullVerifiedMultiplayerSupportMods =
                    fullVerifiedMultiplayerSupportIDs.Select(x => CompatSystem.LocalModByID[x])
                        .OrderBy(x => x.DisplayName)
                        .ToArray();
                var verifiedMultiplayerSupportCount = fullVerifiedMultiplayerSupportMods.Length;
                if (verifiedMultiplayerSupportCount > 0)
                {
                    var verifiedMultiplayerSupportMessage =
                        $"{verifiedMultiplayerSupportCount} Mod(s) Verified Multiplayer Compatible \u2713";
                    var verifiedMultiplayerSupportMessageSize =
                        FontAssets.MouseText.Value.MeasureString(verifiedMultiplayerSupportMessage);
                    if (Main.MouseScreen.Between(drawPos, drawPos + verifiedMultiplayerSupportMessageSize))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        _fakeItem.SetDefaults(0, true);
                        const string textValue = "[c/FFFFFF:Multiplayer Compatible Mods]";
                        var tooltipValue = "";
                        foreach (var localMod in fullVerifiedMultiplayerSupportMods)
                        {
                            var val = $"• {localMod.DisplayName} v{localMod.Version}";
                            var mpCompat = CompatSystem.ModIDByName.TryGetValue(localMod.Name, out var modId)
                                ? data.Individual.MPCompatible.FirstOrDefault(x => x.ModID == modId)
                                : null;
                            if (mpCompat != null && !string.IsNullOrEmpty(mpCompat.Note))
                                val += " — [c/99E550:" + mpCompat.Note + "]";
                            tooltipValue += val + "\n";
                        }

                        _fakeItem.SetNameOverride(textValue);
                        _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                        _fakeItem.type = ItemID.IronPickaxe;
                        _fakeItem.scale = 0f;
                        _fakeItem.rare = ItemRarityID.Yellow;
                        _fakeItem.value = -1;
                        Main.HoverItem = _fakeItem;
                        Main.instance.MouseText("");
                        Main.mouseText = true;
                    }

                    DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
                        verifiedMultiplayerSupportMessage, drawPos,
                        new Color(153, 229, 80), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f, alphaMult: 0.55f);
                    drawPos.Y += 32;
                }

                var unstableInMultiplayerCount = data.Individual.MPUnstable.Length;
                if (unstableInMultiplayerCount > 0)
                {
                    var unstableInMultiplayerMessage =
                        $"{unstableInMultiplayerCount} Mod(s) Unstable in Multiplayer \u26a0";
                    var unstableInMultiplayerMessageSize =
                        FontAssets.MouseText.Value.MeasureString(unstableInMultiplayerMessage);
                    if (Main.MouseScreen.Between(drawPos, drawPos + unstableInMultiplayerMessageSize))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        _fakeItem.SetDefaults(0, true);
                        const string textValue = "[c/FFFFFF:Mods Unstable in Multiplayer]";
                        var tooltipValue = "";
                        foreach (var mod in data.Individual.MPUnstable)
                        {
                            var localMod = CompatSystem.LocalModByID[mod.ModID];
                            var val = $"• {localMod.DisplayName} v{localMod.Version}";
                            if (!string.IsNullOrEmpty(mod.Note)) val += " — [c/F2A754:" + mod.Note + "]";
                            if (compatConfig.ShowRecommendedFixes && mod.FixMods is { Length: > 0 })
                            {
                                val += "\n  [c/BDB8C4:- Recommended Compatibility Add-ons: ";
                                foreach (var fixModName in mod.FixMods) val += fixModName + ", ";
                                val = val[..^2];
                                val += "]";
                            }

                            if (compatConfig.DisplayGitHubIssues && mod.IssueIDs is { Length: > 0 })
                            {
                                val += "\n  [c/BDB8C4:- Relevant GitHub Issues: ";
                                foreach (var issueID in mod.IssueIDs) val += "#" + issueID + ", ";
                                val = val[..^2];
                                val += $" ({data.Extra.GithubInfo.First(x => x.ModID == mod.ModID).Repo})]";
                            }

                            tooltipValue += val + "\n";
                        }

                        _fakeItem.SetNameOverride(textValue);
                        _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                        _fakeItem.type = ItemID.IronPickaxe;
                        _fakeItem.scale = 0f;
                        _fakeItem.rare = ItemRarityID.Yellow;
                        _fakeItem.value = -1;
                        Main.HoverItem = _fakeItem;
                        Main.instance.MouseText("");
                        Main.mouseText = true;
                    }

                    DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, unstableInMultiplayerMessage,
                        drawPos,
                        new Color(242, 167, 84), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f, alphaMult: 0.55f);
                    drawPos.Y += 32;
                }

                var incompatibleMultiplayerCount = data.Individual.MPIncompatible.Length;
                if (incompatibleMultiplayerCount > 0)
                {
                    var incompatibleMultiplayerMessage =
                        $"{incompatibleMultiplayerCount} Mod(s) Known Multiplayer Incompatible \u2718";
                    var incompatibleMultiplayerMessageSize =
                        FontAssets.MouseText.Value.MeasureString(incompatibleMultiplayerMessage);
                    if (Main.MouseScreen.Between(drawPos, drawPos + incompatibleMultiplayerMessageSize))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        _fakeItem.SetDefaults(0, true);
                        const string textValue = "[c/FFFFFF:Muliplayer Incompatible Mods]";
                        var tooltipValue = "";
                        foreach (var mod in data.Individual.MPIncompatible)
                        {
                            var localMod = CompatSystem.LocalModByID[mod.ModID];
                            var val = $"• {localMod.DisplayName} v{localMod.Version}";
                            if (!string.IsNullOrEmpty(mod.Note)) val += " — [c/EF4545:" + mod.Note + "]";
                            if (compatConfig.ShowRecommendedFixes && mod.FixMods is { Length: > 0 })
                            {
                                val += "\n  [c/BDB8C4:- Recommended Add-ons: ";
                                foreach (var fixModName in mod.FixMods) val += fixModName + ", ";
                                val = val[..^2];
                                val += "]";
                            }

                            if (mod.IssueIDs is { Length: > 0 })
                            {
                                val += "\n  [c/BDB8C4:- Relevant GitHub Issues: ";
                                foreach (var issueID in mod.IssueIDs) val += "#" + issueID + ", ";
                                val = val[..^2];
                                val += $" ({data.Extra.GithubInfo.First(x => x.ModID == mod.ModID).Repo})]";
                            }

                            tooltipValue += val + "\n";
                        }

                        _fakeItem.SetNameOverride(textValue);
                        _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                        _fakeItem.type = ItemID.IronPickaxe;
                        _fakeItem.scale = 0f;
                        _fakeItem.rare = ItemRarityID.Yellow;
                        _fakeItem.value = -1;
                        Main.HoverItem = _fakeItem;
                        Main.instance.MouseText("");
                        Main.mouseText = true;
                    }

                    DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
                        incompatibleMultiplayerMessage,
                        drawPos,
                        new Color(239, 69, 69), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f, alphaMult: 0.55f);
                    drawPos.Y += 32;
                }
            }

            var potentialIssuesCount = compatConfig.CheckMultiplayerCompat
                ? data.Individual.MPUnstable.Length + data.Individual.MPIncompatible.Length
                : 0;
            var potentialIssuesMessage = potentialIssuesCount > 0
                ? $"{potentialIssuesCount} Potential Issue(s) \u2757"
                : compatConfig.CheckMultiplayerCompat
                    ? "No Issues Detected! \u2713"
                    : "No Checks Enabled — See Config";
            var potentialIssuesMessageSize = FontAssets.MouseText.Value.MeasureString(potentialIssuesMessage);
            if (compatConfig.CheckMultiplayerCompat &&
                Main.MouseScreen.Between(drawPos, drawPos + potentialIssuesMessageSize))
            {
                Main.LocalPlayer.mouseInterface = true;
                _fakeItem.SetDefaults(0, true);
                const string textValue = "[c/FFFFFF:Checking] [c/BDB8C4:Multiplayer Support]";
                _fakeItem.SetNameOverride(textValue);
                _fakeItem.type = ItemID.IronPickaxe;
                _fakeItem.scale = 0f;
                _fakeItem.value = -1;
                Main.HoverItem = _fakeItem;
                Main.instance.MouseText("");
                Main.mouseText = true;
            }

            //var drawPositives = Main.MouseScreen.Between(drawPos, drawPos + potentialIssuesMessageSize);
            DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, potentialIssuesMessage, drawPos,
                Color.LightGray, 0f, Vector2.Zero, 1f,
                SpriteEffects.None, 0f, alphaMult: 0.55f);
            drawPos.Y += 32;

            if (!compatConfig.ListLocalMods) return;

            var localModsCount = ModLoader.Mods.Length - 1 - CompatSystem.WorkshopModNames.Count;
            if (localModsCount > 0)
            {
                var localModsMessage = $"No Data for {localModsCount} Local Mod(s) \u2753";
                var localModsMessageSize = FontAssets.MouseText.Value.MeasureString(localModsMessage);
                var bottomRightDrawPos =
                    new Vector2(Main.screenWidth - 9 - localModsMessageSize.X, Main.screenHeight - 35);
                if (Main.MouseScreen.Between(bottomRightDrawPos, bottomRightDrawPos + localModsMessageSize))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    _fakeItem.SetDefaults(0, true);
                    const string textValue = "[c/FFFFFF:Local Mods (Not From Workshop)]";
                    var tooltipValue = "";
                    var alphaSortedMods = ModLoader.Mods.OrderBy(mod => mod.DisplayName).ToArray();
                    foreach (var mod in alphaSortedMods)
                    {
                        if (mod.Name == "ModLoader") continue;
                        if (CompatSystem.WorkshopModNames.Contains(mod.Name)) continue;
                        tooltipValue += $"• {mod.DisplayName} v{mod.Version} \n";
                    }

                    _fakeItem.SetNameOverride(textValue);
                    _fakeItem.ToolTip = new ItemTooltip(tooltipValue);
                    _fakeItem.type = ItemID.IronPickaxe;
                    _fakeItem.scale = 0f;
                    _fakeItem.value = -1;
                    Main.HoverItem = _fakeItem;
                    Main.instance.MouseText("");
                    Main.mouseText = true;
                }

                DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, localModsMessage,
                    bottomRightDrawPos,
                    Color.LightGray, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f, alphaMult: 0.55f);
            }
        }
    }

    private static void DrawOutlinedStringOnMenu(SpriteBatch spriteBatch, DynamicSpriteFont font, string text,
        Vector2 position, Color drawColor, float rotation, Vector2 origin, float scale, SpriteEffects effects,
        float layerDepth, bool special = false, float alphaMult = 0.3f)
    {
        for (var i = 0; i < 5; i++)
        {
            var color = Color.Black;
            if (i == 4)
            {
                color = drawColor;
                if (special)
                {
                    color.R = (byte)((255 + color.R) / 2);
                    color.G = (byte)((255 + color.R) / 2);
                    color.B = (byte)((255 + color.R) / 2);
                }
            }

            color.A = (byte)(color.A * alphaMult);

            var offX = 0;
            var offY = 0;
            switch (i)
            {
                case 0:
                    offX = -2;
                    break;
                case 1:
                    offX = 2;
                    break;
                case 2:
                    offY = -2;
                    break;
                case 3:
                    offY = 2;
                    break;
            }

            spriteBatch.DrawString(font, text, position + new Vector2(offX, offY), color, rotation, origin, scale,
                effects, layerDepth);
        }
    }
}