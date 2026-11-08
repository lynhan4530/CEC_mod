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
        public const string PluginVersion = "0.3.0";

        /// <summary>Shared logger for the patches.</summary>
        internal static ManualLogSource Log;

        /// <summary>True once Harmony patches and bridges have been applied.</summary>
        internal static bool IsLoaded { get; private set; }

        private void Awake()
        {
            InitializeCore(Logger);
        }

        internal static void InitializeCore(ManualLogSource logger)
        {
            if (IsLoaded)
            {
                logger?.LogWarning($"{PluginName} already initialized — skipping duplicate load.");
                return;
            }

            IsLoaded = true;
            Log = logger ?? BepInEx.Logging.Logger.CreateLogSource(PluginName);

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(CecPlugin).Assembly);
            EgBridge.Initialize(harmony);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded — Cooperative Engagement Capability active.");
        }
    }
}
