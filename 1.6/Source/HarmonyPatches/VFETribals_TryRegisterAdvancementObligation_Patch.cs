using System.Reflection;
using HarmonyLib;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch]
    public static class VFETribals_TryRegisterAdvancementObligation_Patch
    {
        public static bool Prepare() => ModsConfig.IsActive("OskarPotocki.VFE.Tribals");
        public static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("VFETribals.GameComponent_Tribals"), "TryRegisterAdvancementObligation");
        public static bool Prefix() => !BetterResearchMenuMod.settings.disableVFETribalsAdvancement;
    }
}
