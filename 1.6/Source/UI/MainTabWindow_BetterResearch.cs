using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BetterResearchMenu
{
    [HotSwappable]
    public class ResearchNode
    {
        public ResearchProjectDef def;
        public Vector2 pos;
        public Vector2 dampVelocity;
        public Vector2 drawPos;
        public Vector2 velocity;
        public NodeState state;
        public bool isDragging;
        public float RadiusMultiplier => def.HasModExtension<ResearchFoundationExtension>() ? 1.5f : 1.0f;
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
        public static Texture2D TexGreenBubble = ContentFinder<Texture2D>.Get("UI/GreenBubble");
        public static Texture2D TexOrangeBubble = ContentFinder<Texture2D>.Get("UI/OrangeBubble");
        public static Texture2D TexBarBg => SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));
        public static Texture2D TexBarFill => SolidColorMaterials.NewSolidColorTexture(ColorBubbleProgress);
        public static Texture2D TexCenter = ContentFinder<Texture2D>.Get("UI/CenterSlider");
        public static Texture2D TexSpacing = ContentFinder<Texture2D>.Get("UI/SpacingSlider");
        public static Texture2D TexContracting = ContentFinder<Texture2D>.Get("UI/ContractingSlider");
        public static Texture2D TexLock = ContentFinder<Texture2D>.Get("UI/Lock");
        private static Texture2D texGradient;
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

        static MainTabWindow_BetterResearch()
        {
            texGradient = new Texture2D(1, 2);
            texGradient.SetPixels(new[] { Color.black, Color.white });
            texGradient.Apply();

            var type = typeof(MainTabWindow_Research);
            selectedProjectField = AccessTools.Field(type, "selectedProject");
            curTabIntField = AccessTools.Field(type, "curTabInt");
            lockedReasonsField = AccessTools.Field(type, "lockedReasons");
            drawStartButtonMethod = AccessTools.Method(type, "DrawStartButton");
        }

        public static List<TechLevel> AllTechLevels = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Where(tl => tl != TechLevel.Undefined).ToList();
        private float TopBarHeight => currentTab == DefsOf.Main ? 45f : 0f;
        private float BottomBarHeight => Find.ResearchManager.currentProj != null ? 80f : 40f;
        private float RightPanelWidth => 300f;
        private float NodeSizeExpanded => 80f;
        private float NodeSizeMinimized => 40f;
        private float NodeSizeDot => 20f;
        private static Color ColorBoxBackground => new ColorInt(38, 36, 36).ToColor;
        private static Color ColorGraphBackground => new ColorInt(15, 20, 26).ToColor;
        private static Color ColorAnomalyBackground => new ColorInt(45, 25, 25).ToColor;
        private static Color ColorVGEBackground => new ColorInt(25, 45, 45).ToColor;
        private static Color ColorEdgeFinished => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorEdgeUnfinished => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeDot => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeMinimized => new ColorInt(95, 99, 102).ToColor;
        private static Color ColorBubbleProgress => new ColorInt(125, 183, 96).ToColor;
        private static Color ColorTechLevelTab => new ColorInt(29, 34, 38).ToColor;
        private static Color ColorTechLevelTabSelected => new ColorInt(38, 43, 47).ToColor;
        private static Color ColorLeftBarBackground => new ColorInt(73, 78, 96).ToColor;
        private static Color ColorResearchingButton => new ColorInt(159, 125, 76).ToColor;
        private static Color ColorLocked => Color.red;
        private float ThicknessFinished => 3f;
        private float ThicknessUnfinished => 2f;
        private float IconPadding => 12f;
        private float physicsTemperature = 0f;
        private bool isPanning;
        private Vector2 cameraOffset;
        private float zoom = 1f;
        private int lastStateCheckHash = -1;
        private ResearchTabDef currentTab = DefsOf.Main;
        private TechLevel currentEra = TechLevel.Animal;
        private List<ResearchNode> nodes = new List<ResearchNode>();
        private List<ResearchEdge> edges = new List<ResearchEdge>();
        private ResearchNode selectedNode;
        private Vector2 dragOffset;
        private Vector2 rightPanelScroll;
        private Vector2 dragStartMousePos;
        private bool wasDraggingNode;
        private static MainTabWindow_Research vanillaResearchWindow;
        private static FieldInfo selectedProjectField;
        private static FieldInfo curTabIntField;
        private static FieldInfo lockedReasonsField;
        private static MethodInfo drawStartButtonMethod;
        private Rect graphRect;
        private QuickSearchWidget quickSearchWidget = new QuickSearchWidget();
        private float prevPanelWidth = 0f;
        public override float Margin => 0f;
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, base.InitialSize.y);
        private bool LeftBarVisible =>
            currentTab == DefsOf.Main &&
            BetterResearchMenuMod.settings.enableTechAdvancement &&
            DefDatabase<ResearchProjectDef>.AllDefs.Any(x => x.techLevel == currentEra && x.tab == currentTab && x.HasModExtension<ResearchFoundationExtension>());
        public override void PreOpen()
        {
            base.PreOpen();
            quickSearchWidget.Reset();
            currentEra = Faction.OfPlayer.def.techLevel;
            cameraOffset = Vector2.zero;
            InitPhysics(true);
        }

        public void InitPhysics(bool instant = false)
        {
            ResearchProjectDef previouslySelectedDef = selectedNode?.def;

            float techLevelSpacing = 350f;
            float randomOffset = 50f;

            nodes.Clear();
            edges.Clear();

            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.tab != currentTab) continue;
                if (currentTab == DefsOf.Main && def.techLevel != currentEra) continue;
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
                node.drawPos = node.pos;

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

            if (previouslySelectedDef != null)
            {
                selectedNode = nodes.FirstOrDefault(n => n.def == previouslySelectedDef);
            }

            if (selectedNode == null && Find.ResearchManager.currentProj != null)
            {
                selectedNode = nodes.FirstOrDefault(n => n.def == Find.ResearchManager.currentProj);
            }

            if (instant)
            {
                if (selectedNode != null)
                    cameraOffset = -selectedNode.pos;
                InitPhysicsLayout();
            }
        }

        private NodeState GetNodeState(ResearchProjectDef def)
        {
            if (BetterResearchMenuMod.settings.restrictViewingFutureTechLevels && def.techLevel > Faction.OfPlayer.def.techLevel)
                return NodeState.Hidden;

            if (BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted)
                return NodeState.Hidden;

            if (State.nodeStates.TryGetValue(def.defName, out var state))
                return state;

            if (def.IsFinished) return NodeState.Expanded;
            if (def.prerequisites.NullOrEmpty()) return NodeState.Minimized;
            if (def.PrerequisitesCompleted) return NodeState.Dot;

            return NodeState.Expanded;
        }

        private void InitPhysicsLayout()
        {
            physicsTemperature = 100f;
            for (var i = 0; i < 500; i++)
            {
                PhysicsTick(0.016f);
            }
            foreach (var node in nodes)
            {
                node.drawPos = node.pos;
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            int currentStateHash = 0;
            var allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                if (allDefs[i].IsFinished) currentStateHash++;
                if (allDefs[i].PrerequisitesCompleted) currentStateHash++;
            }

            if (lastStateCheckHash != -1 && currentStateHash != lastStateCheckHash)
            {
                InitPhysics(false);
                physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            }
            lastStateCheckHash = currentStateHash;

            PhysicsTick(0.02f);

            foreach (var node in nodes)
            {
                if (node.isDragging)
                {
                    node.drawPos = node.pos;
                    node.dampVelocity = Vector2.zero;
                }
                else
                {
                    node.drawPos = Vector2.SmoothDamp(node.drawPos, node.pos, ref node.dampVelocity, 0.05f);
                }
            }

            if (physicsTemperature <= 0.1f)
            {
                foreach (var node in nodes)
                    node.drawPos = node.pos;
            }
        }

        private void PhysicsTick(float dt)
        {
            if (physicsTemperature < 0.1f) { physicsTemperature = 0f; velocitySum = 0f; return; }

            var k = 180f;
            var minDistance = 15f;
            var maxRepulsionDistance = 500f;
            var dampingFactor = 0.9f;
            var clingMultiplier = 20f;

            velocitySum = 0f;
            foreach (var node in nodes)
            {
                if (node.isDragging || node.state == NodeState.Hidden)
                {
                    node.velocity = Vector2.zero;
                    continue;
                }

                var force = Vector2.zero;

                foreach (var other in nodes)
                {
                    if (node == other || other.state == NodeState.Hidden) continue;
                    var dir = node.pos - other.pos;
                    var dist = dir.magnitude;
                    if (dist < minDistance) { dir = Rand.UnitVector2; dist = minDistance; }

                    if (dist < maxRepulsionDistance)
                    {
                        float effectiveK = k * ((node.RadiusMultiplier + other.RadiusMultiplier) * 0.5f);
                        force += dir / dist * (effectiveK * effectiveK / dist) * BetterResearchMenuMod.settings.spacingForceMultiplier;
                    }
                }

                foreach (var edge in edges)
                {
                    if (edge.from != node && edge.to != node) continue;
                    var other = edge.from == node ? edge.to : edge.from;
                    if (other.state == NodeState.Hidden) continue;
                    var dir = other.pos - node.pos;
                    var dist = dir.magnitude;

                    float adjustedK = k * ((node.RadiusMultiplier + other.RadiusMultiplier) * 0.5f);

                    if (dist > 5f)
                    {
                        var strength = BetterResearchMenuMod.settings.contractingForceMultiplier;
                        if (node.state == NodeState.Dot || node.state == NodeState.Minimized)
                            strength *= clingMultiplier;

                        force += dir / dist * (dist * dist / adjustedK) * strength;
                    }
                }

                float centerForce = 0.02f * BetterResearchMenuMod.settings.centerForceMultiplier;
                force -= node.pos * centerForce;

                node.velocity = (node.velocity + force * dt) * dampingFactor;
                velocitySum += node.velocity.sqrMagnitude;
            }

            if (velocitySum < 0.5f) { physicsTemperature = 0f; return; }

            foreach (var node in nodes)
            {
                if (node.isDragging || node.state == NodeState.Hidden) continue;

                var move = node.velocity * dt * 2.5f;

                float maxMove = physicsTemperature * 0.2f;
                if (move.magnitude > maxMove) move = move.normalized * maxMove;

                node.pos += move;
                State.nodePositions[node.def.defName] = node.pos;
            }

            physicsTemperature *= 0.98f;
        }

        private float velocitySum = 0f;

        public override void DoWindowContents(Rect inRect)
        {

            var targetBgColor = currentTab == DefsOf.Anomaly ? ColorAnomalyBackground : currentTab == DefsOf.VGE_Gravtech ? ColorVGEBackground : ColorGraphBackground;
            GUI.color = targetBgColor;
            GUI.DrawTexture(inRect, texGradient);
            GUI.color = Color.white;

            var panelWidth = selectedNode != null ? RightPanelWidth : 0f;
            if (prevPanelWidth != panelWidth)
            {
                cameraOffset.x += (panelWidth - prevPanelWidth) / 2f / zoom;
                prevPanelWidth = panelWidth;
            }
            graphRect = new Rect(0f, TopBarHeight, inRect.width - panelWidth, inRect.height - TopBarHeight - BottomBarHeight);

            float leftBarShift = LeftBarVisible ? 55f : 5f;

            var controlAreaRect = new Rect(leftBarShift + 30f, graphRect.height - 95f, 220f, 90f);
            float searchBarWidth = 200f;
            float searchBarHeight = 24f;
            var searchBarRect = new Rect(graphRect.width - searchBarWidth - 4f, 4f, searchBarWidth, searchBarHeight);
            var pivot = new Vector2(graphRect.width / 2f, graphRect.height / 2f);

            var panelRect = new Rect(inRect.width - RightPanelWidth, TopBarHeight, RightPanelWidth, graphRect.height);

            HandleInputs(graphRect, controlAreaRect, panelRect, searchBarRect);

            var mousePos = Event.current.mousePosition;
            var localMousePos = mousePos - new Vector2(graphRect.x, graphRect.y);

            Rect interactionExclusion = controlAreaRect;
            interactionExclusion.x -= 30f;

            bool mouseInPanel = selectedNode != null && panelRect.Contains(Event.current.mousePosition);

            if (!mouseInPanel && !interactionExclusion.Contains(localMousePos) && !searchBarRect.Contains(localMousePos) && Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1 || Event.current.button == 2) && graphRect.Contains(mousePos))
            {
                bool nodeClicked = false;

                if (Event.current.button == 0 || Event.current.button == 1)
                {
                    for (var i = nodes.Count - 1; i >= 0; i--)
                    {
                        var node = nodes[i];
                        if (node.state == NodeState.Hidden)
                            continue;

                        var screenPos = (node.drawPos + cameraOffset) * zoom + pivot;

                        var isFoundation = node.def.HasModExtension<ResearchFoundationExtension>();
                        var nodeSize = (node.state == NodeState.Dot || node.state == NodeState.Minimized) ? NodeSizeMinimized : (isFoundation ? NodeSizeExpanded * 2f : NodeSizeExpanded);
                        nodeSize *= zoom;

                        if (Vector2.Distance(screenPos, localMousePos) < nodeSize / 2f)
                        {
                            if (Event.current.button == 0)
                            {
                                selectedNode = node;
                                node.isDragging = true;
                                wasDraggingNode = true;
                                dragStartMousePos = localMousePos;

                                var worldMousePos = ((localMousePos - pivot) / zoom) - cameraOffset;
                                dragOffset = node.pos - worldMousePos;
                            }
                            else if (Event.current.button == 1)
                            {
                                node.state = NodeState.Minimized;
                                State.nodeStates[node.def.defName] = node.state;
                                if (selectedNode == node) selectedNode = null;
                                physicsTemperature = Mathf.Max(physicsTemperature, 20f);
                                DefsOf.BRM_CollapsingNode.PlayOneShotOnCamera();
                            }
                            nodeClicked = true;
                            Event.current.Use();
                            break;
                        }
                    }
                }

                if (!nodeClicked && Event.current.button != 1)
                {
                    isPanning = true;
                    Event.current.Use();
                }
            }

            if (Event.current.rawType == EventType.MouseUp)
            {
                isPanning = false;
                if (Event.current.button == 0 && wasDraggingNode)
                {
                    if (selectedNode != null && Vector2.Distance(localMousePos, dragStartMousePos) < 5f)
                    {
                        if (selectedNode.state == NodeState.Minimized || selectedNode.state == NodeState.Dot)
                        {
                            if (!selectedNode.def.CanStartNow && !selectedNode.def.IsFinished)
                            {
                                Messages.Message(GetLockedReason(selectedNode.def), MessageTypeDefOf.RejectInput, false);
                            }
                            else
                            {
                                selectedNode.state = NodeState.Expanded;
                                State.nodeStates[selectedNode.def.defName] = selectedNode.state;
                                physicsTemperature = Mathf.Max(physicsTemperature, 20f);
                                DefsOf.BRM_ExpandingNode.PlayOneShotOnCamera();
                            }
                        }
                    }
                }
                wasDraggingNode = false;
                foreach (var node in nodes)
                    node.isDragging = false;
            }

            foreach (var node in nodes)
            {
                if (node.isDragging && Event.current.type == EventType.MouseDrag)
                {
                    node.pos = ((localMousePos - pivot) / zoom) - cameraOffset + dragOffset;
                    node.velocity = Vector2.zero;
                    node.dampVelocity = Vector2.zero;
                    State.nodePositions[node.def.defName] = node.pos;
                    physicsTemperature = Mathf.Max(physicsTemperature, 20f);
                }
            }

            Widgets.BeginGroup(graphRect);
            DrawGraphControls(controlAreaRect);
            quickSearchWidget.OnGUI(searchBarRect);

            Vector2 WorldToScreen(Vector2 worldPos)
            {
                return (worldPos + cameraOffset) * zoom + pivot;
            }

            foreach (var edge in edges)
            {
                if (edge.from.state == NodeState.Hidden || edge.to.state == NodeState.Hidden)
                    continue;
                if (!NodeMatchesSearch(edge.from) || !NodeMatchesSearch(edge.to)) continue;

                var color = edge.from.def.IsFinished ? ColorEdgeFinished : ColorEdgeUnfinished;

                Vector2 fromPos = WorldToScreen(edge.from.drawPos);
                Vector2 toPos = WorldToScreen(edge.to.drawPos);

                float thickness = (edge.from.def.IsFinished ? ThicknessFinished : ThicknessUnfinished) * zoom;
                Widgets.DrawLine(fromPos, toPos, color, thickness);
            }

            foreach (var node in nodes)
            {
                if (node.state == NodeState.Hidden)
                    continue;
                if (!NodeMatchesSearch(node)) continue;
                Vector2 screenPos = WorldToScreen(node.drawPos);
                bool isFoundation = node.def.HasModExtension<ResearchFoundationExtension>();

                if (node.state == NodeState.Dot || node.state == NodeState.Minimized)
                {
                    var hitSize = NodeSizeMinimized * zoom;
                    var isHovering = Vector2.Distance(screenPos, localMousePos) < hitSize / 2f;
                    var drawState = (node.state == NodeState.Minimized || isHovering) ? NodeState.Minimized : NodeState.Dot;

                    var size = (drawState == NodeState.Minimized ? NodeSizeMinimized : NodeSizeDot) * zoom;
                    var buttonRect = new Rect(screenPos.x - size / 2f, screenPos.y - size / 2f, size, size);

                    if (node.def.IsFinished)
                    {
                        GUI.color = Color.green;
                        GUI.DrawTexture(buttonRect, TexGreenBubble);
                        GUI.color = Color.white;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Text.Font = zoom < 0.6f ? GameFont.Tiny : GameFont.Medium;
                        Widgets.Label(buttonRect, "✓");
                    }
                    else
                    {
                        GUI.color = drawState == NodeState.Minimized ? ColorNodeMinimized : ColorNodeDot;
                        GUI.DrawTexture(buttonRect, TexBubble);
                        GUI.color = Color.white;

                        if (drawState == NodeState.Minimized)
                        {
                            if (!node.def.CanStartNow && !node.def.IsFinished)
                            {
                                GUI.DrawTexture(buttonRect.ContractedBy(4f * zoom), TexLock);
                            }
                            else
                            {
                                Text.Anchor = TextAnchor.MiddleCenter;
                                Text.Font = zoom < 0.6f ? GameFont.Tiny : GameFont.Medium;
                                Widgets.Label(buttonRect, "?");
                            }
                        }
                    }
                    Text.Anchor = TextAnchor.UpperLeft;
                    continue;
                }

                var nodeSize = (isFoundation ? NodeSizeExpanded * 2f : NodeSizeExpanded) * zoom;
                var nodeRect = new Rect(screenPos.x - nodeSize / 2f, screenPos.y - nodeSize / 2f, nodeSize, nodeSize);

                if (node == selectedNode)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(nodeRect.ExpandedBy(10f * zoom), TexBubble);
                    GUI.color = Color.white;
                }
                var padding = isFoundation ? IconPadding * 2f : IconPadding;
                DrawBubble(nodeRect, node.def, padding * zoom, drawSilhouette: true);

                if (zoom > 0.4f)
                {
                    var oldMatrix = GUI.matrix;

                    GUIUtility.ScaleAroundPivot(new Vector2(zoom, zoom), screenPos);

                    Text.Anchor = TextAnchor.UpperCenter;
                    Text.Font = GameFont.Small;

                    float labelWidth = 200f;
                    float unscaledNodeSize = isFoundation ? NodeSizeExpanded * 2f : NodeSizeExpanded;
                    Rect labelRect = new Rect(screenPos.x - (labelWidth / 2f), screenPos.y + (unscaledNodeSize / 2f) + 5f, labelWidth, 100f);

                    Widgets.Label(labelRect, node.def.LabelCap);

                    Text.Font = GameFont.Tiny;
                    var subRect = new Rect(labelRect.x, labelRect.y + 18f, labelRect.width, labelRect.height);
                    string subLabel = "BRM_Points".Translate(node.def.Cost);
                    if (isFoundation)
                        subLabel += "\n" + "BRM_Foundation".Translate();
                    Widgets.Label(subRect, subLabel);

                    GUI.matrix = oldMatrix;
                }
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndGroup();

            if (currentTab == DefsOf.Main)
            {
                DrawTopBar(inRect);
                DrawLeftBar(inRect);
            }
            DrawBottomBar(inRect);
            if (selectedNode != null)
                DrawRightPanel(inRect);
        }

        private void HandleInputs(Rect graphRect, Rect sliderExcl, Rect panelExcl, Rect searchBarExcl)
        {
            float zoomSensitivity = 0.05f;
            float minZoom = 0.2f;
            float maxZoom = 3f;

            Vector2 mousePos = Event.current.mousePosition;
            if (panelExcl.Contains(mousePos)) return;

            Vector2 localMousePos = mousePos - new Vector2(graphRect.x, graphRect.y);

            Rect totalExclusion = sliderExcl;
            totalExclusion.x -= 30f;

            if (Event.current.type == EventType.ScrollWheel && graphRect.Contains(Event.current.mousePosition))
            {
                zoom -= Event.current.delta.y * zoomSensitivity;
                zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
                Event.current.Use();
            }

            if (!totalExclusion.Contains(localMousePos) && !searchBarExcl.Contains(localMousePos) && isPanning && Event.current.type == EventType.MouseDrag)
            {
                cameraOffset += Event.current.delta / zoom;
                Event.current.Use();
            }
        }

        private void DrawTopBar(Rect inRect)
        {
            float padding = 5f;
            float iconSize = TopBarHeight - 10;
            float iconMargin = 10f;
            float textOffset = 60f;

            var erasWithProjects = AllTechLevels.Where(tl => DefDatabase<ResearchProjectDef>.AllDefs.Any(x => (x.techLevel == tl || x.techLevel == TechLevel.Undefined && tl == Faction.OfPlayer.def.techLevel) && x.tab == currentTab)).ToList();
            var topRect = new Rect(0f, 0f, inRect.width, TopBarHeight - 5);
            Widgets.DrawBoxSolid(topRect, ColorBoxBackground);
            var segWidth = inRect.width / erasWithProjects.Count;
            for (int i = 0; i < erasWithProjects.Count; i++)
            {
                var techLevel = erasWithProjects[i];
                var segRect = new Rect(i * segWidth, 0f, segWidth, TopBarHeight - 5);
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
                        InitPhysics(true);
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    }
                }
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(innerRect.x + textOffset, innerRect.y + 2f, innerRect.width - textOffset, 20f), techLevel.ToStringHuman().CapitalizeFirst());
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(innerRect.x + textOffset, innerRect.y + 18f, innerRect.width - textOffset, 16f), $"{finishedInEra}/{allProjectsInEra.Count}");

                var barRect = new Rect(i * segWidth, TopBarHeight - 5, segWidth, 5);
                Widgets.FillableBar(barRect, allProjectsInEra.Count > 0 ? finishedInEra / (float)allProjectsInEra.Count : 0f, TexBarFill, TexBarBg, doBorder: false);
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawLeftBar(Rect inRect)
        {
            if (BetterResearchMenuMod.settings.enableTechAdvancement)
            {
                float leftBarWidth = 50f;
                float iconSize = 40f;
                float iconMargin = 5f;
                float labelWidth = 200f;
                float labelHeight = 50f;

                var foundations = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => x.techLevel == currentEra && x.tab == currentTab && x.HasModExtension<ResearchFoundationExtension>()).ToList();
                var total = foundations.Count;
                if (total == 0)
                    return;

                var leftRect = new Rect(0f, TopBarHeight, leftBarWidth, inRect.height - TopBarHeight - BottomBarHeight);
                Widgets.DrawBoxSolid(leftRect, ColorLeftBarBackground);
                var finished = foundations.Count(x => x.IsFinished);
                var progress = total > 0 ? (float)finished / total : 0f;

                GUI.DrawTexture(leftRect, TexBarBg);
                var fillHeight = leftRect.height * progress;
                GUI.DrawTexture(new Rect(leftRect.x, leftRect.yMax - fillHeight, leftRect.width, fillHeight), TexBarFill);

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
                Widgets.Label(new Rect(leftRect.xMax + 5, leftRect.y, labelWidth, labelHeight), "BRM_FoundationsComplete".Translate(finished, total));

                if (progress >= 1f && currentEra < TechLevel.Archotech)
                {
                    var advanceBtnRect = new Rect(leftRect.xMax, leftRect.y, 200f, 40f);
                    if (Widgets.ButtonText(advanceBtnRect, "BRM_AdvanceTo".Translate(nextEra.ToStringHuman().CapitalizeFirst())))
                    {
                        Faction.OfPlayer.def.techLevel = nextEra;
                        Find.WindowStack.Add(new Window_TechAdvance(nextEra));
                        currentEra = nextEra;
                        InitPhysics(true);
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    }
                }
            }
        }

        private Vector2 researchDescScroll;
        private void DrawRightPanel(Rect inRect)
        {
            float btnMargin = 5f;
            float btnSize = 24f;
            float iconMargin = 20f;
            float iconTopOffset = 40f;
            float iconSize = 60f;
            float iconPadding = 8f;
            float titleOffset = 15f;
            float titlePadding = 40f;
            float sectionSpacing = 20f;
            float labelHeight = 24f;
            float researchDescHeight = 100f;
            float scrollOffset = 30f;
            float scrollBottomMargin = 70f;
            float scrollbarWidth = 16f;
            float listItemHeight = 28f;
            float actionBtnBottomMargin = 60f;
            float actionBtnHeight = 40f;

            var panelRect = new Rect(inRect.width - RightPanelWidth, TopBarHeight, RightPanelWidth, inRect.height - TopBarHeight - BottomBarHeight);
            Widgets.DrawBoxSolid(panelRect, Widgets.WindowBGFillColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(new Rect(panelRect.x + btnMargin, panelRect.y + btnMargin, btnSize, btnSize), "—", drawBackground: false, textColor: Color.white, doMouseoverSound: true))
            {
                selectedNode.state = NodeState.Minimized;
                State.nodeStates[selectedNode.def.defName] = NodeState.Minimized;
                selectedNode = null;
                physicsTemperature = Mathf.Max(physicsTemperature, 20f);
                DefsOf.BRM_CollapsingNode.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(new Rect(panelRect.xMax - btnSize - 5, panelRect.y + btnMargin, btnSize, btnSize), "x", drawBackground: false, textColor: Color.white, doMouseoverSound: true))
            {
                selectedNode = null;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (selectedNode is null) return;
            var proj = selectedNode.def;
            var projDescHeight = Text.CalcHeight(proj.description, panelRect.width - titlePadding);
            researchDescHeight = projDescHeight;

            var iconRect = new Rect(panelRect.x + iconMargin, panelRect.y + iconTopOffset, iconSize, iconSize);
            DrawBubble(iconRect, proj, iconPadding);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(iconRect.xMax + titleOffset, iconRect.y, panelRect.width - iconRect.width - titlePadding, iconRect.height), proj.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            var descRect = new Rect(panelRect.x + iconMargin, iconRect.yMax + 10, panelRect.width - titlePadding, researchDescHeight);
            Widgets.LabelScrollable(descRect, proj.description, ref researchDescScroll);
            var curY = iconRect.yMax + researchDescHeight + sectionSpacing + 10;
            Widgets.Label(new Rect(panelRect.x + iconMargin, curY, panelRect.width - titlePadding, labelHeight), "BRM_Unlocks".Translate());
            curY += scrollOffset;
            var outRect = new Rect(panelRect.x + iconMargin, curY, panelRect.width - titlePadding, panelRect.height - curY - scrollBottomMargin);
            var viewRect = new Rect(0f, 0f, outRect.width - scrollbarWidth, proj.UnlockedDefs.Count * listItemHeight);
            Widgets.BeginScrollView(outRect, ref rightPanelScroll, viewRect);
            var listY = 0f;
            foreach (var unlock in proj.UnlockedDefs)
            {
                Widgets.HyperlinkWithIcon(new Rect(0f, listY, viewRect.width, labelHeight), new Dialog_InfoCard.Hyperlink(unlock));
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
            else if (proj.IsFinished is false)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                var reason = GetLockedReason(proj);
                Widgets.Label(btnRect, reason.Colorize(ColorLocked));
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (Prefs.DevMode && !proj.IsFinished)
            {
                var devBtnRect = new Rect(btnRect.x, btnRect.y - actionBtnHeight - btnMargin, btnRect.width, actionBtnHeight);
                if (Widgets.ButtonText(devBtnRect, "DEV: Finish instantly"))
                {
                    Find.ResearchManager.FinishProject(proj);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
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
            float iconMargin = 20f;
            float iconYOffset = 10f;
            float iconSize = 60f;
            float iconPadding = 8f;
            float labelWidth = 400f;
            float labelHeight = 30f;
            float barYOffset = 40f;
            float barWidthReduction = 120f;
            float barHeight = 24f;

            var bottomRect = new Rect(0f, inRect.height - BottomBarHeight, inRect.width, BottomBarHeight);
            Widgets.DrawBoxSolid(bottomRect, ColorBoxBackground);

            var proj = Find.ResearchManager.currentProj;
            if (proj != null)
            {
                var contentX = iconMargin;
                var bubbleRect = new Rect(contentX, bottomRect.y + iconYOffset, iconSize, iconSize);
                DrawBubble(bubbleRect, proj, iconPadding);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(contentX + 80f, bottomRect.y + iconYOffset, labelWidth, labelHeight), "BRM_CurrentlyResearching".Translate(proj.LabelCap));
                var progRect = new Rect(contentX + 80f, bottomRect.y + barYOffset, bottomRect.width - contentX - 80f - barWidthReduction, barHeight);
                Widgets.FillableBar(progRect, proj.ProgressPercent, TexBarFill, TexBarBg, true);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(progRect, $"{proj.ProgressApparent:F0} / {proj.CostApparent:F0}");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            DrawResearchTabs(bottomRect);
        }

        private void DrawGraphControls(Rect controlAreaRect)
        {
            float oldGrav = BetterResearchMenuMod.settings.centerForceMultiplier;
            float oldSpace = BetterResearchMenuMod.settings.spacingForceMultiplier;
            float oldCont = BetterResearchMenuMod.settings.contractingForceMultiplier;

            float sliderHeight = 22f;
            float verticalSpacing = 6f;
            float iconSize = 16f;
            float sliderWidth = controlAreaRect.width - 20f;

            var gravityRect = new Rect(controlAreaRect.x, controlAreaRect.y, sliderWidth, sliderHeight);
            var spacingRect = new Rect(controlAreaRect.x, gravityRect.yMax + verticalSpacing, sliderWidth, sliderHeight);
            var contractingRect = new Rect(controlAreaRect.x, spacingRect.yMax + verticalSpacing, sliderWidth, sliderHeight);

            GUI.DrawTexture(new Rect(gravityRect.x - 24f, gravityRect.y + 1f, 16f, 16f), TexCenter);
            BetterResearchMenuMod.settings.centerForceMultiplier = Widgets.HorizontalSlider(gravityRect, BetterResearchMenuMod.settings.centerForceMultiplier, 0.1f, 5.0f, true);

            GUI.DrawTexture(new Rect(spacingRect.x - 24f, spacingRect.y + 1f, iconSize, iconSize), TexSpacing);
            BetterResearchMenuMod.settings.spacingForceMultiplier = Widgets.HorizontalSlider(spacingRect, BetterResearchMenuMod.settings.spacingForceMultiplier, 0.1f, 5.0f, true);

            GUI.DrawTexture(new Rect(contractingRect.x - 24f, contractingRect.y + 1f, iconSize, iconSize), TexContracting);
            BetterResearchMenuMod.settings.contractingForceMultiplier = Widgets.HorizontalSlider(contractingRect, BetterResearchMenuMod.settings.contractingForceMultiplier, 0.1f, 5.0f, true);

            if (oldGrav != BetterResearchMenuMod.settings.centerForceMultiplier ||
                oldSpace != BetterResearchMenuMod.settings.spacingForceMultiplier ||
                oldCont != BetterResearchMenuMod.settings.contractingForceMultiplier)
            {
                physicsTemperature = Mathf.Max(physicsTemperature, 20f);
            }
        }

        private void SetType(ResearchTabDef newTab)
        {
            if (currentTab == newTab) return;
            currentTab = newTab;
            currentEra = Faction.OfPlayer.def.techLevel;
            cameraOffset = Vector2.zero;
            zoom = 1f;
            InitPhysics(true);
        }

        private void DrawBubble(Rect rect, ResearchProjectDef proj, float iconPadding, bool drawSilhouette = false)
        {
            var tex = proj.IsFinished ? TexGreenBubble : proj == Find.ResearchManager.currentProj ? TexOrangeBubble : TexBubble;
            GUI.DrawTexture(rect, tex);

            if (proj.ProgressPercent > 0f && !proj.IsFinished)
            {
                var progressPercent = proj.ProgressPercent;
                GUI.color = ColorBubbleProgress;
                var fillRect = new Rect(rect.x, rect.y + rect.height * (1f - progressPercent), rect.width, rect.height * progressPercent);
                GUI.DrawTextureWithTexCoords(fillRect, TexBubble, new Rect(0f, 0f, 1f, progressPercent));
                GUI.color = Color.white;
            }

            var ext = proj.GetModExtension<ResearchIconExtension>();
            var hasCustomIcon = ext != null && !ext.texPath.NullOrEmpty();
            var customTex = hasCustomIcon ? ContentFinder<Texture2D>.Get(ext.texPath, false) : null;
            var iconRect = rect.ContractedBy(iconPadding);

            if (customTex != null)
                DrawIcon(iconRect, proj.IsFinished, drawSilhouette, r => Widgets.DrawTextureFitted(r, customTex, 1f));
            else
            {
                Def iconDef = null;
                foreach (var unlock in proj.UnlockedDefs)
                {
                    iconDef = unlock;
                    break;
                }
                if (iconDef != null)
                    DrawIcon(iconRect, proj.IsFinished, drawSilhouette, r => Widgets.DefIcon(r, iconDef));
            }

            if (ext != null && !ext.markerTexPath.NullOrEmpty())
            {
                var markerTex = ContentFinder<Texture2D>.Get(ext.markerTexPath, false);
                if (markerTex != null)
                {
                    float mSize = rect.width * 0.45f;
                    GUI.DrawTexture(new Rect(rect.xMax - mSize * 0.9f, rect.y - mSize * 0.1f, mSize, mSize), markerTex);
                }
            }
        }

        private void DrawIcon(Rect rect, bool isFinished, bool drawSilhouette, Action<Rect> drawAction)
        {
            if (isFinished)
                drawAction(rect);
            else if (drawSilhouette)
            {
                GUI.color = new Color(0.3f, 0.3f, 0.3f);
                drawAction(rect);
                GUI.color = Color.white;
            }
            else
                drawAction(rect);
        }

        private bool NodeMatchesSearch(ResearchNode node)
        {
            if (!quickSearchWidget.filter.Active) return true;
            if (quickSearchWidget.filter.Matches(node.def.label)) return true;
            foreach (var unlock in node.def.UnlockedDefs)
                if (quickSearchWidget.filter.Matches(unlock.label)) return true;
            return false;
        }

        public override void Notify_ClickOutsideWindow()
        {
            base.Notify_ClickOutsideWindow();
            quickSearchWidget.Unfocus();
        }

        private string GetLockedReason(ResearchProjectDef proj)
        {
            vanillaResearchWindow ??= new MainTabWindow_Research();
            selectedProjectField.SetValue(vanillaResearchWindow, proj);
            curTabIntField.SetValue(vanillaResearchWindow, proj.tab);

            GUI.BeginGroup(new Rect(-9999f, -9999f, 1f, 1f));
            drawStartButtonMethod.Invoke(vanillaResearchWindow, new object[] { new Rect(0f, 0f, 1f, 1f) });
            GUI.EndGroup();

            var reasons = (List<string>)lockedReasonsField.GetValue(vanillaResearchWindow);
            if (reasons != null && reasons.Count > 0)
                return "Locked".Translate() + ": " + reasons[0];
            return "Locked".Translate();
        }
    }
}
