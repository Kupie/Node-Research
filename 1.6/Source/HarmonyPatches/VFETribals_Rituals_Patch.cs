using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.ShouldShowGizmo))]
    public static class VFETribals_Rituals_Patch
    {
        public static bool Prepare() => ModsConfig.IsActive("OskarPotocki.VFE.Tribals");

        public static void Postfix(Precept_Ritual __instance, ref bool __result)
        {
            if (BetterResearchMenuMod.settings.disableVFETribalsAdvancement && __instance.def.defName.StartsWith("VFET_AdvanceTo"))
            {
                __result = false;
            }
        }
    }
}