using System.Reflection;
using HarmonyLib;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch]
    public static class Profectus_CanResearch_Patch
    {
        public static bool Prepare() => ModsConfig.IsActive("oskarpotocki.vfe.classical");

        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("VFEC.Perks.Workers.Profectus");
            return AccessTools.Method(type, "CanResearch");
        }

        public static void Postfix(ResearchProjectDef proj, ref bool __result)
        {
            if (__result && proj.HasModExtension<EmergenceExtension>())
            {
                __result = false;
            }
        }
    }
}
