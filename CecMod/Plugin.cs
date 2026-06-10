using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CecMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CecPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.cecmod.seapower";
        public const string PluginName = "Sea Power CEC";
        public const string PluginVersion = "0.1.0";

        /// <summary>Shared logger for the patches.</summary>
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded — Cooperative Engagement Capability active.");
        }
    }
}
