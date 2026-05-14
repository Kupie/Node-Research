using HarmonyLib;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    public class BetterResearchMenuMod : Mod
    {
        public static BetterResearchMenuSettings settings;
        public BetterResearchMenuMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<BetterResearchMenuSettings>();
            new Harmony("BetterResearchMenuMod").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("BRM_RestrictResearchTechLevel".Translate(), ref settings.restrictResearchToTechLevel);
            ls.CheckboxLabeled("BRM_RestrictViewingFutureTechLevels".Translate(), ref settings.restrictViewingFutureTechLevels);
            ls.CheckboxLabeled("BRM_RestrictViewingFutureProjects".Translate(), ref settings.restrictViewingFutureProjects);
            ls.CheckboxLabeled("BRM_EnableTechAdvancement".Translate(), ref settings.enableTechAdvancement);
            if (ModsConfig.IsActive("OskarPotocki.VFE.Tribals"))
            {
                ls.CheckboxLabeled("BRM_DisableVFETribalsAdvancement".Translate(), ref settings.disableVFETribalsAdvancement);
            }
            settings.repelForceMultiplier = ls.SliderLabeled("BRM_RepelForceMultiplier".Translate(settings.repelForceMultiplier.ToString("F1")), settings.repelForceMultiplier, 0.1f, 5f);
            ls.End();
        }

        public override string SettingsCategory() => Content.Name;
    }
}
