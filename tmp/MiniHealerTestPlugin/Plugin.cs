using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace MiniHealerTestPlugin;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string ModGuid = "com.codex.minihealer.testplugin";
    private const string ModName = "Mini Healer Test Plugin";
    private const string ModVersion = "1.0.0";

    private void Awake()
    {
        Logger.LogInfo($"{ModName} loaded");
        Logger.LogInfo($"GameObject active in scene: {gameObject.name}");

        var marker = new GameObject("MiniHealerTestPluginMarker");
        Object.DontDestroyOnLoad(marker);
        Logger.LogInfo("Created persistent marker object");
    }
}
