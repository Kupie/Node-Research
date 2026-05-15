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
            if (BetterResearchMenuMod.settings.collapseOnCompletion)
            {
                State.nodeStates[proj.defName] = NodeState.Minimized;
                State.expandedNodeOrder.Remove(proj.defName);
            }

            if (proj.HasModExtension<EmergenceExtension>())
            {
                var ext = proj.GetModExtension<EmergenceExtension>();
                Faction.OfPlayer.def.techLevel = ext.targetLevel;
                Find.WindowStack.Add(new Window_TechAdvance(ext.targetLevel));
                DefsOf.BRM_Advancement.PlayOneShotOnCamera();
                if (Find.WindowStack.WindowOfType<MainTabWindow_BetterResearch>() is MainTabWindow_BetterResearch win)
                    win.ForceEra(ext.targetLevel);
            }

            if (BetterResearchMenuMod.settings.autoOpenMenuOnFinish && Current.ProgramState == ProgramState.Playing)
            {
                Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
            }
        }
    }
}
