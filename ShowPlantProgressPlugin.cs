using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ShowPlantProgress;

[BepInPlugin(_modUid, _modDescription, _modVersion)]
[HarmonyPatch]
public class ShowPlantProgressPlugin : BaseUnityPlugin
{
	internal const string _modVersion = "1.5.1";
	internal const string _modDescription = "Show Plant Progress";
	internal const string _modUid = "kompjoefriek.showplantprogress";

	internal static new ManualLogSource Logger;

	private static ConfigEntry<bool> _configEnableMod;
	private static ConfigEntry<int> _configAmountOfDecimals;
	private static ConfigEntry<bool> _configShowTime;

	private static readonly List<string> _bushList = [ "RaspberryBush(Clone)", "BlueberryBush(Clone)", "CloudberryBush(Clone)" ];

	private void Awake()
	{
		Logger = base.Logger;
		_configEnableMod = Config.Bind("1 - Global", "Enable Mod", true, "Enable or disable this mod");
		_configAmountOfDecimals = Config.Bind("2 - General", "Amount of Decimal Places", 2, "The amount of decimal places to show");
		_configShowTime = Config.Bind("2 - General", "Show Time", false, "Show the time when done");

		if (!_configEnableMod.Value) { return; }

		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
	}

	private static string GetColorStringFromPercentage(double percentage)
	{
		if (percentage >= 75.0) return "green";
		if (percentage >= 50.0) return "yellow";
		if (percentage >= 25.0) return "orange";
		return "red";
	}

	private static string GetValueAsColoredString(string color, double value)
	{
		return $"<color={color}>{value}%</color>";
	}

	private static string FormatMinutesAsString(double minutes)
	{
		int hours = (int)(minutes / 60.0);
		int mins = (int)(minutes % 60.0);
		int secs = (int)((minutes - Math.Floor(minutes)) * 60.0);
		if (hours > 1) return $"{hours:D2}:{mins:D2}:{secs:D2}";
		return $"{mins:D2}:{secs:D2}";
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
	public static string PlantGetHoverText_Patch(string __result, Plant __instance)
	{
		if (__instance == null) return __result;

		double timeSincePlanted = Traverse.Create(__instance).Method("TimeSincePlanted").GetValue<double>();
		float growTime = Traverse.Create(__instance).Method("GetGrowTime").GetValue<float>();

		double percentage = timeSincePlanted / (double)growTime * 100.0;
		string color = GetColorStringFromPercentage(percentage);
		string growPercentage = GetValueAsColoredString(color, Math.Round(percentage, _configAmountOfDecimals.Value, MidpointRounding.AwayFromZero));

		if (_configShowTime.Value)
		{
			double timeRemaining = (growTime - timeSincePlanted) / 60.0;
			growPercentage += $", {FormatMinutesAsString(timeRemaining)}";
			Logger.LogMessage("timeRemaining: "+ timeRemaining+", formatted: "+ FormatMinutesAsString(timeRemaining));
		}

		__result = __result.Replace(" )", $", {growPercentage} )");
		// Put extra info on new line if time is included
		if (_configShowTime.Value)
		{
			return __result.Replace(" (", "\n(");
		}
		return __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
	public static string BerryBushPickable_Patch(string __result, Pickable __instance, ZNetView ___m_nview)
	{
		if (__instance == null) return __result;
		if (!_bushList.Contains(__instance.name)) return __result;

		DateTime startTime = new(___m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L));
		double percentage = (ZNet.instance.GetTime() - startTime).TotalMinutes / (double)__instance.m_respawnTimeMinutes * 100.0;
		if (percentage > 99.99f) return __result;
		string color = GetColorStringFromPercentage(percentage);
		string growPercentage = GetValueAsColoredString(color, Math.Round(percentage, _configAmountOfDecimals.Value, MidpointRounding.AwayFromZero));

		if (_configShowTime.Value)
		{
			double timeRemaining = __instance.m_respawnTimeMinutes - (ZNet.instance.GetTime() - startTime).TotalMinutes;
			growPercentage += $", {FormatMinutesAsString(timeRemaining)}";
		}

		string instanceName = Localization.instance.Localize(__instance.GetHoverName());
		return __result + $"{instanceName} ( {growPercentage} )";
	}
}
