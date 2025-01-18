using System.Linq;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CompatChecker.Content.Commands;

[Autoload(false)]
public class DetoursCommand : ModCommand
{
    public override string Command => "detours";

    public override CommandType Type => CommandType.Chat;
    
    public override string Description => Language.GetTextValue("Mods.CompatChecker.Commands.Detours.Description");

    public override string Usage => Language.GetTextValue("Mods.CompatChecker.Commands.Detours.Usage");

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        // Reply with usage information, if no arguments are provided
        if (args.Length == 0)
        {
            
            caller.Reply("Usage: " + Usage, new Color(255, 25, 25));
            return;
        }

        // Handle the provided argument
        switch (args[0])
        {
            case "list":
                ListDetours(caller);
                break;
            case "conflicting":
                ListConflictingDetours(caller);
                break;
            case "count":
                CountDetours(caller);
                break;
            default:
                caller.Reply("Invalid argument provided. Please use /detours for usage information.",
                    new Color(255, 25, 25));
                break;
        }
    }

    private static void ListDetours(CommandCaller caller)
    {
        var detoursList = CompatChecker.Detours;

        if (detoursList.Count == 0)
        {
            caller.Reply("No MonoMod detours found!");
            return;
        }

        var msg = string.Empty;
        msg += $"[c/99E550:{detoursList.Count} detour{(detoursList.Count > 1 ? "s" : string.Empty)}:]";
        foreach (var detour in detoursList)
            msg +=
                $"\n[c/FFFFFF:  • {MonoModHooks.StringRep(detour.Method.Method)}] ({detour.Entry.DeclaringType!.FullName}) {(!detour.IsApplied ? "[c/EF4545:(Inactive)]" : string.Empty)}";
        caller.Reply(msg, new Color(209, 203, 216));
    }

    private static void ListConflictingDetours(CommandCaller caller)
    {
        var detoursList = CompatChecker.Detours;

        // Check for any conflicting detours with LINQ (targetting smae method) and group them into their own groups
        var conflictingDetours = detoursList.GroupBy(detour => MonoModHooks.StringRep(detour.Method.Method))
            .Where(group =>
                group.Count() > 1 && group.Select(detour => detour.Entry.DeclaringType!.Assembly.GetName().Name)
                    .Distinct().Count() > 1);
        var enumerables = conflictingDetours as IGrouping<string, DetourInfo>[] ?? conflictingDetours.ToArray();
        var totalConflictingDetours = enumerables.Length;

        if (totalConflictingDetours > 0)
        {
            var msg = string.Empty;
            msg +=
                $"[c/F2A754:{totalConflictingDetours} methods with potentially conflicting detour{(totalConflictingDetours > 1 ? "s" : string.Empty)}:]";
            foreach (var conflictingDetour in enumerables)
            {
                msg +=
                    $"\n[c/FFFFFF:  • {conflictingDetour.Key}] ({conflictingDetour.Count()} detour{(conflictingDetour.Count() > 1 ? "s" : string.Empty)})";
                foreach (var detour in conflictingDetour)
                    msg +=
                        $"\n    - {detour.Entry.DeclaringType!.Assembly.GetName().Name} ({detour.Entry.DeclaringType.FullName}) {(!detour.IsApplied ? "[c/EF4545:(Inactive)]" : string.Empty)}";
            }

            caller.Reply(msg, new Color(209, 203, 216));
        }
        else
        {
            caller.Reply("No conflicting detours found!");
        }
    }

    private static void CountDetours(CommandCaller caller)
    {
        var detoursList = CompatChecker.Detours;
        var totalDetours = detoursList.Count;
        var totalActiveDetours = detoursList.Count(detour => detour.IsApplied);
        var totalModsWithDetours = detoursList.Select(detour => detour.Entry.DeclaringType!.Assembly.GetName().Name)
            .Distinct().Count();

        if (totalDetours == 0)
        {
            caller.Reply("No MonoMod detours found!");
            return;
        }

        var isPlural = totalDetours > 1;
        caller.Reply(
            $"[c/FFFFFF:{totalActiveDetours}/{totalDetours} MonoMod detours are applied] ({(isPlural ? "across" : "by")} {totalModsWithDetours} mod{(totalModsWithDetours > 1 ? "s" : string.Empty)})",
            new Color(209, 203, 216));
    }
}