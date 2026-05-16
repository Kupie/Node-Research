using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    [HotSwappable]
    public class BetterResearchMenuMod : Mod
    {
        public static BetterResearchMenuSettings settings;
        public BetterResearchMenuMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<BetterResearchMenuSettings>();
            new Harmony("BetterResearchMenuMod").PatchAll();
        }
        private Vector2 scrollPos;
        private float scrollHeight = 99999999;
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var viewRect = new Rect(0f, 0f, inRect.width - 16, scrollHeight);
            scrollHeight = 0;
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            var ls = new Listing_Standard();
            ls.Begin(viewRect);
            var initY = ls.curY;
            ls.CheckboxLabeled("BRM_RestrictResearchTechLevel".Translate(), ref settings.restrictResearchToTechLevel);
            ls.CheckboxLabeled("BRM_RestrictViewingFutureTechLevels".Translate(), ref settings.restrictViewingFutureTechLevels);
            ls.CheckboxLabeled("BRM_RestrictViewingFutureProjects".Translate(), ref settings.restrictViewingFutureProjects);
            ls.CheckboxLabeled("BRM_EnableTechAdvancement".Translate(), ref settings.enableTechAdvancement);
            ls.CheckboxLabeled("BRM_StartCollapsed".Translate(), ref settings.startCollapsed);
            ls.CheckboxLabeled("BRM_NeverCollapseFoundations".Translate(), ref settings.neverCollapseFoundations);

            ls.Gap();
            ls.CheckboxLabeled("BRM_CollapseOnCompletion".Translate(), ref settings.collapseOnCompletion);
            ls.CheckboxLabeled("BRM_AutoOpenMenuOnFinish".Translate(), ref settings.autoOpenMenuOnFinish);
            ls.CheckboxLabeled("BRM_ForbidVanillaMenu".Translate(), ref settings.forbidVanillaMenu);

            var maxNodesStr = settings.maxExpandedNodes <= 0 ? "BRM_Infinite".Translate().ToString() : settings.maxExpandedNodes.ToString();
            ls.Label("BRM_MaxExpandedNodes".Translate(maxNodesStr));
            settings.maxExpandedNodes = (int)ls.Slider(settings.maxExpandedNodes, -1, 100);

            string currentAdvancementLabel = settings.advancementTiedTo switch
            {
                AdvancementType.Foundations => "BRM_Foundations".Translate().ToString(),
                AdvancementType.EraCompletion => "BRM_EraCompletion".Translate().ToString(),
                _ => settings.advancementTiedTo.ToString()
            };

            if (ls.ButtonText("BRM_AdvancementTiedTo".Translate(currentAdvancementLabel)))
            {
                var list = new List<FloatMenuOption>
                {
                    new FloatMenuOption("BRM_Foundations".Translate(), () => settings.advancementTiedTo = AdvancementType.Foundations),
                    new FloatMenuOption("BRM_EraCompletion".Translate(), () => settings.advancementTiedTo = AdvancementType.EraCompletion)
                };
                Find.WindowStack.Add(new FloatMenu(list));
            }

            if (settings.advancementTiedTo == AdvancementType.EraCompletion)
            {
                ls.Label("BRM_EraCompletionPercentage".Translate(settings.eraCompletionPercentage.ToStringPercent()));
                settings.eraCompletionPercentage = ls.Slider(settings.eraCompletionPercentage, 0.1f, 1f);
            }

            ls.Gap();
            ls.CheckboxLabeled("BRM_RevealAllInGodMode".Translate(), ref settings.revealAllInGodMode);
            ls.CheckboxLabeled("BRM_EnableEmergence".Translate(), ref settings.enableEmergence);

            if (settings.enableEmergence)
            {
                ls.Label("BRM_EmergenceCostNeolithic".Translate(settings.emergenceCostNeolithic));
                settings.emergenceCostNeolithic = ls.Slider(settings.emergenceCostNeolithic, 100f, 10000f);
                ls.Label("BRM_EmergenceCostMedieval".Translate(settings.emergenceCostMedieval));
                settings.emergenceCostMedieval = ls.Slider(settings.emergenceCostMedieval, 100f, 10000f);
                ls.Label("BRM_EmergenceCostIndustrial".Translate(settings.emergenceCostIndustrial));
                settings.emergenceCostIndustrial = ls.Slider(settings.emergenceCostIndustrial, 100f, 10000f);
                ls.Label("BRM_EmergenceCostSpacer".Translate(settings.emergenceCostSpacer));
                settings.emergenceCostSpacer = ls.Slider(settings.emergenceCostSpacer, 100f, 10000f);
                ls.Label("BRM_EmergenceCostUltra".Translate(settings.emergenceCostUltra));
                settings.emergenceCostUltra = ls.Slider(settings.emergenceCostUltra, 100f, 10000f);
                ls.Label("BRM_EmergenceCostArchotech".Translate(settings.emergenceCostArchotech));
                settings.emergenceCostArchotech = ls.Slider(settings.emergenceCostArchotech, 100f, 10000f);
            }

            if (ModsConfig.IsActive("OskarPotocki.VFE.Tribals"))
            {
                ls.CheckboxLabeled("BRM_DisableVFETribalsAdvancement".Translate(), ref settings.disableVFETribalsAdvancement);
            }
            ls.End();
            Widgets.EndScrollView();
            scrollHeight = ls.curY - initY;
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            EmergenceGenerator.UpdateEmergenceNodes();
        }

        public override string SettingsCategory() => Content.Name;
    }
}
