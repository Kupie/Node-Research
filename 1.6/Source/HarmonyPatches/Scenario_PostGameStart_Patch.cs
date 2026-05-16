using HarmonyLib;
using RimWorld;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.PostGameStart))]
    public static class Scenario_PostGameStart_Patch
    {
        public static void Postfix()
        {
            State.startingScenarioTechLevel = Faction.OfPlayer.def.techLevel;
        }
    }
}
