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
            if (__result && BetterResearchMenuMod.settings.restrictResearchToTechLevel)
                if (__instance.techLevel > Faction.OfPlayer.def.techLevel)
                    __result = false;
        }
    }
}
