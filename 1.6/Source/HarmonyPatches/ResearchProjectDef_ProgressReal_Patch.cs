using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.ProgressReal), MethodType.Getter)]
    public static class ResearchProjectDef_ProgressReal_Patch
    {
        public static void Postfix(ResearchProjectDef __instance, ref float __result)
        {
            var ext = __instance.GetModExtension<EmergenceExtension>();
            if (ext != null && Faction.OfPlayerSilentFail?.def.techLevel > __instance.techLevel)
            {
                __result = __instance.Cost;
            }
        }
    }
}
