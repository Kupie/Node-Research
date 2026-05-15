using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    public class ResearchIconExtension : DefModExtension
    {
        public string texPath;
        public string markerTexPath;
    }

    public class ResearchFoundationExtension : DefModExtension
    {
    }

    public class EmergenceExtension : DefModExtension
    {
        public TechLevel targetLevel;
    }

    public static class Extensions
    {
        public static bool IsFoundation(this ResearchNode node)
        {
            return !node.isPhantom && node.def.IsFoundation();
        }

        public static bool IsFoundation(this ResearchProjectDef def)
        {
            return def.HasModExtension<ResearchFoundationExtension>();
        }
    }
}
