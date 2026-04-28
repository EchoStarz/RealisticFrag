using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;

namespace RealisticFrag.Client;

/// <summary>
/// BepInEx plugin host for RealisticFrag's client-side Harmony patches (v2+).
///
/// Loads the same <c>config.json</c> the server-side mod uses (from the SPT user/mods
/// folder) and exposes its parsed contents via <see cref="Config"/> for patches to consume.
/// Patches are applied to EFT's bullet-impact handler to gate fragmentation on velocity.
/// </summary>
[BepInPlugin(ModGuid, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid    = "com.echostarz.realisticfrag.client";
    public const string ModName    = "RealisticFrag.Client";
    public const string ModVersion = "1.0.0";

    /// <summary>Static handle so patches can read config without DI.</summary>
    public static ModConfig? LoadedConfig { get; private set; }

    /// <summary>Static logger for use from patch classes.</summary>
    public static ManualLogSource? Log { get; private set; }

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"{ModName} v{ModVersion} loading");

        try
        {
            LoadedConfig = LoadConfig();
            if (LoadedConfig is null)
            {
                Log.LogWarning($"{ModName} could not locate config.json — patches will run with empty config (no velocity gating).");
                LoadedConfig = new ModConfig();
            }
            else
            {
                Log.LogInfo($"{ModName} loaded {LoadedConfig.AmmoOverrides.Count} ammo overrides");
            }

            // Apply Harmony patches in this assembly (Patches/ subfolder)
            new Harmony(ModGuid).PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo($"{ModName} Harmony patches applied");
        }
        catch (Exception ex)
        {
            Log.LogError($"{ModName} failed to initialize: {ex}");
        }
    }

    /// <summary>
    /// Locate and parse the same <c>config.json</c> the server mod uses. Tries the canonical
    /// SPT mods path first, then falls back to a sibling <c>config.json</c> next to this DLL.
    /// </summary>
    private static ModConfig? LoadConfig()
    {
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        // SPT install root: BepInEx\plugins\<this>\..\..\..  → <SPT root>
        var sptRoot   = Path.GetFullPath(Path.Combine(pluginDir, "..", "..", ".."));
        var serverModConfig = Path.Combine(sptRoot, "SPT", "user", "mods", "RealisticFrag", "config.json");
        var sidecarConfig   = Path.Combine(pluginDir, "config.json");

        var path = File.Exists(serverModConfig) ? serverModConfig
                 : File.Exists(sidecarConfig)   ? sidecarConfig
                 : null;
        if (path is null) return null;

        Log?.LogInfo($"{ModName} reading config from {path}");
        var raw = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<ModConfig>(raw);
    }
}
