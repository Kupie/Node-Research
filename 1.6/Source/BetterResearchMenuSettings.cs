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
		
		// experimental TG fixes
		public bool disableTweaksGaloreResearchChanges = false;
		
        public bool startCollapsed = false;
        public bool neverCollapseFoundations = true;
        public bool physicsEnabled = true;

        public bool collapseOnCompletion = false;
        public int maxExpandedNodes = -1;
        public bool autoOpenMenuOnFinish = true;
        public bool forbidVanillaMenu = false;
        public AdvancementType advancementTiedTo = AdvancementType.Foundations;
        public float eraCompletionPercentage = 1f;

        public bool revealAllInGodMode = true;
        public bool revealMysteryNodeOnHover = false;
        public bool enableEmergence = true;
        public float emergenceCostNeolithic = 500f;
        public float emergenceCostMedieval = 5000f;
        public float emergenceCostIndustrial = 10000f;
        public float emergenceCostSpacer = 20000f;
        public float emergenceCostUltra = 40000f;
        public float emergenceCostArchotech = 100000f;

        public float spacingForceMultiplier = 1.634f;
        public float contractingForceMultiplier = 1.09f;
        public float centerForceMultiplier = 1f;

        public bool autoRevealNodes = false;
        public float wasdPanSpeed = 15f;
        public float zoomSensitivity = 0.05f;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref restrictResearchToTechLevel, "restrictResearchToTechLevel", true);
            Scribe_Values.Look(ref restrictViewingFutureTechLevels, "restrictViewingFutureTechLevels", true);
            Scribe_Values.Look(ref restrictViewingFutureProjects, "restrictViewingFutureProjects", true);
            Scribe_Values.Look(ref enableTechAdvancement, "enableTechAdvancement", true);
            Scribe_Values.Look(ref disableVFETribalsAdvancement, "disableVFETribalsAdvancement", true);
			Scribe_Values.Look(ref disableTweaksGaloreResearchChanges, "disableTweaksGaloreResearchChanges", false);
            Scribe_Values.Look(ref startCollapsed, "startCollapsed", false);
            Scribe_Values.Look(ref neverCollapseFoundations, "neverCollapseFoundations", true);
            Scribe_Values.Look(ref physicsEnabled, "physicsEnabled", true);

            Scribe_Values.Look(ref collapseOnCompletion, "collapseOnCompletion", false);
            Scribe_Values.Look(ref maxExpandedNodes, "maxExpandedNodes", -1);
            Scribe_Values.Look(ref autoOpenMenuOnFinish, "autoOpenMenuOnFinish", true);
            Scribe_Values.Look(ref forbidVanillaMenu, "forbidVanillaMenu", false);
            Scribe_Values.Look(ref advancementTiedTo, "advancementTiedTo", AdvancementType.Foundations);
            Scribe_Values.Look(ref eraCompletionPercentage, "eraCompletionPercentage", 1f);

            Scribe_Values.Look(ref revealAllInGodMode, "revealAllInGodMode", true);
            Scribe_Values.Look(ref revealMysteryNodeOnHover, "revealMysteryNodeOnHover", false);
            Scribe_Values.Look(ref enableEmergence, "enableEmergence", true);
            Scribe_Values.Look(ref emergenceCostNeolithic, "emergenceCostNeolithic", 500f);
            Scribe_Values.Look(ref emergenceCostMedieval, "emergenceCostMedieval", 5000f);
            Scribe_Values.Look(ref emergenceCostIndustrial, "emergenceCostIndustrial", 10000f);
            Scribe_Values.Look(ref emergenceCostSpacer, "emergenceCostSpacer", 20000f);
            Scribe_Values.Look(ref emergenceCostUltra, "emergenceCostUltra", 40000f);
            Scribe_Values.Look(ref emergenceCostArchotech, "emergenceCostArchotech", 100000f);

            Scribe_Values.Look(ref spacingForceMultiplier, "spacingForceMultiplier", 1.634f);
            Scribe_Values.Look(ref contractingForceMultiplier, "contractingForceMultiplier", 1.09f);
            Scribe_Values.Look(ref centerForceMultiplier, "centerForceMultiplier", 1f);

            Scribe_Values.Look(ref autoRevealNodes, "autoRevealNodes", false);
            Scribe_Values.Look(ref wasdPanSpeed, "wasdPanSpeed", 15f);
            Scribe_Values.Look(ref zoomSensitivity, "zoomSensitivity", 0.05f);
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
