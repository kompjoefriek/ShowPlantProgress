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
	internal const string _modVersion = "1.5.2";
	internal const string _modDescription = "Show Plant Progress";
	internal const string _modUid = "kompjoefriek.showplantprogress";

	internal static new ManualLogSource Logger;

	private Harmony _harmony;

	private static ConfigEntry<bool> _configEnableMod;
	private static ConfigEntry<bool> _configEnableLogging;
	private static ConfigEntry<bool> _configShowPercentage;
	private static ConfigEntry<bool> _configShowColorPercentage;
	private static ConfigEntry<bool> _configShowTime;
	private static ConfigEntry<int> _configAmountOfDecimals;

	private static readonly List<string> _bushList = ["RaspberryBush(Clone)", "BlueberryBush(Clone)", "CloudberryBush(Clone)"];

	private static object _logObject;
	private static DateTime _lastLogTime;

	private void Awake()
	{
		Logger = base.Logger;

		// Normal configs
		_configEnableMod = Config.Bind("1 - Global", "Enable Mod", true, "Enable or disable this mod");
		_configEnableLogging = Config.Bind("1 - Global", "Enable Mod Logging", false, "Enable or disable logging for this mod");

		_configShowPercentage = Config.Bind("2 - Progress", "Show Percentage", true, "Shows the plant or pickable progress as a percentage when you hover over the plant or pickable");
		_configShowColorPercentage = Config.Bind("2 - Progress", "Show Percentage Color", true, "Makes it so the percentage changes color depending on the progress");
		_configAmountOfDecimals = Config.Bind("2 - Progress", "Show Percentage Decimal Places", 2, "The amount of decimal places to show for the percentage");
		_configShowTime = Config.Bind("2 - Progress", "Show Time", false, "Show the time when done");

		// Deprecated config
		Dictionary<ConfigDefinition, string> orphanedEntries = Traverse.Create(Config).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>();
		if (orphanedEntries != null)
		{
			ConfigDefinition deprecatedConfigAmountOfDecimalsDefinition = new ConfigDefinition("2 - General", "Amount of Decimal Places");
			ConfigDefinition deprecatedConfigShowTimeDefinition = new ConfigDefinition("2 - General", "Show Time");
			bool hasDeprecatedConfigAmountOfDecimals = orphanedEntries.TryGetValue(deprecatedConfigAmountOfDecimalsDefinition, out string deprecatedConfigAmountOfDecimalsValue);
			bool hasDeprecatedConfigShowTime = orphanedEntries.TryGetValue(deprecatedConfigShowTimeDefinition, out string deprecatedShowTimeValue);

			bool didConfigChange = false;
			if (hasDeprecatedConfigAmountOfDecimals)
			{
				_configAmountOfDecimals.SetSerializedValue(deprecatedConfigAmountOfDecimalsValue);
				didConfigChange |= RemoveDeprecatedConfigDefinition(ref orphanedEntries, deprecatedConfigAmountOfDecimalsDefinition);
			}
			if (hasDeprecatedConfigShowTime)
			{
				_configAmountOfDecimals.SetSerializedValue(deprecatedShowTimeValue);
				didConfigChange |= RemoveDeprecatedConfigDefinition(ref orphanedEntries, deprecatedConfigShowTimeDefinition);
			}

			if (_configEnableLogging.Value)
			{
				foreach (KeyValuePair<ConfigDefinition, string> entry in orphanedEntries)
				{
					Logger.LogWarning("Orphaned config: " + entry.Key.ToString());
				}
			}

			if (didConfigChange)
			{
				Config.Save();
			}
		}

		_lastLogTime = DateTime.Now;

		if (!_configEnableMod.Value) { return; }

		_harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
	}

	private void OnDestroy()
	{
		_harmony?.UnpatchSelf();
	}

	private static bool RemoveDeprecatedConfigDefinition(ref Dictionary<ConfigDefinition, string> entries, ConfigDefinition definition)
	{
		if (!entries.Remove(definition))
		{
			Logger.LogWarning("Failed to remove deprecated config: " + definition.ToString());
			return false;
		}
		return true;
	}

	private static string GetColorStringFromPercentage(double percentage)
	{
		if (!_configShowColorPercentage.Value) return "white";
		if (percentage >= 75.0) return "green";
		if (percentage >= 50.0) return "yellow";
		if (percentage >= 25.0) return "orange";
		return "red";
	}

	private static string GetValueAsColoredString(string color, double value)
	{
		return $"<color={color}>{value}%</color>";
	}

	private static string FormatSecondsAsString(double seconds)
	{
		int hours = (int)(seconds / 3600.0);
		double remainingSeconds = seconds - (hours * 3600.0);
		int mins = (int)(remainingSeconds / 60.0);
		remainingSeconds -= mins * 60.0;
		int secs = (int)(remainingSeconds);
		if (hours >= 1) return $"{hours:D2}:{mins:D2}:{secs:D2}";
		return $"{mins:D2}:{secs:D2}";
	}

	// Log message at Info log level, but only once per second for the same object
	private static void LogInfoThrottled(object obj, string message)
	{
		if (_configEnableLogging.Value)
		{
			if (object.Equals(_logObject, obj))
			{
				double secondsSinceLastLog = (DateTime.Now - _lastLogTime).TotalSeconds;
				if (secondsSinceLastLog >= 1)
				{
					_logObject = obj;
					_lastLogTime = DateTime.Now;
					Logger.LogInfo(message);
				}
			}
			else
			{
				_logObject = obj;
				_lastLogTime = DateTime.Now;
				Logger.LogInfo(message);
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
	public static string PlantGetHoverText_Patch(string __result, Plant __instance)
	{
		if (__instance == null) return __result;

		if (_configShowPercentage.Value)
		{
			double timeSincePlanted = Traverse.Create(__instance).Method("TimeSincePlanted").GetValue<double>();
			float growTime = Traverse.Create(__instance).Method("GetGrowTime").GetValue<float>();

			// Don't change hover text when item is done
			if (timeSincePlanted >= growTime) return __result;

			double percentage = (timeSincePlanted / growTime) * 100.0;
			string color = GetColorStringFromPercentage(percentage);
			string growPercentage = GetValueAsColoredString(color, Math.Round(percentage, _configAmountOfDecimals.Value, MidpointRounding.AwayFromZero));
			string logMessage = "Plant percentage: " + percentage + ", time planted: " + timeSincePlanted + ", grow time: " + growTime;

			if (_configShowTime.Value)
			{
				double timeRemaining = growTime - timeSincePlanted;
				string formattedTime = FormatSecondsAsString(timeRemaining);
				growPercentage += ", " + formattedTime;
				logMessage += "\nPlant timeRemaining: " + timeRemaining + " (seconds), formatted: " + formattedTime;
			}
			LogInfoThrottled(__instance, logMessage);

			string newResult = __result.Replace(" )", $", {growPercentage} )");
			// Put extra info on new line if time is included
			if (_configShowTime.Value)
			{
				return newResult.Replace(" (", "\n(");
			}
			return newResult;
		}
		else if (_configShowTime.Value)
		{
			double timeSincePlanted = Traverse.Create(__instance).Method("TimeSincePlanted").GetValue<double>();
			float growTime = Traverse.Create(__instance).Method("GetGrowTime").GetValue<float>();

			// Don't change hover text when item is done
			if (timeSincePlanted >= growTime) return __result;

			double timeRemaining = growTime - timeSincePlanted;
			string formattedTime = FormatSecondsAsString(timeRemaining);
			LogInfoThrottled(__instance, "Plant timeRemaining: " + timeRemaining + " (seconds), formatted: " + formattedTime);

			return __result.Replace(" )", ", " + formattedTime + " )");
		}

		return __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
	public static string BerryBushPickable_Patch(string __result, Pickable __instance, ZNetView ___m_nview)
	{
		if (__instance == null) return __result;
		if (!_bushList.Contains(__instance.name)) return __result;

		if (_configShowPercentage.Value)
		{
			DateTime startTime = new(___m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L));
			double timeSinceStart = (ZNet.instance.GetTime() - startTime).TotalSeconds;
			double respawnTimeSeconds = __instance.m_respawnTimeMinutes * 60.0;

			// Don't change hover text when item is done
			if (timeSinceStart >= respawnTimeSeconds) return __result;

			double percentage = (timeSinceStart / respawnTimeSeconds) * 100.0;
			string color = GetColorStringFromPercentage(percentage);
			string growPercentage = GetValueAsColoredString(color, Math.Round(percentage, _configAmountOfDecimals.Value, MidpointRounding.AwayFromZero));
			string logMessage = "BerryBushPickable percentage: " + percentage + ", time since start: " + timeSinceStart + ", respawn time: " + respawnTimeSeconds;

			if (_configShowTime.Value)
			{
				double timeRemaining = respawnTimeSeconds - timeSinceStart;
				string formattedTime = FormatSecondsAsString(timeRemaining);
				growPercentage += ", " + formattedTime;
				logMessage += "\nBerryBushPickable timeRemaining: " + timeRemaining + " (seconds), formatted: " + formattedTime;
			}
			LogInfoThrottled(__instance, logMessage);

			string instanceName = Localization.instance.Localize(__instance.GetHoverName());
			return __result + $"{instanceName} ( {growPercentage} )";
		}
		else if (_configShowTime.Value)
		{
			DateTime startTime = new(___m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L));
			double timeSinceStart = (ZNet.instance.GetTime() - startTime).TotalSeconds;
			double respawnTimeSeconds = __instance.m_respawnTimeMinutes * 60.0;

			// Don't change hover text when item is done
			if (timeSinceStart >= respawnTimeSeconds) return __result;

			double timeRemaining = respawnTimeSeconds - timeSinceStart;
			string formattedTime = FormatSecondsAsString(timeRemaining);
			LogInfoThrottled(__instance, "BerryBushPickable timeRemaining: " + timeRemaining + " (seconds), formatted: " + formattedTime);

			string instanceName = Localization.instance.Localize(__instance.GetHoverName());
			return __result + $"{instanceName} ( {formattedTime} )";
		}

		return __result;
	}
}
