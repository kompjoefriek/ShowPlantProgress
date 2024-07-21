using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ShowPlantProgress;

[BepInPlugin(_modUid, _modDescription, _modVersion)]
[HarmonyPatch]
public class ShowPlantProgressPlugin : BaseUnityPlugin
{
	internal const string _modVersion = "1.5.0";
	internal const string _modDescription = "Show Plant Progress";
	internal const string _modUid = "kompjoefriek.showplantprogress";

	internal static new ManualLogSource Logger;

	private static ConfigEntry<bool> _configEnableMod;
	private static ConfigEntry<int> _configAmountOfDecimals;

	private static readonly List<string> _bushList = [ "RaspberryBush(Clone)", "BlueberryBush(Clone)", "CloudberryBush(Clone)" ];

	private void Awake()
	{
		Logger = base.Logger;
		_configEnableMod = Config.Bind("1 - Global", "Enable Mod", true, "Enable or disable this mod");
		_configAmountOfDecimals = Config.Bind("2 - General", "Amount of Decimal Places", 2, "The amount of decimal places to show");

		if (!_configEnableMod.Value) { return; }

		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
	}

	private static string GetColor(double percentage)
	{
		if (percentage >= 75) return "green";
		if (percentage >= 50) return "yellow";
		if (percentage >= 25) return "orange";
		return "red";
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
	public static string PlantGetHoverText_Patch(string __result, Plant __instance)
	{
		if (__instance == null) return __result;

		double percentage = Math.Floor(Traverse.Create(__instance).Method("TimeSincePlanted").GetValue<double>() / Traverse.Create(__instance).Method("GetGrowTime").GetValue<float>() * 100);
		string color = GetColor(percentage);
		string growPercentage = $"<color={color}>{Math.Round(percentage, _configAmountOfDecimals.Value, MidpointRounding.AwayFromZero)}%</color>";

		return __result.Replace(" )", $", {growPercentage} )");
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
	public static string BerryBushPickable_Patch(string __result, Pickable __instance, ZNetView ___m_nview)
	{
		if (!_bushList.Contains(__instance.name)) return __result;

		DateTime startTime = new DateTime(___m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L));
		double percentage = (ZNet.instance.GetTime() - startTime).TotalMinutes / (double)__instance.m_respawnTimeMinutes * 100;
		if (percentage > 99.99f) return __result;

		string color = GetColor(percentage);
		string growPercentage = $"<color={color}>{Math.Round(percentage, _configAmountOfDecimals.Value, MidpointRounding.AwayFromZero)}%</color>";

		return __result + $"{Localization.instance.Localize(__instance.GetHoverName())} ( {growPercentage} )";
	}
}
