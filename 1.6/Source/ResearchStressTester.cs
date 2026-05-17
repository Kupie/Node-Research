using System.Collections.Generic;
using Verse;
using RimWorld;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class ResearchStressTester
    {
        public static List<ResearchProjectDef> AllTestNodes = new List<ResearchProjectDef>();

        static ResearchStressTester()
        {
            string[] parents = { "Electricity", "Fabrication", "Machining", "MicroelectronicsBasics", "Smithing" };
            var mainTab = DefsOf.Main;

            for (int i = 0; i < 600; i++)
            {
                var parentA = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(parents[i % parents.Length]);
                if (parentA == null) continue;

                var newProj = new ResearchProjectDef
                {
                    defName = $"StressTest_{i}",
                    label = $"ST {i}",
                    baseCost = 1000,
                    techLevel = parentA.techLevel,
                    tab = mainTab,
                    prerequisites = new List<ResearchProjectDef>()
                };

                if (i > 5 && Rand.Value < 0.4f)
                {
                    newProj.prerequisites.Add(AllTestNodes[i - 5]);
                }
                else if (Rand.Value < 0.15f)
                {
                    var parentB = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(parents[(i + 1) % parents.Length]);
                    newProj.prerequisites.Add(parentA);
                    newProj.prerequisites.Add(parentB);
                }
                else
                {
                    newProj.prerequisites.Add(parentA);
                }

                AllTestNodes.Add(newProj);
                DefDatabase<ResearchProjectDef>.Add(newProj);
            }
        }
    }
}