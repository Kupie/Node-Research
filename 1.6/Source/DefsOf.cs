using RimWorld;
using Verse;

namespace BetterResearchMenu
{
    [DefOf]
    public static class DefsOf
    {
        public static ResearchTabDef Main;
        [MayRequireAnomaly]
        public static ResearchTabDef Anomaly;

        [MayRequire("vanillaexpanded.gravship")]
        public static ResearchTabDef VGE_Gravtech;
        [MayRequire("vanillaexpanded.gravship")]
        public static ResearchProjectDef BasicGravtech;

        public static SoundDef BRM_ExpandingNode;
        public static SoundDef BRM_CollapsingNode;
        public static SoundDef BRM_Advancement;

        static DefsOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DefsOf));
    }
}
