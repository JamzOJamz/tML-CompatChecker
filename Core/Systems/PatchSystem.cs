using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CompatChecker.Content.Configs;
using CompatChecker.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Steamworks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace CompatChecker.Core.Systems;

/// <summary>
///     A system that applies detours and IL edits to both base game and modded code, enabling new features and resolving
///     compatibility issues.
/// </summary>
public class PatchSystem : ModSystem
{
    public override void Load()
    {
        MonoModHooks.Modify(typeof(UIModItem).GetMethod("OnInitialize"),
            UIModItem_OnInitialize_ILEdit); // Patch to add additional functionality to UIModItem
        if (ModLoader.TryGetMod("ConciseModList",
                out var conciseModList)) // Patch to make Concise Mod List's code thread-safe
        {
            var conciseModListAssembly = conciseModList.Code;
            var conciseModListType =
                conciseModListAssembly.GetType("ConciseModList.ConciseModList");
            var conciseUiModItemType =
                conciseModListAssembly.GetType("ConciseModList.ConciseUIModItem");
            if (conciseModListType != null && conciseUiModItemType != null)
            {
                var conciseModListDelegateMethod =
                    conciseModListType.GetMethod("<Load>b__1_1", BindingFlags.NonPublic | BindingFlags.Instance);
                if (conciseModListDelegateMethod != null)
                    // ReSharper disable InconsistentNaming
                    MonoModHooks.Add(conciseModListDelegateMethod,
                        (Action<object, object, UIMods> _, object _, object _, UIMods self) =>
                            // ReSharper restore InconsistentNaming
                        {
                            self.modItemsTask = Task.Run(() =>
                            {
                                var mods = ModOrganizer.FindMods(true);
                                return mods.Select(mod =>
                                    Activator.CreateInstance(conciseUiModItemType, mod) as UIModItem).ToList();
                            }, self._cts.Token);
                        });
            }
        }
        else
        {
            MonoModHooks.Add(typeof(UIModItem).GetMethod("DrawSelf", BindingFlags.Instance | BindingFlags.NonPublic),
                UIModItem_DrawSelf_Detour); // Patch to show Ko-fi link in Compatibility Checker's UIModItem
        }
    }

    private static void UIModItem_OnInitialize_ILEdit(ILContext il)
    {
        try
        {
            // Start the Cursor at the start
            var c = new ILCursor(il);
            if (!c.TryGotoNext(i => i.MatchLdcR4(-22f), i => i.MatchStfld<StyleDimension>("Pixels"), i => i.MatchDup(),
                    i => i.MatchLdflda<UIElement>("Left"), i => i.MatchLdcR4(1f),
                    i => i.MatchCall<StyleDimension>("set_Percent"))) return;

            // Skip the next 6 instructions
            c.Index += 6;

            // Push the instance of UIModItem to the stack
            c.Emit(OpCodes.Ldarg_0);

            // Emit delegate to work with modLocationIcon
            c.EmitDelegate<Func<UIHoverImage, UIModItem, UIHoverImage>>((modLocationIcon, modItem) =>
            {
                var mod = modItem._mod;

                modLocationIcon.OnLeftClick += (_, _) =>
                {
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    switch (mod.location)
                    {
                        case ModLocation.Workshop:
                        {
                            if (!ModOrganizer.TryReadManifest(ModOrganizer.GetParentDir(mod.modFile.path),
                                    out var info)) return;
                            var workshopPage =
                                $"https://steamcommunity.com/sharedfiles/filedetails/?id={info.workshopEntryId}";
                            try
                            {
                                SteamFriends.ActivateGameOverlayToWebPage(workshopPage);
                            }
                            catch
                            {
                                Utils.OpenToURL(workshopPage);
                            }

                            break;
                        }
                        case ModLocation.Local:
                            Directory.CreateDirectory(ModLoader.ModPath);
                            FSUtils.OpenFolderAndSelectItem(ModLoader.ModPath, Path.GetFileName(mod.modFile.path));
                            break;
                        case ModLocation.Modpack:
                            break;
                        default:
#pragma warning disable CA2208
                            throw new ArgumentOutOfRangeException();
#pragma warning restore CA2208
                    }
                };

                modLocationIcon.OnRightClick += (_, _) =>
                {
                    if (mod.location != ModLocation.Local) return;
                    var modSourcePath = Path.Combine(ModCompile.ModSourcePath, mod.Name);
                    if (!Directory.Exists(modSourcePath)) return;
                    SoundEngine.PlaySound(SoundID.MenuOpen);
                    Utils.OpenFolder(modSourcePath);
                };

                var textToAdd = "";
                switch (modItem._mod.location)
                {
                    case ModLocation.Local:
                    {
                        modLocationIcon.SetImage(UICommon.ButtonOpenFolder);
                        textToAdd += Language.GetTextValue("Mods.CompatChecker.UI.LeftClickShowTmodFile");
                        var modSourcePath = Path.Combine(ModCompile.ModSourcePath, mod.Name);
                        if (Directory.Exists(modSourcePath))
                            textToAdd += Language.GetTextValue("Mods.CompatChecker.UI.RightClickOpenModSourceFolder");
                        modLocationIcon.HoverText =
                            Language.GetTextValue("tModLoader.ModFrom" + mod.location) + textToAdd;
                        break;
                    }
                    case ModLocation.Workshop:
                        textToAdd += Language.GetTextValue("Mods.CompatChecker.UI.LeftClickOpenWorkshopPage");
                        break;
                    case ModLocation.Modpack:
                        break;
                    default:
#pragma warning disable CA2208
                        throw new ArgumentOutOfRangeException();
#pragma warning restore CA2208
                }

                if (!string.IsNullOrEmpty(textToAdd))
                    modLocationIcon.HoverText = Language.GetTextValue("tModLoader.ModFrom" + mod.location) + textToAdd;

                return modLocationIcon;
            });
        }
        catch
        {
            MonoModHooks.DumpIL(ModContent.GetInstance<CompatChecker>(), il);
        }
    }

    private static void UIModItem_DrawSelf_Detour(Action<UIModItem, SpriteBatch> orig, UIModItem self,
        SpriteBatch spriteBatch)
    {
        orig(self, spriteBatch);
        
        if (!ModContent.GetInstance<CompatConfig>().ShowModDonationLink)
            return;

        var mod = self._mod;
        if (mod.Name != nameof(CompatChecker)) return;

        var drawingExtraText = mod.properties.side != ModSide.Server &&
                               (mod.Enabled != self._loaded || self._configChangesRequireReload);
        if (drawingExtraText)
            return; // Don't draw if tModLoader is already drawing extra text here, e.g. "Reload Required"

        var innerDimensions = self.GetInnerDimensions();
        var drawPos =
            new Vector2(
                innerDimensions.X + 10f + self._modIconAdjust + self._uiModStateText.Width.Pixels + self.left2ndLine,
                innerDimensions.Y + 45f);
        var brightDiscoColor =
            new Color(Main.DiscoR + 80, Main.DiscoG + 80, Main.DiscoB + 80, 255); // Brighten the disco color
        var donationText = CompatChecker.KoFiURL + " \u2764";
        var donationTextSize = FontAssets.MouseText.Value.MeasureString(donationText);
        if (Main.MouseScreen.Between(drawPos, drawPos + donationTextSize))
        {
            Main.LocalPlayer.mouseInterface = true;
            UICommon.TooltipMouseText(Language.GetTextValue("Mods.CompatChecker.UI.DonationTooltip"));
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                SoundEngine.PlaySound(SoundID.MenuOpen);
                Utils.OpenToURL(CompatChecker.KoFiURL);
            }
        }
        Utils.DrawBorderString(spriteBatch, donationText, drawPos, brightDiscoColor);
    }
}