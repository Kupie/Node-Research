using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace BetterResearchMenu
{
    [StaticConstructorOnStartup]
    public static class VFETribalsCompat
    {
        private static bool active;
        private static FieldInfo instanceField;
        private static MethodInfo offsetMethod;

        static VFETribalsCompat()
        {
            active = ModsConfig.IsActive("oskarpotocki.vfe.tribals");
            if (!active)
                return;

            var type = AccessTools.TypeByName("VFETribals.GameComponent_Tribals");
            if (type == null)
            {
                Log.Error("[BetterResearchMenu] VFE Tribals is active but GameComponent_Tribals type not found");
                active = false;
                return;
            }

            instanceField = AccessTools.Field(type, "Instance");
            if (instanceField == null)
            {
                Log.Error("[BetterResearchMenu] VFE Tribals GameComponent_Tribals.Instance field not found");
                active = false;
                return;
            }

            offsetMethod = AccessTools.Method(type, "OffsetAvailableCornerstonePoints", new[] { typeof(int) });
            if (offsetMethod == null)
            {
                Log.Error("[BetterResearchMenu] VFE Tribals GameComponent_Tribals.OffsetAvailableCornerstonePoints method not found");
                active = false;
                return;
            }
        }

        public static void GrantCornerstonePoint()
        {
            if (!active)
                return;
            if (!BetterResearchMenuMod.settings.disableVFETribalsAdvancement)
                return;

            try
            {
                var instance = instanceField?.GetValue(null);
                if (instance != null)
                {
                    offsetMethod?.Invoke(instance, new object[] { 1 });
                }
            }
            catch (Exception ex)
            {
                Log.Error("[BetterResearchMenu] Failed to grant VFE Tribals cornerstone point: " + ex);
            }
        }
    }
}
