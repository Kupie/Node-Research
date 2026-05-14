using Verse;

namespace BetterResearchMenu
{
    public class BetterResearchMenuSettings : ModSettings
    {
        public bool restrictResearchToTechLevel = true;
        public bool restrictViewingFutureTechLevels = true;
        public bool restrictViewingFutureProjects = true;
        public bool enableTechAdvancement = true;
        public bool disableVFETribalsAdvancement = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref restrictResearchToTechLevel, "restrictResearchToTechLevel", true);
            Scribe_Values.Look(ref restrictViewingFutureTechLevels, "restrictViewingFutureTechLevels", true);
            Scribe_Values.Look(ref restrictViewingFutureProjects, "restrictViewingFutureProjects", true);
            Scribe_Values.Look(ref enableTechAdvancement, "enableTechAdvancement", true);
            Scribe_Values.Look(ref disableVFETribalsAdvancement, "disableVFETribalsAdvancement", true);
        }
    }
}
