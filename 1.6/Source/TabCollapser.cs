using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class TabCollapser
    {
        private static Dictionary<ResearchProjectDef, ResearchTabDef> originalTabs = new Dictionary<ResearchProjectDef, ResearchTabDef>();
        private static bool isCollapsed;

        static TabCollapser()
        {
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                originalTabs[def] = def.tab;
                if (DefsOf.Anomaly != null && def.tab == DefsOf.Anomaly) continue;
                if (DefsOf.VGE_Gravtech != null && def.tab == DefsOf.VGE_Gravtech) continue;
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

            Collapse();
        }

        public static void Collapse()
        {
            if (isCollapsed) return;
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (DefsOf.Anomaly != null && def.tab == DefsOf.Anomaly) continue;
                if (DefsOf.VGE_Gravtech != null && def.tab == DefsOf.VGE_Gravtech) continue;
                def.tab = DefsOf.Main;
            }
            isCollapsed = true;
        }

        public static void Restore()
        {
            if (!isCollapsed) return;
            foreach (var kvp in originalTabs)
            {
                kvp.Key.tab = kvp.Value;
            }
            isCollapsed = false;
        }
    }
}
