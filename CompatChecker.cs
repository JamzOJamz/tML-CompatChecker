using System;
using System.Linq;
using CompatChecker.Content.Configs;
using CompatChecker.Core.Systems;
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

namespace CompatChecker;

public class CompatChecker : Mod
{
    /*public CompatChecker() // Runs before any mod is loaded
    {
    }*/

    private const string DiscordURL = "https://discord.gg/F9bThEE9FV"; // Jamz's Mods, #cc-chat
    public const string KoFiURL = "https://ko-fi.com/jamzojamz"; // Ko-fi link for donations

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
        var compatConfig = ModContent.GetInstance<CompatConfig>();
        if (Main.menuMode != 0 || !compatConfig.DrawEnabledModListOnMainMenu)
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
            var textValue = Language.GetTextValue("Mods.CompatChecker.UI.EnabledMods");
            var tooltipValue = "";
            var alphaSortedMods = ModLoader.Mods.OrderBy(mod => mod.DisplayNameClean).ToArray();
            var maxModsToDisplay = Main.screenHeight < 979 ? 20 : 25;
            var maxModsPerLineWhenShiftHeld = Main.screenWidth < 1366 ? 3 : 4;
            var leftShiftHeld = Main.keyState.PressingShift();
            var actualIndexWithoutModLoader = 0;
            for (var i = 0; i < alphaSortedMods.Length; i++)
            {
                if (!leftShiftHeld && i > maxModsToDisplay)
                {
                    tooltipValue += Language.GetTextValue("Mods.CompatChecker.UI.MoreMods", alphaSortedMods.Length - i);
                    break;
                }

                var mod = alphaSortedMods[i];
                if (mod.Name == "ModLoader") continue;

                if (!leftShiftHeld)
                {
                    var val = $"- {mod.DisplayName}";
                    if (compatConfig.EnabledModListShowVersions)
                        val += $" v{mod.Version}";
                    if (!CompatSystem.WorkshopModNames.Contains(mod.Name))
                        val += Language.GetTextValue("Mods.CompatChecker.UI.LocalModDescriptor");
                    tooltipValue += val + "\n";
                }
                else
                {
                    // Compact display for when shift is held, with 4 mods per line
                    if (actualIndexWithoutModLoader > 0 &&
                        actualIndexWithoutModLoader % maxModsPerLineWhenShiftHeld == 0)
                        tooltipValue = tooltipValue[..^1] + "\n";
                    tooltipValue +=
                        $"{mod.DisplayName}{(compatConfig.EnabledModListShowVersions ? $" v{mod.Version}" : string.Empty)}, ";
                }

                actualIndexWithoutModLoader++;
            }

            if (leftShiftHeld)
                tooltipValue = tooltipValue[..^2];

            _fakeItem.SetNameOverride(textValue + "\n" + tooltipValue);
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
        var anyModsNeedReload = Interface.modsMenu.items.Any(i =>
            i.NeedsReload);
        var backToMainMenuText = Language.GetTextValue("Mods.CompatChecker.UI.BackToMainMenuButton");
        var backToMainMenuTextSize = FontAssets.MouseText.Value.MeasureString(backToMainMenuText) + new Vector2(5, 0);
        var topRightDrawPos = new Vector2(Main.screenWidth - 18 - backToMainMenuTextSize.X, 12);
        var backToMainMenuHovered = Main.MouseScreen.Between(topRightDrawPos, topRightDrawPos + backToMainMenuTextSize);
        if (backToMainMenuHovered)
        {
            Main.LocalPlayer.mouseInterface = true;
            if (!_lastHoveringBackToMainMenuText)
                SoundEngine.PlaySound(SoundID.MenuTick);
            _fakeItem.SetDefaults(0, true);
            _fakeItem.SetNameOverride(Language.GetTextValue("Mods.CompatChecker.UI.BackToMainMenuTooltip"));
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

        var helpContributeButton = Language.GetTextValue("Mods.CompatChecker.UI.HelpContributeButton");
        var helpContributeButtonSize = FontAssets.MouseText.Value.MeasureString(helpContributeButton);
        var helpHovered = Main.MouseScreen.Between(bottomDrawPos, bottomDrawPos + helpContributeButtonSize);
        if (helpHovered)
        {
            Main.LocalPlayer.mouseInterface = true;
            if (!_lastHoveringHelpContributeButton)
                SoundEngine.PlaySound(SoundID.MenuTick);
            _fakeItem.SetDefaults(0, true);
            _fakeItem.SetNameOverride(Language.GetTextValue("Mods.CompatChecker.UI.HelpContributeTooltip", DiscordURL));
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
        if (Language.ActiveCulture.LegacyId == 6) // Russian translation active, take up more space
            bottomDrawPos.Y -= 28;

        var noteText = smallWidthRes
            ? Language.GetTextValue("Mods.CompatChecker.UI.CompatibilityNoteSmallWidth")
            : Language.GetTextValue("Mods.CompatChecker.UI.CompatibilityNote");
        DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value,
            noteText, bottomDrawPos, Color.White, 0f,
            Vector2.Zero, 1f,
            SpriteEffects.None, 0f, true, 0.5f);

        var loadingModsUiCheck = Interface.modsMenu.loading;
        if (ModLoader.HasMod("ConciseModList"))
            loadingModsUiCheck = Interface.modsMenu.uiLoader.Parent != null;
        if (CompatSystem.CompatibilityData == null || CompatSystem.RequestError != null ||
            loadingModsUiCheck || anyModsNeedReload) // Loading state
        {
            if (CompatSystem.RequestError != null)
            {
                DrawOutlinedStringOnMenu(Main.spriteBatch, FontAssets.MouseText.Value, CompatSystem.RequestError.Value,
                    drawPos,
                    menucolor * 0.8f, 0f, Vector2.Zero, 1.02f, SpriteEffects.None, 0f, true);
            }
            else
            {
                var dots = new string('.', (int)(Main.timeForVisualEffects / 20 % 4));
                var doingThing = CompatSystem.CompatibilityData != null && anyModsNeedReload
                    ? Language.GetTextValue("tModLoader.ModReloadRequired")
                    : Language.GetTextValue("Mods.CompatChecker.UI.Loading");
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
                var verifiedMultiplayerSupportMods = data.Individual?.MPCompatible ?? [];
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
                    var verifiedMultiplayerSupportMessage = Language.GetTextValue(
                        "Mods.CompatChecker.UI.VerifiedMPCompatibleCount",
                        verifiedMultiplayerSupportCount);
                    var verifiedMultiplayerSupportMessageSize =
                        FontAssets.MouseText.Value.MeasureString(verifiedMultiplayerSupportMessage);
                    if (Main.MouseScreen.Between(drawPos, drawPos + verifiedMultiplayerSupportMessageSize))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        _fakeItem.SetDefaults(0, true);
                        var textValue = Language.GetTextValue("Mods.CompatChecker.UI.MultiplayerCompatibleMods");
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

                        _fakeItem.SetNameOverride(textValue + "\n" + tooltipValue);
                        _fakeItem.type = ItemID.IronPickaxe;
                        _fakeItem.scale = 0f;
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

                var unstableInMultiplayerCount = data.Individual?.MPUnstable?.Length ?? 0;
                if (unstableInMultiplayerCount > 0)
                {
                    var unstableInMultiplayerMessage = Language.GetTextValue("Mods.CompatChecker.UI.MPUnstableCount",
                        unstableInMultiplayerCount);
                    var unstableInMultiplayerMessageSize =
                        FontAssets.MouseText.Value.MeasureString(unstableInMultiplayerMessage);
                    if (Main.MouseScreen.Between(drawPos, drawPos + unstableInMultiplayerMessageSize))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        _fakeItem.SetDefaults(0, true);
                        var textValue = Language.GetTextValue("Mods.CompatChecker.UI.ModsUnstableInMultiplayer");
                        var tooltipValue = "";
                        foreach (var mod in data.Individual.MPUnstable)
                        {
                            var localMod = CompatSystem.LocalModByID[mod.ModID];
                            var val = $"• {localMod.DisplayName} v{localMod.Version}";
                            if (!string.IsNullOrEmpty(mod.Note)) val += " — [c/F2A754:" + mod.Note + "]";
                            if (compatConfig.ShowRecommendedFixes && mod.FixMods is { Length: > 0 })
                            {
                                val += Language.GetTextValue("Mods.CompatChecker.UI.RecommendedAddons");
                                foreach (var fixModName in mod.FixMods) val += fixModName + ", ";
                                val = val[..^2];
                                val += "]";
                            }

                            if (compatConfig.DisplayGitHubIssues && mod.IssueIDs is { Length: > 0 })
                            {
                                val += Language.GetTextValue("Mods.CompatChecker.UI.RelevantGitHubIssues");
                                foreach (var issueID in mod.IssueIDs) val += "#" + issueID + ", ";
                                val = val[..^2];
                                val += $" ({data.Extra.GithubInfo.First(x => x.ModID == mod.ModID).Repo})]";
                            }

                            tooltipValue += val + "\n";
                        }

                        _fakeItem.SetNameOverride(textValue + "\n" + tooltipValue);
                        _fakeItem.type = ItemID.IronPickaxe;
                        _fakeItem.scale = 0f;
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

                var incompatibleMultiplayerCount = data.Individual?.MPIncompatible?.Length ?? 0;
                if (incompatibleMultiplayerCount > 0)
                {
                    var incompatibleMultiplayerMessage = Language.GetTextValue(
                        "Mods.CompatChecker.UI.MPIncompatibleCount",
                        incompatibleMultiplayerCount);
                    var incompatibleMultiplayerMessageSize =
                        FontAssets.MouseText.Value.MeasureString(incompatibleMultiplayerMessage);
                    if (Main.MouseScreen.Between(drawPos, drawPos + incompatibleMultiplayerMessageSize))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        _fakeItem.SetDefaults(0, true);
                        var textValue = Language.GetTextValue("Mods.CompatChecker.UI.MultiplayerIncompatibleMods");
                        var tooltipValue = "";
                        foreach (var mod in data.Individual.MPIncompatible)
                        {
                            var localMod = CompatSystem.LocalModByID[mod.ModID];
                            var val = $"• {localMod.DisplayName} v{localMod.Version}";
                            if (!string.IsNullOrEmpty(mod.Note)) val += " — [c/EF4545:" + mod.Note + "]";
                            if (compatConfig.ShowRecommendedFixes && mod.FixMods is { Length: > 0 })
                            {
                                val += Language.GetTextValue("Mods.CompatChecker.UI.RecommendedAddons");
                                foreach (var fixModName in mod.FixMods) val += fixModName + ", ";
                                val = val[..^2];
                                val += "]";
                            }

                            if (mod.IssueIDs is { Length: > 0 })
                            {
                                val += Language.GetTextValue("Mods.CompatChecker.UI.RelevantGitHubIssues");
                                foreach (var issueID in mod.IssueIDs) val += "#" + issueID + ", ";
                                val = val[..^2];
                                val += $" ({data.Extra.GithubInfo.First(x => x.ModID == mod.ModID).Repo})]";
                            }

                            tooltipValue += val + "\n";
                        }

                        _fakeItem.SetNameOverride(textValue + "\n" + tooltipValue);
                        _fakeItem.type = ItemID.IronPickaxe;
                        _fakeItem.scale = 0f;
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
                ? (data.Individual?.MPUnstable?.Length ?? 0) + (data.Individual?.MPIncompatible?.Length ?? 0)
                : 0;
            var potentialIssuesMessage = potentialIssuesCount > 0
                ? Language.GetTextValue("Mods.CompatChecker.UI.PotentialIssuesCount", potentialIssuesCount)
                : compatConfig.CheckMultiplayerCompat
                    ? Language.GetTextValue("Mods.CompatChecker.UI.NoIssues")
                    : Language.GetTextValue("Mods.CompatChecker.UI.NoChecksEnabled");
            var potentialIssuesMessageSize = FontAssets.MouseText.Value.MeasureString(potentialIssuesMessage);
            if (compatConfig.CheckMultiplayerCompat &&
                Main.MouseScreen.Between(drawPos, drawPos + potentialIssuesMessageSize))
            {
                Main.LocalPlayer.mouseInterface = true;
                _fakeItem.SetDefaults(0, true);
                var textValue = Language.GetTextValue("Mods.CompatChecker.UI.CheckingMPSupport");
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
                var localModsMessage =
                    Language.GetTextValue("Mods.CompatChecker.UI.NoDataLocalModsCount", localModsCount);
                var localModsMessageSize = FontAssets.MouseText.Value.MeasureString(localModsMessage);
                var bottomRightDrawPos =
                    new Vector2(Main.screenWidth - 9 - localModsMessageSize.X, Main.screenHeight - 35);
                if (Main.MouseScreen.Between(bottomRightDrawPos, bottomRightDrawPos + localModsMessageSize))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    _fakeItem.SetDefaults(0, true);
                    var textValue = Language.GetTextValue("Mods.CompatChecker.UI.LocalMods");
                    var tooltipValue = "";
                    var alphaSortedMods = ModLoader.Mods.OrderBy(mod => mod.DisplayNameClean).ToArray();
                    foreach (var mod in alphaSortedMods)
                    {
                        if (mod.Name == "ModLoader") continue;
                        if (CompatSystem.WorkshopModNames.Contains(mod.Name)) continue;
                        tooltipValue += $"• {mod.DisplayName} v{mod.Version} \n";
                    }

                    _fakeItem.SetNameOverride(textValue + "\n" + tooltipValue);
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