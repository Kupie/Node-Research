using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    public enum AdvancementType { Foundations, EraCompletion }

    public class BetterResearchMenuSettings : ModSettings
    {
        public bool restrictResearchToTechLevel = true;
        public bool restrictViewingFutureTechLevels = true;
        public bool restrictViewingFutureProjects = true;
        public bool enableTechAdvancement = true;
        public bool disableVFETribalsAdvancement = true;
        public bool startCollapsed = true;
        public bool neverCollapseFoundations = true;
        public bool physicsEnabled = true;

        public bool collapseOnCompletion = false;
        public int maxExpandedNodes = -1;
        public bool autoOpenMenuOnFinish = true;
        public bool forbidVanillaMenu = false;
        public AdvancementType advancementTiedTo = AdvancementType.Foundations;
        public float eraCompletionPercentage = 1f;

        public bool revealAllInGodMode = true;
        public bool enableEmergence = true;
        public float emergenceCostNeolithic = 500f;
        public float emergenceCostMedieval = 1000f;
        public float emergenceCostIndustrial = 2000f;
        public float emergenceCostSpacer = 3000f;
        public float emergenceCostUltra = 4000f;
        public float emergenceCostArchotech = 5000f;

        public float spacingForceMultiplier = 1f;
        public float contractingForceMultiplier = 1f;
        public float centerForceMultiplier = 1f;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref restrictResearchToTechLevel, "restrictResearchToTechLevel", true);
            Scribe_Values.Look(ref restrictViewingFutureTechLevels, "restrictViewingFutureTechLevels", true);
            Scribe_Values.Look(ref restrictViewingFutureProjects, "restrictViewingFutureProjects", true);
            Scribe_Values.Look(ref enableTechAdvancement, "enableTechAdvancement", true);
            Scribe_Values.Look(ref disableVFETribalsAdvancement, "disableVFETribalsAdvancement", true);
            Scribe_Values.Look(ref startCollapsed, "startCollapsed", true);
            Scribe_Values.Look(ref neverCollapseFoundations, "neverCollapseFoundations", true);
            Scribe_Values.Look(ref physicsEnabled, "physicsEnabled", true);

            Scribe_Values.Look(ref collapseOnCompletion, "collapseOnCompletion", false);
            Scribe_Values.Look(ref maxExpandedNodes, "maxExpandedNodes", -1);
            Scribe_Values.Look(ref autoOpenMenuOnFinish, "autoOpenMenuOnFinish", true);
            Scribe_Values.Look(ref forbidVanillaMenu, "forbidVanillaMenu", false);
            Scribe_Values.Look(ref advancementTiedTo, "advancementTiedTo", AdvancementType.Foundations);
            Scribe_Values.Look(ref eraCompletionPercentage, "eraCompletionPercentage", 1f);

            Scribe_Values.Look(ref revealAllInGodMode, "revealAllInGodMode", true);
            Scribe_Values.Look(ref enableEmergence, "enableEmergence", true);
            Scribe_Values.Look(ref emergenceCostNeolithic, "emergenceCostNeolithic", 500f);
            Scribe_Values.Look(ref emergenceCostMedieval, "emergenceCostMedieval", 1000f);
            Scribe_Values.Look(ref emergenceCostIndustrial, "emergenceCostIndustrial", 2000f);
            Scribe_Values.Look(ref emergenceCostSpacer, "emergenceCostSpacer", 3000f);
            Scribe_Values.Look(ref emergenceCostUltra, "emergenceCostUltra", 4000f);
            Scribe_Values.Look(ref emergenceCostArchotech, "emergenceCostArchotech", 5000f);

            Scribe_Values.Look(ref spacingForceMultiplier, "spacingForceMultiplier", 1f);
            Scribe_Values.Look(ref contractingForceMultiplier, "contractingForceMultiplier", 1f);
            Scribe_Values.Look(ref centerForceMultiplier, "centerForceMultiplier", 1f);
        }

        public float GetEmergenceCost(TechLevel level)
        {
            switch (level)
            {
                case TechLevel.Neolithic: return emergenceCostNeolithic;
                case TechLevel.Medieval: return emergenceCostMedieval;
                case TechLevel.Industrial: return emergenceCostIndustrial;
                case TechLevel.Spacer: return emergenceCostSpacer;
                case TechLevel.Ultra: return emergenceCostUltra;
                case TechLevel.Archotech: return emergenceCostArchotech;
                default: return 1000f;
            }
        }
    }
}
