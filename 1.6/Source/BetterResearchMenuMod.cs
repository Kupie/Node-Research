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

        public static BetterResearchMenuMod instance;
        public BetterResearchMenuMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<BetterResearchMenuSettings>();
            instance = this;
            new Harmony("BetterResearchMenuMod").PatchAll();
        }
        private Vector2 scrollPos;
        public override void DoSettingsWindowContents(Rect inRect)
        {
            float width = inRect.width - 18f;
            var height = CalculateHeight(width);
            var viewRect = new Rect(0f, 0f, width, height);

            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            var ls = new Listing_Standard();
            ls.Begin(viewRect);
            DrawSettings(ls);
            ls.End();
            Widgets.EndScrollView();
        }

        private float CalculateHeight(float width)
        {
            GUI.BeginGroup(new Rect(-9999f, -9999f, 1f, 1f));
            var ls = new Listing_Standard();
            ls.Begin(new Rect(0f, 0f, width, 99999f));
            DrawSettings(ls, true);
            float height = ls.CurHeight;
            ls.End();
            GUI.EndGroup();
            return height;
        }

        private void DrawSettings(Listing_Standard ls, bool dryRun = false)
        {
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

            if (ls.ButtonText("BRM_AdvancementTiedTo".Translate(currentAdvancementLabel)) && !dryRun)
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
                ls.Label("BRM_EmergenceCostNeolithic".Translate());
                string neolithicBuffer = settings.emergenceCostNeolithic.ToString();
                ls.TextFieldNumeric(ref settings.emergenceCostNeolithic, ref neolithicBuffer);

                ls.Label("BRM_EmergenceCostMedieval".Translate());
                string medievalBuffer = settings.emergenceCostMedieval.ToString();
                ls.TextFieldNumeric(ref settings.emergenceCostMedieval, ref medievalBuffer);

                ls.Label("BRM_EmergenceCostIndustrial".Translate());
                string industrialBuffer = settings.emergenceCostIndustrial.ToString();
                ls.TextFieldNumeric(ref settings.emergenceCostIndustrial, ref industrialBuffer);

                ls.Label("BRM_EmergenceCostSpacer".Translate());
                string spacerBuffer = settings.emergenceCostSpacer.ToString();
                ls.TextFieldNumeric(ref settings.emergenceCostSpacer, ref spacerBuffer);

                ls.Label("BRM_EmergenceCostUltra".Translate());
                string ultraBuffer = settings.emergenceCostUltra.ToString();
                ls.TextFieldNumeric(ref settings.emergenceCostUltra, ref ultraBuffer);

                ls.Label("BRM_EmergenceCostArchotech".Translate());
                string archotechBuffer = settings.emergenceCostArchotech.ToString();
                ls.TextFieldNumeric(ref settings.emergenceCostArchotech, ref archotechBuffer);
            }

            if (ModsConfig.IsActive("OskarPotocki.VFE.Tribals"))
            {
                ls.CheckboxLabeled("BRM_DisableVFETribalsAdvancement".Translate(), ref settings.disableVFETribalsAdvancement);
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Startup.UpdateEmergenceNodes();
        }

        public override string SettingsCategory() => Content.Name;
    }
}
