using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterResearchMenu
{
    public class ResearchNode
    {
        public ResearchProjectDef def;
        public Vector2 pos;
        public NodeState state;
        public bool isDragging;
    }

    public class ResearchEdge
    {
        public ResearchNode from;
        public ResearchNode to;
    }

    [HotSwappable]
    [StaticConstructorOnStartup]
    public class MainTabWindow_BetterResearch : MainTabWindow
    {
        public static Texture2D TexBubble = ContentFinder<Texture2D>.Get("UI/Bubble");
        public static Texture2D TexBarBg = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));
        public static Texture2D TexBarFill = SolidColorMaterials.NewSolidColorTexture(Color.green);
        public static Dictionary<TechLevel, Texture2D> TechLevelIcons = new Dictionary<TechLevel, Texture2D>
        {
            { TechLevel.Animal, ContentFinder<Texture2D>.Get("UI/TechLevels/Animal") },
            { TechLevel.Neolithic, ContentFinder<Texture2D>.Get("UI/TechLevels/Neolithic") },
            { TechLevel.Medieval, ContentFinder<Texture2D>.Get("UI/TechLevels/Medieval") },
            { TechLevel.Industrial, ContentFinder<Texture2D>.Get("UI/TechLevels/Industrial") },
            { TechLevel.Spacer, ContentFinder<Texture2D>.Get("UI/TechLevels/Spacer") },
            { TechLevel.Ultra, ContentFinder<Texture2D>.Get("UI/TechLevels/Ultra") },
            { TechLevel.Archotech, ContentFinder<Texture2D>.Get("UI/TechLevels/Archotech") }
        };

        private static List<TechLevel> AllTechLevels = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Where(tl => tl != TechLevel.Undefined).ToList();
        private float TopBarHeight => 50f;
        private float RightPanelWidth => 300f;
        private float NodeSizeExpanded => 80f;
        private float NodeSizeMinimized => 40f;
        private float NodeSizeDot => 20f;
        private static Color ColorBoxBackground => new ColorInt(38, 36, 36).ToColor;
        private static Color ColorGraphBackground => new ColorInt(30, 35, 41).ToColor;
        private static Color ColorEdgeFinished => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorEdgeUnfinished => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeDot => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeMinimized => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeFinished => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeBorder => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorBubbleProgress => new ColorInt(125, 183, 96).ToColor;
        private static Color ColorTechLevelTab => new ColorInt(66, 77, 89).ToColor;
        private static Color ColorTechLevelTabSelected => new ColorInt(101, 114, 130).ToColor;
        private static Color ColorLeftBarBackground => new ColorInt(73, 78, 96).ToColor;
        private static Color ColorRightPanelBackground => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorResearchingButton => new ColorInt(159, 125, 76).ToColor;
        private static Color ColorNodeUnfinished => Color.white;
        private static Color ColorTextQuestionMark => Color.white;
        private static Color ColorNodeIconTint => Color.white;
        private static Color ColorPanelIconTint => Color.white;
        private static Color ColorPanelButtonText => Color.white;
        private static Color ColorLocked => Color.red;
        private Vector2 cameraOffset;
        private float zoom = 1f;
        private ResearchTabDef currentTab = DefsOf.Main;
        private TechLevel currentEra = TechLevel.Animal;
        private List<ResearchNode> nodes = new List<ResearchNode>();
        private List<ResearchEdge> edges = new List<ResearchEdge>();
        private ResearchNode selectedNode;
        private Vector2 rightPanelScroll;
        private Rect graphRect;
        public override float Margin => 0f;
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, base.InitialSize.y);
        public override void PreOpen()
        {
            base.PreOpen();
            currentEra = Faction.OfPlayer.def.techLevel;
            cameraOffset = Vector2.zero;
            InitPhysics();
        }

        public void InitPhysics()
        {
            float techLevelSpacing = 350f;
            float randomOffset = 50f;

            nodes.Clear();
            edges.Clear();

            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.tab != currentTab) continue;
                if (def.techLevel != currentEra) continue;
                if (BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted) continue;

                var node = new ResearchNode { def = def };
                node.state = GetNodeState(def);

                if (State.nodePositions.TryGetValue(def.defName, out var savedPos))
                {
                    node.pos = savedPos;
                }
                else
                {
                    Vector2 avgPos = Vector2.zero;
                    int count = 0;
                    if (def.prerequisites != null)
                    {
                        foreach (var prereq in def.prerequisites)
                        {
                            if (State.nodePositions.TryGetValue(prereq.defName, out var pPos))
                            {
                                avgPos += pPos;
                                count++;
                            }
                        }
                    }
                    if (count > 0) avgPos /= count;
                    else avgPos = new Vector2(((int)def.techLevel - (int)currentEra) * techLevelSpacing, 0f);
                    node.pos = avgPos + new Vector2(Rand.Range(-randomOffset, randomOffset), Rand.Range(-randomOffset, randomOffset));
                    State.nodePositions[def.defName] = node.pos;
                }

                nodes.Add(node);
            }

            foreach (var node in nodes)
            {
                if (node.def.prerequisites != null)
                {
                    foreach (var prereq in node.def.prerequisites)
                    {
                        var parentNode = nodes.FirstOrDefault(n => n.def == prereq);
                        if (parentNode != null)
                            edges.Add(new ResearchEdge { from = parentNode, to = node });
                    }
                }
            }

            if (selectedNode == null && Find.ResearchManager.currentProj != null)
            {
                selectedNode = nodes.FirstOrDefault(n => n.def == Find.ResearchManager.currentProj);
            }

            if (selectedNode != null)
                cameraOffset = -selectedNode.pos;

            SimulateLayout();
        }

        private NodeState GetNodeState(ResearchProjectDef def)
        {
            if (BetterResearchMenuMod.settings.restrictViewingFutureTechLevels && def.techLevel > Faction.OfPlayer.def.techLevel)
                return NodeState.Hidden;

            if (State.nodeStates.TryGetValue(def.defName, out var state))
                return state;

            if (BetterResearchMenuMod.settings.restrictViewingFutureProjects)
            {
                if (def.IsFinished) return NodeState.Expanded;
                if (def.PrerequisitesCompleted) return NodeState.Dot;
                return NodeState.Hidden;
            }

            return NodeState.Expanded;
        }

        private void SimulateLayout()
        {
            var k = 160f;
            var temp = 100f;
            var minDistance = 0.1f;
            var maxRepulsionDistance = 500f;
            var dampingFactor = 0.5f;
            var minForceThreshold = 0.01f;
            var coolingFactor = 0.98f;
            var simulationIterations = 400;
            for (var i = 0; i < simulationIterations; i++)
            {
                foreach (var node in nodes)
                {
                    if (node.isDragging || node.state == NodeState.Hidden)
                        continue;
                    var force = Vector2.zero;

                    foreach (var other in nodes)
                    {
                        if (node == other || other.state == NodeState.Hidden)
                            continue;
                        var dir = node.pos - other.pos;
                        var dist = dir.magnitude;
                        if (dist < minDistance)
                        {
                            dir = Rand.InsideUnitCircle;
                            dist = minDistance;
                        }
                        if (dist < maxRepulsionDistance)
                            force += dir / dist * ((k * k) / dist);
                    }

                    foreach (var edge in edges)
                    {
                        if (edge.from != node && edge.to != node)
                            continue;
                        var other = edge.from == node ? edge.to : edge.from;
                        if (other.state == NodeState.Hidden)
                            continue;
                        var dir = other.pos - node.pos;
                        var dist = dir.magnitude;
                        if (dist > minDistance)
                            force += dir / dist * ((dist * dist) / k);
                    }

                    force.x += -node.pos.x * dampingFactor;
                    force.y += -node.pos.y * dampingFactor;

                    var mag = force.magnitude;
                    if (mag > minForceThreshold)
                        node.pos += force / mag * Mathf.Min(mag, temp);
                }
                temp *= coolingFactor;
            }

            foreach (var node in nodes)
                State.nodePositions[node.def.defName] = node.pos;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var bottomH = 100f;
            var thicknessFinished = 3f;
            var thicknessUnfinished = 2f;
            var borderExpansion = 5f;
            var iconPadding = 12f;
            var zoomThresholdTinyFont = 0.6f;
            var minRectXOffset = 28f;
            var minRectYOffset = -42f;
            var labelXOffset = 100f;
            var labelYOffset = 45f;

            Widgets.DrawBoxSolid(inRect, ColorGraphBackground);

            var panelWidth = selectedNode != null ? RightPanelWidth : 0f;
            graphRect = new Rect(0f, TopBarHeight, inRect.width - panelWidth, inRect.height - TopBarHeight - bottomH);

            HandleInputs();

            var mousePos = Event.current.mousePosition;
            var mouseGraphPos = ((mousePos - graphRect.center) / zoom) - cameraOffset;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && graphRect.Contains(mousePos))
            {
                for (var i = nodes.Count - 1; i >= 0; i--)
                {
                    var node = nodes[i];
                    if (node.state == NodeState.Hidden)
                        continue;

                    if (node.state == NodeState.Dot || node.state == NodeState.Minimized)
                    {
                        var s = node.state == NodeState.Dot ? NodeSizeDot : NodeSizeMinimized;
                        if (new Rect(node.pos.x - s / 2f, node.pos.y - s / 2f, s, s).Contains(mouseGraphPos))
                        {
                            node.state = NodeState.Expanded;
                            State.nodeStates[node.def.defName] = node.state;
                            Event.current.Use();
                            break;
                        }
                    }
                    else
                    {
                        var nodeRectHalfSize = NodeSizeExpanded / 2f;
                        var nodeRect = new Rect(node.pos.x - nodeRectHalfSize, node.pos.y - nodeRectHalfSize, NodeSizeExpanded, NodeSizeExpanded);
                        var minRectSize = 16f;
                        var minRect = new Rect(node.pos.x + minRectXOffset, node.pos.y + minRectYOffset, minRectSize, minRectSize);
                        if (minRect.Contains(mouseGraphPos))
                        {
                            node.state = NodeState.Minimized;
                            State.nodeStates[node.def.defName] = node.state;
                            Event.current.Use();
                            break;
                        }
                        if (nodeRect.Contains(mouseGraphPos))
                        {
                            selectedNode = node;
                            node.isDragging = true;
                            Event.current.Use();
                            break;
                        }
                    }
                }
            }

            if (Event.current.type == EventType.MouseUp)
            {
                foreach (var node in nodes)
                    node.isDragging = false;
            }

            foreach (var node in nodes)
            {
                if (node.isDragging)
                {
                    node.pos = mouseGraphPos;
                    State.nodePositions[node.def.defName] = node.pos;
                }
            }

            Widgets.DrawBoxSolid(graphRect, ColorGraphBackground);

            Widgets.BeginGroup(graphRect);

            var pivot = new Vector2(graphRect.width / 2f, graphRect.height / 2f);
            Vector2 WorldToScreen(Vector2 worldPos)
            {
                return (worldPos + cameraOffset) * zoom + pivot;
            }

            foreach (var edge in edges)
            {
                if (edge.from.state == NodeState.Hidden || edge.to.state == NodeState.Hidden)
                    continue;

                var color = edge.from.def.IsFinished ? ColorEdgeFinished : ColorEdgeUnfinished;

                Vector2 fromPos = WorldToScreen(edge.from.pos);
                Vector2 toPos = WorldToScreen(edge.to.pos);

                float thickness = (edge.from.def.IsFinished ? thicknessFinished : thicknessUnfinished) * zoom;
                Widgets.DrawLine(fromPos, toPos, color, thickness);
            }

            foreach (var node in nodes)
            {
                if (node.state == NodeState.Hidden)
                    continue;

                Vector2 screenPos = WorldToScreen(node.pos);

                if (node.state == NodeState.Dot || node.state == NodeState.Minimized)
                {
                    var size = (node.state == NodeState.Dot ? NodeSizeDot : NodeSizeMinimized) * zoom;
                    var buttonRect = new Rect(screenPos.x - size / 2f, screenPos.y - size / 2f, size, size);

                    GUI.color = node.state == NodeState.Dot ? ColorNodeDot : ColorNodeMinimized;
                    GUI.DrawTexture(buttonRect, TexBubble);
                    GUI.color = Color.white;

                    if (node.state == NodeState.Minimized)
                    {
                        GUI.color = ColorTextQuestionMark;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(buttonRect, "?");
                        GUI.color = Color.white;
                    }
                    continue;
                }

                var nodeSize = NodeSizeExpanded * zoom;
                var nodeRect = new Rect(screenPos.x - nodeSize / 2f, screenPos.y - nodeSize / 2f, nodeSize, nodeSize);

                GUI.color = node.def.IsFinished ? ColorNodeFinished : ColorNodeUnfinished;
                GUI.DrawTexture(nodeRect.ExpandedBy(borderExpansion * zoom), TexBubble);
                GUI.color = Color.white;

                GUI.color = ColorNodeBorder;
                GUI.DrawTexture(nodeRect, TexBubble);
                GUI.color = Color.white;

                if (node.def.ProgressPercent > 0f && !node.def.IsFinished)
                {
                    var progressPercent = node.def.ProgressPercent;
                    GUI.color = ColorBubbleProgress;
                    var fillRect = new Rect(nodeRect.x, nodeRect.y + nodeRect.height * (1f - progressPercent), nodeRect.width, nodeRect.height * progressPercent);
                    GUI.DrawTextureWithTexCoords(fillRect, TexBubble, new Rect(0f, 0f, 1f, progressPercent));
                    GUI.color = Color.white;
                }

                GUI.color = ColorNodeIconTint;
                var icon = GetIcon(node.def);
                if (icon != null)
                    GUI.DrawTexture(nodeRect.ContractedBy(iconPadding * zoom), icon);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.UpperCenter;

                Text.Font = zoom < zoomThresholdTinyFont ? GameFont.Tiny : GameFont.Small;

                Widgets.Label(new Rect(screenPos.x - labelXOffset, screenPos.y + (labelYOffset * zoom), labelXOffset * 2f, 30f), node.def.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndGroup();

            DrawTopBar(inRect);
            DrawLeftBar(inRect);
            DrawBottomBar(inRect);
            if (selectedNode != null)
                DrawRightPanel(inRect);
        }

        private void HandleInputs()
        {
            float moveSpeed = 20f;
            float zoomSensitivity = 0.05f;
            float minZoom = 0.2f;
            float maxZoom = 3f;
            int dragMouseButton = 2;

            var cameraDelta = Vector2.zero;
            if (Input.GetKey(KeyCode.W))
                cameraDelta.y += moveSpeed / zoom;
            if (Input.GetKey(KeyCode.S))
                cameraDelta.y -= moveSpeed / zoom;
            if (Input.GetKey(KeyCode.A))
                cameraDelta.x += moveSpeed / zoom;
            if (Input.GetKey(KeyCode.D))
                cameraDelta.x -= moveSpeed / zoom;
            cameraOffset += cameraDelta;

            if (Event.current.type == EventType.ScrollWheel)
            {
                zoom -= Event.current.delta.y * zoomSensitivity;
                zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseDrag && Event.current.button == dragMouseButton)
            {
                cameraOffset += Event.current.delta / zoom;
                Event.current.Use();
            }
        }

        private void DrawTopBar(Rect inRect)
        {
            float padding = 5f;
            float iconSize = 40f;
            float iconMargin = 10f;
            float textOffset = 60f;

            var topRect = new Rect(0f, 0f, inRect.width, TopBarHeight);
            Widgets.DrawBoxSolid(topRect, ColorBoxBackground);
            var segWidth = inRect.width / AllTechLevels.Count;
            for (int i = 0; i < AllTechLevels.Count; i++)
            {
                var techLevel = AllTechLevels[i];
                var segRect = new Rect(i * segWidth, 0f, segWidth, TopBarHeight);
                if (techLevel == currentEra)
                {
                    Widgets.DrawBoxSolid(segRect, ColorTechLevelTabSelected);
                }
                else
                {
                    Widgets.DrawBoxSolid(segRect, ColorTechLevelTab);
                }
                var allProjectsInEra = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => (x.techLevel == techLevel || x.techLevel == TechLevel.Undefined && techLevel == Faction.OfPlayer.def.techLevel) && x.tab == currentTab).ToList();
                var finishedInEra = allProjectsInEra.Count(x => x.IsFinished);
                var innerRect = segRect.ContractedBy(padding);
                if (TechLevelIcons.TryGetValue(techLevel, out var icon))
                    GUI.DrawTexture(new Rect(innerRect.x + iconMargin, innerRect.y, iconSize, iconSize), icon);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (Widgets.ButtonInvisible(segRect))
                {
                    if (techLevel != currentEra)
                    {
                        currentEra = techLevel;
                        cameraOffset = Vector2.zero;
                        zoom = 1f;
                        InitPhysics();
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    }
                }
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(innerRect.x + textOffset, innerRect.y, innerRect.width - textOffset, innerRect.height / 2f), techLevel.ToStringHuman().CapitalizeFirst());
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(innerRect.x + textOffset, innerRect.y + innerRect.height / 2f, innerRect.width - textOffset, innerRect.height / 2f), $"{finishedInEra}/{allProjectsInEra.Count}");
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawLeftBar(Rect inRect)
        {
            if (BetterResearchMenuMod.settings.enableTechAdvancement)
            {
                float leftBarY = 60f;
                float leftBarWidth = 50f;
                float leftBarHeightOffset = 160f;
                float iconSize = 40f;
                float iconMargin = 5f;
                float labelWidth = 200f;
                float labelHeight = 50f;

                var foundations = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => x.techLevel == currentEra && x.tab == currentTab && x.HasModExtension<ResearchFoundationExtension>()).ToList();
                var total = foundations.Count;
                if (total == 0)
                    return;

                var leftRect = new Rect(0f, leftBarY, leftBarWidth, inRect.height - leftBarHeightOffset);
                Widgets.DrawBoxSolid(leftRect, ColorLeftBarBackground);
                var finished = foundations.Count(x => x.IsFinished);
                var progress = total > 0 ? (float)finished / total : 0f;

                var nextEra = (TechLevel)((int)currentEra + 1);
                if (TechLevelIcons.TryGetValue(nextEra, out var nextIcon))
                {
                    var nextIconRect = new Rect(leftRect.center.x - iconSize / 2f, leftRect.y + iconMargin, iconSize, iconSize);
                    GUI.DrawTexture(nextIconRect, nextIcon);
                }

                if (TechLevelIcons.TryGetValue(currentEra, out var currentIcon))
                {
                    var currentIconRect = new Rect(leftRect.center.x - iconSize / 2f, leftRect.yMax - iconSize - iconMargin, iconSize, iconSize);
                    GUI.DrawTexture(currentIconRect, currentIcon);
                }

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(leftRect.xMax, leftRect.y, labelWidth, labelHeight), "BRM_FoundationsComplete".Translate(finished, total));
            }
        }

        private void DrawRightPanel(Rect inRect)
        {
            float panelHeightOffset = 150f;
            float btnMargin = 5f;
            float btnSize = 24f;
            float iconMargin = 20f;
            float iconTopOffset = 40f;
            float iconSize = 60f;
            float iconBorderExpansion = 3f;
            float iconPadding = 8f;
            float titleOffset = 15f;
            float titlePadding = 40f;
            float sectionSpacing = 20f;
            float labelHeight = 24f;
            float scrollOffset = 30f;
            float scrollBottomMargin = 70f;
            float scrollbarWidth = 16f;
            float listItemHeight = 28f;
            float actionBtnBottomMargin = 60f;
            float actionBtnHeight = 40f;

            var panelRect = new Rect(inRect.width - RightPanelWidth, TopBarHeight, RightPanelWidth, inRect.height - panelHeightOffset);
            Widgets.DrawBoxSolid(panelRect, ColorRightPanelBackground);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(new Rect(panelRect.x + btnMargin, panelRect.y + btnMargin, btnSize, btnSize), "<", drawBackground: false, textColor: ColorPanelButtonText, doMouseoverSound: true))
            {
                selectedNode = null;
            }
            if (Widgets.ButtonText(new Rect(panelRect.xMax - btnSize - 5, panelRect.y + btnMargin, btnSize, btnSize), "—", drawBackground: false, textColor: ColorPanelButtonText, doMouseoverSound: true))
            {
                selectedNode.state = NodeState.Minimized;
                State.nodeStates[selectedNode.def.defName] = NodeState.Minimized;
                selectedNode = null;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (selectedNode is null) return;
            var proj = selectedNode.def;
            var iconRect = new Rect(panelRect.x + iconMargin, panelRect.y + iconTopOffset, iconSize, iconSize);
            GUI.color = ColorPanelIconTint;
            GUI.DrawTexture(iconRect.ExpandedBy(iconBorderExpansion), TexBubble);
            GUI.color = ColorNodeBorder;
            GUI.DrawTexture(iconRect, TexBubble);
            GUI.color = ColorPanelIconTint;
            var icon = GetIcon(proj);
            if (icon != null)
                GUI.DrawTexture(iconRect.ContractedBy(iconPadding), icon);
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(iconRect.xMax + titleOffset, iconRect.y, panelRect.width - iconRect.width - titlePadding, iconRect.height), proj.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            var curY = iconRect.yMax + sectionSpacing;
            Widgets.Label(new Rect(panelRect.x + iconMargin, curY, panelRect.width - titlePadding, labelHeight), "BRM_Unlocks".Translate());
            curY += scrollOffset;
            var outRect = new Rect(panelRect.x + iconMargin, curY, panelRect.width - titlePadding, panelRect.height - curY - scrollBottomMargin);
            var viewRect = new Rect(0f, 0f, outRect.width - scrollbarWidth, proj.UnlockedDefs.Count * listItemHeight);
            Widgets.BeginScrollView(outRect, ref rightPanelScroll, viewRect);
            var listY = 0f;
            foreach (var unlock in proj.UnlockedDefs)
            {
                Widgets.DefLabelWithIcon(new Rect(0f, listY, viewRect.width, labelHeight), unlock);
                listY += listItemHeight;
            }
            Widgets.EndScrollView();
            var btnRect = new Rect(panelRect.x + iconMargin, panelRect.yMax - actionBtnBottomMargin, panelRect.width - titlePadding, actionBtnHeight);
            if (proj == Find.ResearchManager.currentProj)
            {
                Widgets.DrawBoxSolid(btnRect, ColorResearchingButton);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(btnRect, "BRM_Researching".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else if (proj.CanStartNow)
            {
                if (Widgets.ButtonText(btnRect, "Research".Translate()))
                {
                    SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                    Find.ResearchManager.SetCurrentProject(proj);
                }
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(btnRect, "Locked".Translate().Colorize(ColorLocked));
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawResearchTabs(Rect rect)
        {
            float tabWidth = 120f;
            float tabHeight = 30f;
            float tabsMargin = 10f;
            float tabsYOffset = 5f;

            var hasAnomaly = DefsOf.Anomaly != null;
            var hasGravtech = DefsOf.VGE_Gravtech != null;
            var tabCount = 1 + (hasAnomaly ? 1 : 0) + (hasGravtech ? 1 : 0);
            var tabsWidth = tabWidth * tabCount;
            var tabsRect = new Rect(rect.xMax - tabsWidth - tabsMargin, rect.y + tabsYOffset, tabsWidth, tabHeight);
            var currX = tabsRect.x;
            if (Widgets.ButtonText(new Rect(currX, tabsRect.y, tabWidth, tabsRect.height), "BRM_StandardStudy".Translate())) SetType(DefsOf.Main);
            currX += tabWidth;
            if (hasAnomaly)
            {
                if (Widgets.ButtonText(new Rect(currX, tabsRect.y, tabWidth, tabsRect.height), "BRM_DarkStudy".Translate())) SetType(DefsOf.Anomaly);
                currX += tabWidth;
            }
            if (hasGravtech)
            {
                if (Widgets.ButtonText(new Rect(currX, tabsRect.y, tabWidth, tabsRect.height), "BRM_GravStudy".Translate())) SetType(DefsOf.VGE_Gravtech);
            }
        }

        private void DrawBottomBar(Rect inRect)
        {
            float bottomBarHeight = 80f;
            float iconMargin = 20f;
            float iconYOffset = 10f;
            float iconSize = 60f;
            float iconExpansion = 4f;
            float iconPadding = 8f;
            float labelXOffset = 100f;
            float labelWidth = 400f;
            float labelHeight = 30f;
            float barYOffset = 40f;
            float barWidthReduction = 120f;
            float barHeight = 24f;

            var bottomRect = new Rect(0f, inRect.height - bottomBarHeight, inRect.width, bottomBarHeight);
            Widgets.DrawBoxSolid(bottomRect, ColorBoxBackground);
            var proj = Find.ResearchManager.currentProj;
            if (proj != null)
            {
                GUI.color = ColorPanelIconTint;
                GUI.DrawTexture(new Rect(iconMargin, bottomRect.y + iconYOffset, iconSize, iconSize).ExpandedBy(iconExpansion), TexBubble);
                GUI.color = ColorNodeBorder;
                GUI.DrawTexture(new Rect(iconMargin, bottomRect.y + iconYOffset, iconSize, iconSize), TexBubble);
                GUI.color = ColorPanelIconTint;
                var icon = GetIcon(proj);
                if (icon != null)
                    GUI.DrawTexture(new Rect(iconMargin, bottomRect.y + iconYOffset, iconSize, iconSize).ContractedBy(iconPadding), icon);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(labelXOffset, bottomRect.y + iconYOffset, labelWidth, labelHeight), "BRM_CurrentlyResearching".Translate(proj.LabelCap));
                DrawResearchTabs(bottomRect);
                var progRect = new Rect(labelXOffset, bottomRect.y + barYOffset, bottomRect.width - barWidthReduction, barHeight);
                Widgets.FillableBar(progRect, proj.ProgressPercent, TexBarFill, TexBarBg, true);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(progRect, $"{proj.ProgressApparent:F0} / {proj.CostApparent:F0}");
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void SetType(ResearchTabDef newTab)
        {
            if (currentTab == newTab) return;
            currentTab = newTab;
            currentEra = Faction.OfPlayer.def.techLevel;
            cameraOffset = Vector2.zero;
            zoom = 1f;
            InitPhysics();
        }

        private Texture2D GetIcon(ResearchProjectDef def)
        {
            var ext = def.GetModExtension<ResearchIconExtension>();
            if (ext != null && !string.IsNullOrEmpty(ext.texPath))
            {
                var tex = ContentFinder<Texture2D>.Get(ext.texPath, false);
                if (tex != null) return tex;
            }

            foreach (var unlock in def.UnlockedDefs)
            {
                if (unlock is ThingDef td && td.uiIcon != null && td.uiIcon != BaseContent.BadTex)
                    return td.uiIcon;
                if (unlock is RecipeDef rd && rd.UIIconThing?.uiIcon != null && rd.UIIconThing.uiIcon != BaseContent.BadTex)
                    return rd.UIIconThing.uiIcon;
            }
            return null;
        }
    }
}
