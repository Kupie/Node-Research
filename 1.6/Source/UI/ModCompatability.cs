using System.Reflection;
using HarmonyLib;
using Verse;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class ModCompatability
    {
        private static MethodInfo isResearchLockedByDiscoveryMethod;
        private static bool discoveriesModActive;
        static ModCompatability()
        {
            discoveriesModActive = ModsConfig.IsActive("ferny.Discoveries");
            if (discoveriesModActive)
            {
                var discoveryTrackerType = AccessTools.TypeByName("Discoveries.DiscoveryTracker");
                if (discoveryTrackerType != null)
                {
                    isResearchLockedByDiscoveryMethod = AccessTools.Method(discoveryTrackerType, "IsResearchLockedByDiscovery");
                    if (isResearchLockedByDiscoveryMethod == null)
                    {
                        Log.Error("[BetterResearchMenu] Discoveries mod is active but IsResearchLockedByDiscovery method not found");
                    }
                }
                else
                {
                    Log.Error("[BetterResearchMenu] Discoveries mod is active but DiscoveryTracker type not found");
                }
            }
        }

        public static bool IsResearchLockedByDiscovery(ResearchProjectDef research)
        {
            if (!discoveriesModActive)
                return false;
            return (bool)isResearchLockedByDiscoveryMethod.Invoke(null, new object[] { research });
        }
    }
}
