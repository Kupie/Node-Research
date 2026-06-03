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
            if (__instance.HasModExtension<EmergenceExtension>())
            {
                var progress = MainTabWindow_BetterResearch.GetAdvancementProgressRaw(__instance.techLevel, DefsOf.Main, out _, out _);
                __result = BetterResearchMenuMod.settings.enableEmergence && progress >= 1f && !__instance.IsFinished && __instance.techLevel <= Faction.OfPlayer.def.techLevel;
                return;
            }

            if (__result && MainTabWindow_BetterResearch.GodModeReveal is false && BetterResearchMenuMod.settings.restrictResearchToTechLevel)
            {
                if (DefsOf.Anomaly != null && __instance.tab == DefsOf.Anomaly) return;
                if (DefsOf.VGE_Gravtech != null && __instance.tab == DefsOf.VGE_Gravtech) return;

                if (__instance.techLevel > Faction.OfPlayer.def.techLevel)
                    __result = false;
            }
        }
    }
}
