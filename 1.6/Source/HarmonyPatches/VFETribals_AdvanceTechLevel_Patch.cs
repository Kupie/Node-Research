using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch]
    public static class VFETribals_AdvanceTechLevel_Patch
    {
        public static bool Prepare() => ModsConfig.IsActive("OskarPotocki.VFE.Tribals");
        public static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("VFETribals.GameComponent_Tribals"), "AdvanceTechLevel");

        public static bool Prefix(object __instance)
        {
            if (BetterResearchMenuMod.settings.disableVFETribalsAdvancement)
            {
                var prop = AccessTools.Field(__instance.GetType(), "playerTechLevel");
                prop.SetValue(__instance, Faction.OfPlayer.def.techLevel);
                return false;
            }
            return true;
        }
    }
}
