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
