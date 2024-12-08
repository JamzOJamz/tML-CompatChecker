using System.Diagnostics;
using System.IO;
using ReLogic.OS;
using Terraria;

namespace CompatChecker.Helpers;

public static class FSUtils
{
    public static void OpenFolderAndSelectItem(string folderPath, string fileName) // TODO: Support selecting file in Linux?
    {
        if (!Utils.TryCreatingDirectory(folderPath))
            return;
        if (Platform.IsLinux)
            Utils.OpenFolderXdg(folderPath);
        else
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                Arguments = "/select, \"" + Path.Combine(folderPath, fileName) + "\""
            });
    }
}