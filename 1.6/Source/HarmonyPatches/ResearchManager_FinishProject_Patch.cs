using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class ResearchManager_FinishProject_Patch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            // Advance research queue
            State.researchQueue.Remove(proj.defName);
            while (State.researchQueue.Count > 0)
            {
                var nextDefName = State.researchQueue[0];
                var nextDef = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(nextDefName);
                if (nextDef == null || nextDef.IsFinished)
                {
                    State.researchQueue.RemoveAt(0);
                    continue;
                }
                if (nextDef.CanStartNow)
                {
                    Find.ResearchManager.SetCurrentProject(nextDef);
                    TutorSystem.Notify_Event("StartResearchProject");
                }
                break;
            }

            if (BetterResearchMenuMod.settings.collapseOnCompletion)
            {
                State.nodeStates[proj.defName] = NodeState.Minimized;
                State.expandedNodeOrder.Remove(proj.defName);
            }

            bool isEmergence = proj.HasModExtension<EmergenceExtension>();
            if (isEmergence)
            {
                var ext = proj.GetModExtension<EmergenceExtension>();
                Faction.OfPlayer.def.techLevel = ext.targetLevel;
                Find.WindowStack.Add(new Window_TechAdvance(ext.targetLevel));
                DefsOf.BRM_Advancement.PlayOneShotOnCamera();
                VFETribalsCompat.GrantCornerstonePoint();
                if (Find.WindowStack.WindowOfType<MainTabWindow_BetterResearch>() is MainTabWindow_BetterResearch win)
                    win.ForceEra(TechLevel.Undefined);
            }

            if (!isEmergence && Find.TickManager.TicksGame > 0 && BetterResearchMenuMod.settings.autoOpenMenuOnFinish && Current.ProgramState == ProgramState.Playing)
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research));
            }
        }
    }
}
