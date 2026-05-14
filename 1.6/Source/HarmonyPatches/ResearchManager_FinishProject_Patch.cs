using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public static class ResearchManager_FinishProject_Patch
    {
        public static void Postfix()
        {
            if (Find.WindowStack.IsOpen<MainTabWindow_BetterResearch>())
                Find.WindowStack.WindowOfType<MainTabWindow_BetterResearch>().InitPhysics();
        }
    }
}
