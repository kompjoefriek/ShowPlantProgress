using BepInEx;
using BepInEx.Logging;

namespace ShowPlantProgress;

[BepInPlugin(_modUid, _modDescription, _modVersion)]
public class ShowPlantProgressPlugin : BaseUnityPlugin
{
    internal const string _modVersion = "1.5.0";
    internal const string _modDescription = "Show Plant Progress";
    internal const string _modUid = "kompjoefriek.showplantprogress";

    internal static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {_modUid} is loaded!");
    }
}
