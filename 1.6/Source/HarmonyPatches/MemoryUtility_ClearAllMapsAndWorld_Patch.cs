using HarmonyLib;
using Verse.Profile;

namespace BetterResearchMenu
{
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public static class MemoryUtility_ClearAllMapsAndWorld_Patch
    {
        public static void Prefix()
        {
            State.Clear();
        }
    }
}
