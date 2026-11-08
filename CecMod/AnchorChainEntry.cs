using System;
using System.IO;
using System.Reflection;
using AnchorChain;
using BepInEx.Bootstrap;
using SeaPower;
using UnityEngine;

namespace CecMod
{
    /// <summary>
    /// Entry point invoked by Anchor Chain (<c>io.github.seapower_modders.anchorchain</c>,
    /// Workshop ID 3380210757) when the mod is installed via Steam Workshop or
    /// <c>StreamingAssets</c>. Kept in a separate class from <see cref="CecPlugin"/> so
    /// standalone <c>BepInEx/plugins</c> installations continue to work even without Anchor Chain.
    /// </summary>
    [ACPlugin(CecPlugin.PluginGuid, CecPlugin.PluginName, CecPlugin.PluginVersion)]
    public sealed class AnchorChainEntry : IAnchorChainMod
    {
        public void TriggerEntryPoint()
        {
            if (!IsEnabledInModMenu())
            {
                Debug.Log($"[{CecPlugin.PluginName}] Disabled in the Sea Power Mod Manager — skipping load.");
                return;
            }

            Assembly self = typeof(CecPlugin).Assembly;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                new AssemblyName(args.Name).Name == self.GetName().Name ? self : null;

            GameObject managerObject = Chainloader.ManagerObject;
            if (managerObject == null)
            {
                CecPlugin.InitializeCore(BepInEx.Logging.Logger.CreateLogSource(CecPlugin.PluginName));
                return;
            }

            if (CecPlugin.IsLoaded
                || Chainloader.PluginInfos.ContainsKey(CecPlugin.PluginGuid)
                || managerObject.GetComponent<CecPlugin>() != null)
            {
                Debug.LogWarning(
                    $"[{CecPlugin.PluginName}] Already loaded (remove duplicate copy in BepInEx/plugins if using Workshop); skipping duplicate load.");
                return;
            }

            managerObject.AddComponent<CecPlugin>();
        }

        private static bool IsEnabledInModMenu()
        {
            try
            {
                FileManager fm = Singleton<FileManager>.Instance;
                var dirs = fm != null ? fm.Directories : null;
                if (dirs == null)
                    return true;

                string asmLocation = typeof(CecPlugin).Assembly.Location;
                string asmDir = string.IsNullOrEmpty(asmLocation)
                    ? string.Empty
                    : Path.GetFullPath(Path.GetDirectoryName(asmLocation) ?? string.Empty)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                bool matchedAnyModDir = false;
                foreach (SearchDirectory item in dirs)
                {
                    DirectoryInfo di = item != null ? item.DirectoryInfo : null;
                    if (di == null)
                        continue;

                    string modRoot = Path.GetFullPath(di.FullName)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    bool isThisDir = asmDir.Length > 0 &&
                        (asmDir.Equals(modRoot, StringComparison.OrdinalIgnoreCase)
                         || asmDir.StartsWith(modRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                    if (isThisDir)
                    {
                        matchedAnyModDir = true;
                        if (item.IsChecked)
                            return true;
                    }
                }

                if (!matchedAnyModDir)
                    return true;

                // If both an unchecked local StreamingAssets staging folder and a checked
                // Steam Workshop subscription exist, Anchor Chain deduplicates by GUID and
                // invokes whichever copy was scanned first — check whether any enabled mod
                // directory contains our DLL before skipping.
                string dllFileName = Path.GetFileName(asmLocation);
                if (!string.IsNullOrEmpty(dllFileName))
                {
                    foreach (SearchDirectory item in dirs)
                    {
                        if (item != null && item.IsChecked && item.DirectoryInfo != null && item.DirectoryInfo.Exists)
                        {
                            if (Directory.GetFiles(item.DirectoryInfo.FullName, dllFileName, SearchOption.AllDirectories).Length > 0)
                                return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{CecPlugin.PluginName}] Could not inspect Mod Manager state ({ex.Message}); loading anyway.");
                return true;
            }
        }
    }
}
