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

        public static SoundDef BRM_ExpandingNode;

        static DefsOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DefsOf));
    }
}
