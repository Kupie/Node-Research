using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class TabCollapser
    {
        static TabCollapser()
        {
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (DefsOf.Anomaly != null && def.tab == DefsOf.Anomaly) continue;
                if (DefsOf.VGE_Gravtech != null && def.tab == DefsOf.VGE_Gravtech) continue;
                def.tab = DefsOf.Main;
                if (def.techLevel == TechLevel.Undefined)
                {
                    def.techLevel = TechLevel.Industrial;
                    Log.Error(def.label + " research project lacks a techLevel value, assigning to industrial");
                }
            }

            var mainBtn = MainButtonDefOf.Research;
            if (mainBtn != null)
            {
                mainBtn.tabWindowClass = typeof(MainTabWindow_BetterResearch);
                mainBtn.tabWindowInt = null;
            }
        }
    }
}
