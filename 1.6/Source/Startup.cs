using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        private static Dictionary<ResearchProjectDef, ResearchTabDef> originalTabs = new Dictionary<ResearchProjectDef, ResearchTabDef>();
        private static bool isCollapsed;

        static Startup()
        {

            if (DefsOf.Anomaly != null)
            {
                foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
                {
                    if (def.knowledgeCategory != null || def.knowledgeCost > 0f)
                    {
                        def.tab = DefsOf.Anomaly;
                    }
                }
            }

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
            GenerateEmergenceDefs();
            UpdateEmergenceNodes();
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

        private static void GenerateEmergenceDefs()
        {
            var levels = new[] { TechLevel.Neolithic, TechLevel.Medieval, TechLevel.Industrial, TechLevel.Spacer, TechLevel.Ultra, TechLevel.Archotech };
            foreach (var level in levels)
            {
                var def = new ResearchProjectDef();
                def.defName = "BRM_Emergence_" + level;
                def.label = level.ToStringHuman().CapitalizeFirst();
                def.description = "BRM_EmergenceDesc".Translate(level.ToStringHuman().CapitalizeFirst());
                def.techLevel = (TechLevel)((int)level - 1);
                def.tab = DefsOf.Main;
                def.baseCost = BetterResearchMenuMod.settings.GetEmergenceCost(level);
                def.modExtensions = [new EmergenceExtension { targetLevel = level }];
                def.researchViewX = 99f;
                def.researchViewY = 99f;
                def.prerequisites = [];
                def.hiddenPrerequisites = [];
                def.requiredResearchFacilities = [];
                def.tags = [];
                def.heldByFactionCategoryTags = [];
                def.customUnlockTexts = [];
                def.PostLoad();
                def.ResolveReferences();
                DefDatabase<ResearchProjectDef>.Add(def);
            }
        }

        private static List<(ResearchProjectDef def, EmergenceExtension ext)> cachedEmergenceDefs = new List<(ResearchProjectDef, EmergenceExtension)>();

        public static void UpdateEmergenceNodes()
        {
            if (cachedEmergenceDefs.Count == 0)
            {
                var allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
                for (int i = 0; i < allDefs.Count; i++)
                {
                    var p = allDefs[i];
                    var ext = p.GetModExtension<EmergenceExtension>();
                    if (ext != null)
                    {
                        cachedEmergenceDefs.Add((p, ext));
                    }
                }
            }

            var mainProjectsByLevel = new Dictionary<TechLevel, List<ResearchProjectDef>>();
            var allProjects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allProjects.Count; i++)
            {
                var p = allProjects[i];
                if (p.tab == DefsOf.Main && !p.HasModExtension<EmergenceExtension>())
                {
                    if (!mainProjectsByLevel.TryGetValue(p.techLevel, out var list))
                    {
                        list = new List<ResearchProjectDef>();
                        mainProjectsByLevel[p.techLevel] = list;
                    }
                    list.Add(p);
                }
            }

            var tiedToFoundations = BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.Foundations;

            for (int k = 0; k < cachedEmergenceDefs.Count; k++)
            {
                var tuple = cachedEmergenceDefs[k];
                var def = tuple.def;
                var ext = tuple.ext;

                def.baseCost = BetterResearchMenuMod.settings.GetEmergenceCost(ext.targetLevel);
                def.prerequisites.Clear();

                if (!mainProjectsByLevel.TryGetValue(def.techLevel, out var eraProjects))
                {
                    continue;
                }

                var prereqs = new List<ResearchProjectDef>();

                if (tiedToFoundations)
                {
                    for (int i = 0; i < eraProjects.Count; i++)
                    {
                        if (eraProjects[i].HasModExtension<ResearchFoundationExtension>())
                        {
                            prereqs.Add(eraProjects[i]);
                        }
                    }
                }
                def.prerequisites.AddRange(prereqs);
            }
        }
    }
}
