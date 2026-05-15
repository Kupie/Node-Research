using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class EmergenceGenerator
    {
        static EmergenceGenerator()
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
            UpdateEmergenceNodes();
        }

        public static void UpdateEmergenceNodes()
        {
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(x => x.HasModExtension<EmergenceExtension>()))
            {
                var ext = def.GetModExtension<EmergenceExtension>();
                def.baseCost = BetterResearchMenuMod.settings.GetEmergenceCost(ext.targetLevel);
                def.prerequisites.Clear();
                var eraProjects = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(x => x.techLevel == def.techLevel && x.tab == DefsOf.Main && x != def).ToList();
                var prereqs = BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.Foundations
                    ? eraProjects.Where(x => x.HasModExtension<ResearchFoundationExtension>()).ToList()
                    : eraProjects.Except(eraProjects.SelectMany(x => x.prerequisites ?? new List<ResearchProjectDef>()).Distinct()).ToList();
                def.prerequisites.AddRange(prereqs);
            }
        }
    }
}