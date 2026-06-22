using HarmonyLib;
using Verse;

namespace BetterResearchMenu
{
	// Blocks TweaksGalore.SectionWorker_ResearchProjects.DoOnStartup from
	// overwriting research def baseCost and techLevel on every game boot.
	[HarmonyPatch]
	public static class TweaksGalore_DoOnStartup_Patch
	{
		static bool Prepare()
		{
			return ModsConfig.IsActive("Neronix17.TweaksGalore");
		}

		static System.Reflection.MethodBase TargetMethod()
		{
			var type = AccessTools.TypeByName("TweaksGalore.SectionWorker_ResearchProjects");
			return AccessTools.Method(type, "DoOnStartup");
		}

		[HarmonyPrefix]
		public static bool Prefix()
		{
			return !BetterResearchMenuMod.settings.disableTweaksGaloreResearchChanges;
		}
	}

	// Blocks TweaksGalore.Patch_ResearchManager_ReapplyAllMods.Postfix from
	// resetting and re-advancing the player faction tech level on every load.
	[HarmonyPatch]
	public static class TweaksGalore_ReapplyAllMods_Patch
	{
		static bool Prepare()
		{
			return ModsConfig.IsActive("Neronix17.TweaksGalore");
		}

		static System.Reflection.MethodBase TargetMethod()
		{
			var type = AccessTools.TypeByName("TweaksGalore.Patch_ResearchManager_ReapplyAllMods");
			return AccessTools.Method(type, "Postfix");
		}

		[HarmonyPrefix]
		public static bool Prefix()
		{
			return !BetterResearchMenuMod.settings.disableTweaksGaloreResearchChanges;
		}
	}
}