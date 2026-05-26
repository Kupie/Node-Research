using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class Game_FinalizeInit_Patch
    {
        public static void Postfix()
        {
            var highestFinishedEmergence = TechLevel.Undefined;
            var allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                var def = allDefs[i];
                var ext = def.GetModExtension<EmergenceExtension>();
                if (ext != null && def.IsFinished && ext.targetLevel > highestFinishedEmergence)
                {
                    highestFinishedEmergence = ext.targetLevel;
                }
            }

            if (highestFinishedEmergence != TechLevel.Undefined && Faction.OfPlayer != null && highestFinishedEmergence > Faction.OfPlayer.def.techLevel)
            {
                Faction.OfPlayer.def.techLevel = highestFinishedEmergence;
                State.currentSavedTechLevel = highestFinishedEmergence;
            }

            if (State.initialized) return;
            State.initialized = true;

            var factionLevel = Faction.OfPlayer.def.techLevel;
            for (int i = 0; i < allDefs.Count; i++)
            {
                var def = allDefs[i];
                var ext = def.GetModExtension<EmergenceExtension>();
                if (ext != null && factionLevel >= ext.targetLevel && !def.IsFinished)
                {
                    Find.ResearchManager.progress[def] = def.baseCost;
                }
            }
        }
    }
}
