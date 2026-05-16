using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(ResearchProjectDef), "CanStartNow", MethodType.Getter)]
    public static class ResearchProjectDef_CanStartNow_Patch
    {
        public static void Postfix(ResearchProjectDef __instance, ref bool __result)
        {
            if (__result && MainTabWindow_BetterResearch.GodModeReveal is false && BetterResearchMenuMod.settings.restrictResearchToTechLevel)
            {
                if (DefsOf.Anomaly != null && __instance.tab == DefsOf.Anomaly) return;
                if (DefsOf.VGE_Gravtech != null && __instance.tab == DefsOf.VGE_Gravtech) return;

                if (__instance.techLevel > Faction.OfPlayer.def.techLevel)
                    __result = false;
            }

            if (__result && __instance.HasModExtension<EmergenceExtension>())
            {
                if (!BetterResearchMenuMod.settings.enableEmergence)
                {
                    __result = false;
                    return;
                }
                var progress = MainTabWindow_BetterResearch.GetAdvancementProgressRaw(__instance.techLevel, DefsOf.Main, out _, out _);
                float threshold = BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.EraCompletion ? BetterResearchMenuMod.settings.eraCompletionPercentage : 1f;
                if (progress < threshold) __result = false;
            }
        }
    }
}
