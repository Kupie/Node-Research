using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterResearchMenu
{
    [HotSwappable]
    public class Window_TechAdvance : Window
    {
        public TechLevel newLevel;
        private float scrollPositionX = 0f;
        private List<TechLevel> levels;
        private int targetIndex;

        public Window_TechAdvance(TechLevel level)
        {
            newLevel = level;
            levels = MainTabWindow_BetterResearch.AllTechLevels;
            targetIndex = levels.IndexOf(level);
            scrollPositionX = (targetIndex - 1) * 200f;
            doCloseX = false;
            forcePause = true;
            closeOnClickedOutside = false;
            doWindowBackground = false;
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);
        public override void SetInitialSizeAndPosition() => windowRect = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
        public override float Margin => 0f;
        public override void DoWindowContents(Rect inRect)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(inRect, BaseContent.WhiteTex);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            Widgets.DrawBox(inRect);
            GUI.color = Color.white;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0, inRect.height / 2f - 220f, inRect.width, 60f), "BRM_AdvancedTo".Translate(newLevel.ToStringHuman().CapitalizeFirst()));
            Text.Font = GameFont.Small;

            scrollPositionX = Mathf.Lerp(scrollPositionX, targetIndex * 200f, Time.deltaTime * 5f);
            float centerX = inRect.width / 2f;
            float centerY = inRect.height / 2f - 50f;

            for (int i = 0; i < levels.Count; i++)
            {
                float xPos = centerX + (i * 200f) - scrollPositionX - 100f;
                var distance = Mathf.Abs(centerX - (xPos + 100f));
                var scale = Mathf.Clamp(1f - (distance / 500f), 0.5f, 1f);
                var alpha = Mathf.Clamp(1f - (distance / 600f), 0.2f, 1f);

                if (MainTabWindow_BetterResearch.TechLevelIcons.TryGetValue(levels[i], out var icon))
                {
                    var size = 250f * scale;
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.DrawTexture(new Rect(xPos + (200f - size) / 2f, centerY - size / 2f, size, size), icon);
                    GUI.color = Color.white;
                }
            }

            var unlockedCount = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count(x => x.techLevel == newLevel);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, inRect.height / 2f + 100f, inRect.width, 40f), "BRM_ProjectsUnlocked".Translate(unlockedCount));
            Widgets.Label(new Rect(0, inRect.height / 2f + 140f, inRect.width, 40f), "BRM_ColonyIsNowTech".Translate(newLevel.ToStringHuman().CapitalizeFirst()));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 125f, inRect.height / 2f + 220f, 250f, 50f), "BRM_Continue".Translate()))
            {
                Close();
            }
        }
    }
}
