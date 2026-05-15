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
        public int edgeCount;
        public float lockTicks;
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
        public static readonly Texture2D TexBarBg = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));
        public static readonly Texture2D TexBarFill = SolidColorMaterials.NewSolidColorTexture(new ColorInt(125, 183, 96).ToColor);
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

            if (ModsConfig.IsActive("vanillaexpanded.gravship"))
            {
                gravtechType = AccessTools.TypeByName("VanillaGravshipExpanded.World_ExposeData_Patch");
                if (gravtechType != null)
                {
                    currentGravtechProjectField = AccessTools.Field(gravtechType, "currentGravtechProject");
                }
                else
                {
                    Log.Error("Failed to find currentGravtechProject in VanillaGravshipExpanded.World_ExposeData_Patch");
                }
            }
        }

        private static Type gravtechType;
        private static FieldInfo currentGravtechProjectField;

        public static List<TechLevel> AllTechLevels = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Where(tl => tl != TechLevel.Undefined).ToList();
        private float TopBarHeight => currentTab == DefsOf.Main ? 45f : 0f;
        private float RightPanelWidth => 300f;
        private float NodeSizeExpanded => 80f;
        private float BottomBarHeight => GetActiveProjectsForTab(currentTab).Count > 0 ? 80f : 40f;
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
        private static Color ColorTechLevelTabSelected => new ColorInt(89, 94, 98).ToColor;
        private static Color ColorLeftBarBackground => new ColorInt(73, 78, 96).ToColor;
        private static Color ColorResearchingButton => new ColorInt(159, 125, 76).ToColor;
        private float ThicknessFinished => 3f;
        private float ThicknessUnfinished => 2f;
        private float IconPadding => 12f;
        private float physicsTemperature = 0f;
        private bool isPanning;
        private static Vector2 cameraOffset;
        private float zoom = 1f;
        private int lastStateCheckHash = -1;
        private static ResearchTabDef currentTab;
        private static TechLevel currentEra = TechLevel.Undefined;
        private List<ResearchNode> nodes = new List<ResearchNode>();
        private List<ResearchEdge> edges = new List<ResearchEdge>();
        private ResearchNode selectedNode;
        private Vector2 dragOffset;
        private Vector2 rightPanelScroll;
        private Vector2 dragStartMousePos;
        private bool wasDraggingNode;
        private static MainTabWindow_Research vanillaResearchWindow;
        private Rect graphRect;
        private QuickSearchWidget quickSearchWidget = new QuickSearchWidget();
        private float prevPanelWidth = 0f;
        private Vector2 lastMousePos;
        private static Color currentBgColor = new ColorInt(15, 20, 26).ToColor;
        private static Dictionary<string, Vector2> cachedCameraOffsets = new Dictionary<string, Vector2>();
        public override float Margin => 0f;
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, base.InitialSize.y * 1.3f);
        private bool LeftBarVisible =>
            currentTab == DefsOf.Main &&
            BetterResearchMenuMod.settings.enableTechAdvancement;

        private string GetCacheKey(ResearchProjectDef def) => $"{def.defName}_{currentEra}";

        private List<ResearchProjectDef> GetActiveProjectsForTab(ResearchTabDef tab)
        {
            var list = new List<ResearchProjectDef>();
            if (tab == DefsOf.Anomaly)
            {
                if (Find.ResearchManager.CurrentAnomalyKnowledgeProjects != null)
                    foreach (var item in Find.ResearchManager.CurrentAnomalyKnowledgeProjects)
                        if (item.project != null) list.Add(item.project);
            }
            else if (tab == DefsOf.VGE_Gravtech)
            {
                var p = (ResearchProjectDef)currentGravtechProjectField?.GetValue(null);
                if (p != null) list.Add(p);
            }
            else
            {
                if (Find.ResearchManager.currentProj != null && Find.ResearchManager.currentProj.tab == tab)
                    list.Add(Find.ResearchManager.currentProj);
            }
            return list;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            quickSearchWidget.Reset();
            if (currentTab == null) currentTab = DefsOf.Main;
            if (currentEra == TechLevel.Undefined && Faction.OfPlayer.def.techLevel != TechLevel.Undefined)
                currentEra = Faction.OfPlayer.def.techLevel;

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
                if (currentTab == DefsOf.Main && currentEra != TechLevel.Undefined && def.techLevel != currentEra) continue;
                if (BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted) continue;

                var node = new ResearchNode { def = def };
                node.state = GetNodeState(def);

                if (State.nodePositions.TryGetValue(GetCacheKey(def), out var savedPos))
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
                            if (State.nodePositions.TryGetValue(GetCacheKey(prereq), out var pPos))
                            {
                                avgPos += pPos;
                                count++;
                            }
                        }
                    }
                    if (count > 0) avgPos /= count;
                    else avgPos = new Vector2(((int)def.techLevel - (int)currentEra) * techLevelSpacing, 0f);
                    node.pos = avgPos + new Vector2(Rand.Range(-randomOffset, randomOffset), Rand.Range(-randomOffset, randomOffset));
                    State.nodePositions[GetCacheKey(def)] = node.pos;
                }
                node.drawPos = node.pos;

                nodes.Add(node);
            }

            foreach (var node in nodes)
            {
                node.edgeCount = 0;
            }

            foreach (var node in nodes)
            {
                if (node.def.prerequisites != null)
                {
                    foreach (var prereq in node.def.prerequisites)
                    {
                        var parentNode = nodes.FirstOrDefault(n => n.def == prereq);
                        if (parentNode != null)
                        {
                            edges.Add(new ResearchEdge { from = parentNode, to = node });
                            parentNode.edgeCount++;
                            node.edgeCount++;
                        }
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
                InitPhysicsLayout();
                string key = $"{currentTab.defName}_{currentEra}";
                if (cachedCameraOffsets.TryGetValue(key, out var savedOffset))
                {
                    cameraOffset = savedOffset;
                }
                else
                {
                    var centerNode = nodes.FirstOrDefault(n => n.def.HasModExtension<ResearchFoundationExtension>()) ?? nodes.FirstOrDefault();
                    if (centerNode != null) cameraOffset = -centerNode.pos;
                    else cameraOffset = Vector2.zero;
                    cachedCameraOffsets[key] = cameraOffset;
                }
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
            bool isOrphan = def.prerequisites.NullOrEmpty() && def.hiddenPrerequisites.NullOrEmpty();
            if (isOrphan && !def.IsFinished) return NodeState.Minimized;
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

            currentBgColor = Color.Lerp(currentBgColor, currentTab == DefsOf.Anomaly ? ColorAnomalyBackground : currentTab == DefsOf.VGE_Gravtech ? ColorVGEBackground : ColorGraphBackground, Time.deltaTime * 5f);

            if (wasDraggingNode)
            {
                physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            }

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
                    node.drawPos = Vector2.SmoothDamp(node.drawPos, node.pos, ref node.dampVelocity, 0.02f);
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

            var k = 300f;
            var minDistance = 15f;
            var maxRepulsionDistance = 500f;
            var dampingFactor = 0.6f;
            var clingMultiplier = 20f;

            velocitySum = 0f;
            foreach (var node in nodes)
            {
                if (node.isDragging || node.state == NodeState.Hidden)
                {
                    node.velocity = Vector2.zero;
                    continue;
                }

                if (node.lockTicks > 0f)
                {
                    node.lockTicks -= 1f;
                    node.velocity *= 0.1f;
                    velocitySum += node.velocity.sqrMagnitude;
                    continue;
                }

                var force = Vector2.zero;
                bool nodeIsFoundation = node.def.HasModExtension<ResearchFoundationExtension>();

                foreach (var other in nodes)
                {
                    if (node == other || other.state == NodeState.Hidden) continue;
                    var dir = node.pos - other.pos;
                    var dist = dir.magnitude;
                    if (dist < minDistance) { dir = Rand.UnitVector2; dist = minDistance; }

                    if (dist < maxRepulsionDistance)
                    {
                        bool otherIsFoundation = other.def.HasModExtension<ResearchFoundationExtension>();
                        float effectiveK = k * ((node.RadiusMultiplier + other.RadiusMultiplier) * 0.5f);
                        float forceMag = (effectiveK * effectiveK / dist) * BetterResearchMenuMod.settings.spacingForceMultiplier;
                        if (nodeIsFoundation && otherIsFoundation) forceMag *= 3f;
                        force += dir / dist * forceMag;
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

                Vector2 connectedCenter = Vector2.zero;
                int connCount = 0;
                foreach (var edge in edges)
                {
                    if (edge.from == node) { connectedCenter += edge.to.pos; connCount++; }
                    else if (edge.to == node) { connectedCenter += edge.from.pos; connCount++; }
                }
                if (connCount > 0)
                {
                    connectedCenter /= connCount;
                    Vector2 offset = connectedCenter - node.pos;
                    if (offset.magnitude > 400f)
                        force += offset * 0.8f;
                }

                float centerForce = 0.02f * BetterResearchMenuMod.settings.centerForceMultiplier;
                if (node.edgeCount == 0) centerForce *= 20f;
                force -= node.pos * centerForce;

                node.velocity = (node.velocity + force * dt) * dampingFactor;
                velocitySum += node.velocity.sqrMagnitude;
            }

            if (velocitySum < 0.5f) { physicsTemperature = 0f; return; }

            foreach (var node in nodes)
            {
                if (node.isDragging || node.state == NodeState.Hidden) continue;

                var move = node.velocity * dt * 8.0f;

                float maxMove = physicsTemperature * 0.2f;
                if (move.magnitude > maxMove) move = move.normalized * maxMove;

                node.pos += move;
                State.nodePositions[GetCacheKey(node.def)] = node.pos;
            }

            physicsTemperature *= 0.98f;
        }

        private float velocitySum = 0f;

        public override void DoWindowContents(Rect inRect)
        {

            var targetBgColor = currentTab == DefsOf.Anomaly ? ColorAnomalyBackground : currentTab == DefsOf.VGE_Gravtech ? ColorVGEBackground : ColorGraphBackground;
            GUI.color = currentBgColor;
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

            if (!mouseInPanel && !interactionExclusion.Contains(localMousePos) && !searchBarRect.Contains(localMousePos) && graphRect.Contains(mousePos))
            {
                ResearchNode hoveredNode = null;
                for (var i = nodes.Count - 1; i >= 0; i--)
                {
                    var node = nodes[i];
                    if (node.state == NodeState.Hidden) continue;
                    var screenPos = (node.drawPos + cameraOffset) * zoom + pivot;
                    var isFoundation = node.def.HasModExtension<ResearchFoundationExtension>();
                    var nodeSize = (node.state == NodeState.Dot || node.state == NodeState.Minimized) ? NodeSizeMinimized : (isFoundation ? NodeSizeExpanded * 2f : NodeSizeExpanded);
                    nodeSize *= zoom;
                    if (Vector2.Distance(screenPos, localMousePos) < nodeSize / 2f)
                    {
                        hoveredNode = node;
                        break;
                    }
                }

                if (hoveredNode != null && !isPanning && !wasDraggingNode)
                {
                    selectedNode = hoveredNode;
                }

                if (Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1 || Event.current.button == 2))
                {
                    if (hoveredNode != null)
                    {
                        if (Event.current.button == 0)
                        {
                            hoveredNode.isDragging = true;
                            wasDraggingNode = true;
                            dragStartMousePos = localMousePos;
                            var worldMousePos = ((localMousePos - pivot) / zoom) - cameraOffset;
                            dragOffset = hoveredNode.pos - worldMousePos;
                        }
                        else if (Event.current.button == 1)
                        {
                            hoveredNode.state = NodeState.Minimized;
                            State.nodeStates[hoveredNode.def.defName] = hoveredNode.state;
                            physicsTemperature = Mathf.Max(physicsTemperature, 20f);
                            DefsOf.BRM_CollapsingNode.PlayOneShotOnCamera();
                        }
                        Event.current.Use();
                    }
                    else if (Event.current.button != 1)
                    {
                        isPanning = true;
                        Event.current.Use();
                    }
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

                                foreach (var edge in edges)
                                {
                                    if (edge.to == selectedNode) edge.from.lockTicks = 60f;
                                }
                            }
                        }
                        else if (selectedNode.state == NodeState.Expanded)
                        {
                            if (!selectedNode.def.CanStartNow && !selectedNode.def.IsFinished)
                            {
                                Messages.Message(GetLockedReason(selectedNode.def), MessageTypeDefOf.RejectInput, false);
                            }
                            else if (selectedNode.def.CanStartNow && !GetActiveProjectsForTab(currentTab).Contains(selectedNode.def))
                            {
                                SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                                Find.ResearchManager.SetCurrentProject(selectedNode.def);
                            }
                        }
                    }
                }
                wasDraggingNode = false;
                foreach (var node in nodes)
                    node.isDragging = false;

                physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            }

            foreach (var node in nodes)
            {
                if (node.isDragging && Event.current.type == EventType.MouseDrag)
                {
                    node.pos = ((localMousePos - pivot) / zoom) - cameraOffset + dragOffset;
                    node.velocity = Vector2.zero;
                    node.dampVelocity = Vector2.zero;
                    State.nodePositions[GetCacheKey(node.def)] = node.pos;
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

            var activeProjects = GetActiveProjectsForTab(currentTab);
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
                bool isSilhouetted = !node.def.IsFinished;
                DrawBubble(nodeRect, node.def, padding * zoom, activeProjects, drawSilhouette: isSilhouetted);

                if (zoom > 0.4f)
                {
                    Text.Anchor = TextAnchor.UpperCenter;
                    Text.Font = zoom < 0.6f ? GameFont.Tiny : (zoom > 1.2f ? GameFont.Medium : GameFont.Small);

                    float labelWidth = 200f * zoom;
                    float unscaledNodeSize = isFoundation ? NodeSizeExpanded * 2f : NodeSizeExpanded;
                    Rect labelRect = new Rect(screenPos.x - (labelWidth / 2f), screenPos.y + (unscaledNodeSize * zoom / 2f) + 5f * zoom, labelWidth, 200f * zoom);

                    Widgets.Label(labelRect, node.def.LabelCap);
                    float titleHeight = Text.CalcHeight(node.def.LabelCap, labelWidth);

                    Text.Font = GameFont.Tiny;
                    var subRect = new Rect(labelRect.x, labelRect.y + titleHeight, labelRect.width, labelRect.height);
                    string subLabel = "BRM_Points".Translate(node.def.Cost);
                    if (isFoundation)
                        subLabel += "\n" + "BRM_Foundation".Translate();
                    Widgets.Label(subRect, subLabel);
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

            if (Event.current.type == EventType.MouseDown)
                lastMousePos = localMousePos;
            if (Event.current.type == EventType.ScrollWheel && graphRect.Contains(Event.current.mousePosition))
            {
                zoom -= Event.current.delta.y * zoomSensitivity;
                zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
                Event.current.Use();
            }

            if (!totalExclusion.Contains(localMousePos) && !searchBarExcl.Contains(localMousePos) && isPanning && Event.current.type == EventType.MouseDrag)
            {
                cameraOffset += (localMousePos - lastMousePos) / zoom;
                lastMousePos = localMousePos;
                cachedCameraOffsets[$"{currentTab.defName}_{currentEra}"] = cameraOffset;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag)
            {
                lastMousePos = localMousePos;
            }
        }

        private void DrawTopBar(Rect inRect)
        {
            float padding = 5f;
            float iconSize = TopBarHeight - 10;
            float iconMargin = 10f;
            float textOffset = 60f;

            var erasWithProjects = AllTechLevels.Where(tl => DefDatabase<ResearchProjectDef>.AllDefs.Any(x => (x.techLevel == tl || x.techLevel == TechLevel.Undefined && tl == Faction.OfPlayer.def.techLevel) && x.tab == currentTab)).ToList();
            erasWithProjects.Insert(0, TechLevel.Undefined);
            var topRect = new Rect(0f, 0f, inRect.width, TopBarHeight - 5);
            Widgets.DrawBoxSolid(topRect, ColorBoxBackground);
            float totalWeights = erasWithProjects.Count - 0.5f;
            var segWidth = inRect.width / totalWeights;
            float currentX = 0f;
            for (int i = 0; i < erasWithProjects.Count; i++)
            {
                var techLevel = erasWithProjects[i];
                float weight = techLevel == TechLevel.Undefined ? 0.5f : 1f;
                float width = segWidth * weight;
                var segRect = new Rect(currentX, 0f, width, TopBarHeight - 5);
                if (techLevel == currentEra)
                {
                    Widgets.DrawBoxSolid(segRect, ColorTechLevelTabSelected);
                }
                else
                {
                    Widgets.DrawBoxSolid(segRect, ColorTechLevelTab);
                }
                var innerRect = segRect.ContractedBy(padding);
                var allProjectsInEra = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => (techLevel == TechLevel.Undefined || x.techLevel == techLevel || x.techLevel == TechLevel.Undefined && techLevel == Faction.OfPlayer.def.techLevel) && x.tab == currentTab).ToList();
                var finishedInEra = allProjectsInEra.Count(x => x.IsFinished);
                if (TechLevelIcons.TryGetValue(techLevel, out var icon))
                    GUI.DrawTexture(new Rect(innerRect.x + iconMargin, innerRect.y, iconSize, iconSize), icon);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (Widgets.ButtonInvisible(segRect))
                {
                    if (techLevel != TechLevel.Undefined && BetterResearchMenuMod.settings.restrictResearchToTechLevel && techLevel > Faction.OfPlayer.def.techLevel)
                    {
                        Messages.Message("BRM_CannotAccessEra".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else if (techLevel != currentEra)
                    {
                        currentEra = techLevel;
                        zoom = 1f;
                        InitPhysics(true);
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    }
                }
                Text.Font = GameFont.Small;
                string label = techLevel == TechLevel.Undefined ? "BRM_AllEras".Translate() : techLevel.ToStringHuman().CapitalizeFirst();
                Widgets.Label(new Rect(innerRect.x + textOffset, innerRect.y + 2f, innerRect.width - textOffset, 20f), label);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(innerRect.x + textOffset, innerRect.y + 18f, innerRect.width - textOffset, 16f), $"{finishedInEra}/{allProjectsInEra.Count}");

                var barRect = new Rect(currentX, TopBarHeight - 5, width, 5);
                Widgets.FillableBar(barRect, allProjectsInEra.Count > 0 ? finishedInEra / (float)allProjectsInEra.Count : 0f, TexBarFill, TexBarBg, doBorder: false);
                currentX += width;
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
                var leftRect = new Rect(0f, TopBarHeight, leftBarWidth, inRect.height - TopBarHeight - BottomBarHeight);
                Widgets.DrawBoxSolid(leftRect, ColorLeftBarBackground);
                var finished = foundations.Count(x => x.IsFinished);
                var progress = total > 0 ? (float)finished / total : 1f;

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
                if (total == 0)
                {
                    Widgets.Label(new Rect(leftRect.xMax + 5, leftRect.y, labelWidth, labelHeight), "BRM_FoundationsComplete".Translate(0, 0));
                }
                else
                {
                    Widgets.Label(new Rect(leftRect.xMax + 5, leftRect.y, labelWidth, labelHeight), "BRM_FoundationsComplete".Translate(finished, total));
                }

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

            var iconRect = new Rect(panelRect.x + iconMargin, panelRect.y + iconTopOffset, iconSize, iconSize);
            DrawBubble(iconRect, proj, iconPadding, GetActiveProjectsForTab(currentTab));
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(iconRect.xMax + titleOffset, iconRect.y, panelRect.width - iconRect.width - titlePadding, iconRect.height), proj.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            var researchDescHeight = Mathf.Min
            (Text.CalcHeight(proj.description, panelRect.width - titlePadding - 16) + 5, 100);
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
            if (GetActiveProjectsForTab(currentTab).Contains(proj))
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

            var hasAnomaly = DefsOf.Anomaly != null && (DebugSettings.godMode || Find.Anomaly.HighestLevelReached > 0);
            var hasGravtech = DefsOf.VGE_Gravtech != null && (DebugSettings.godMode || DefsOf.BasicGravtech.IsFinished);
            var tabCount = 1 + (hasAnomaly ? 1 : 0) + (hasGravtech ? 1 : 0);
            var tabsWidth = tabWidth * tabCount;
            var tabsRect = new Rect(rect.xMax - tabsWidth - tabsMargin, rect.yMax - tabHeight - tabsYOffset, tabsWidth, tabHeight);
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
            float iconMargin = 10f;
            float iconSize = 60f;
            float iconPadding = 8f;
            float labelHeight = 30f;
            float barHeight = 24f;

            var bottomRect = new Rect(0f, inRect.height - BottomBarHeight, inRect.width, BottomBarHeight);
            Widgets.DrawBoxSolid(bottomRect, ColorBoxBackground);

            var activeProjs = GetActiveProjectsForTab(currentTab);

            bool hasAnomaly = DefsOf.Anomaly != null && (DebugSettings.godMode || Find.Anomaly.HighestLevelReached > 0);
            bool hasGravtech = DefsOf.VGE_Gravtech != null && (DebugSettings.godMode || DefsOf.BasicGravtech.IsFinished);
            int tabCount = 1 + (hasAnomaly ? 1 : 0) + (hasGravtech ? 1 : 0);
            float tabsWidth = 120f * tabCount;
            float availableWidth = bottomRect.width - tabsWidth - 20f;

            for (int i = 0; i < activeProjs.Count; i++)
            {
                var proj = activeProjs[i];
                float segWidth = availableWidth / activeProjs.Count;
                float startX = i * segWidth;

                var bubbleRect = new Rect(startX + iconMargin, bottomRect.y + 10f, iconSize, iconSize);
                DrawBubble(bubbleRect, proj, iconPadding, activeProjs);

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;

                string labelText;
                if (currentTab == DefsOf.Anomaly && proj.knowledgeCategory != null)
                {
                    string key = proj.knowledgeCategory == KnowledgeCategoryDefOf.Advanced ? "BRM_ActiveAdvancedResearch" : "BRM_ActiveBasicResearch";
                    labelText = key.Translate(proj.LabelCap);
                }
                else
                {
                    labelText = "BRM_CurrentlyResearching".Translate(proj.LabelCap);
                }

                Widgets.Label(new Rect(startX + 80f, bottomRect.y + 10f, segWidth - 80f, labelHeight), labelText);

                var progRect = new Rect(startX + 80f, bottomRect.y + 40f, segWidth - 100f, barHeight);
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
            BetterResearchMenuMod.settings.spacingForceMultiplier = Widgets.HorizontalSlider(spacingRect, BetterResearchMenuMod.settings.spacingForceMultiplier, 0.1f, 10.0f, true);

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
            currentEra = newTab == DefsOf.Main ? Faction.OfPlayer.def.techLevel : TechLevel.Undefined;
            zoom = 1f;
            InitPhysics(true);
        }

        private void DrawBubble(Rect rect, ResearchProjectDef proj, float iconPadding, List<ResearchProjectDef> activeProjs, bool drawSilhouette = false)
        {
            var tex = proj.IsFinished ? TexGreenBubble : activeProjs.Contains(proj) ? TexOrangeBubble : TexBubble;
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
            Color? iconColor = drawSilhouette ? new Color(0.1f, 0.1f, 0.1f, 0.8f) : null;

            if (customTex != null)
            {
                if (iconColor.HasValue) GUI.color = iconColor.Value;
                Widgets.DrawTextureFitted(iconRect, customTex, 1f);
                if (iconColor.HasValue) GUI.color = Color.white;
            }
            else
            {
                Def iconDef = null;
                foreach (var unlock in proj.UnlockedDefs)
                {
                    iconDef = unlock;
                    break;
                }
                if (iconDef != null)
                {
                    var oldColor = Color.white;
                    var terrain = iconDef as TerrainDef;
                    var thingDef = iconDef as ThingDef;
                    List<StuffCategoryDef> oldStuff = null;
                    Material oldMat = null;
                    if (terrain != null)
                    {
                        oldColor = terrain.uiIconColor;
                        terrain.uiIconColor = iconColor ?? oldColor;
                    }
                    else if (thingDef != null)
                    {
                        oldColor = thingDef.uiIconColor;
                        thingDef.uiIconColor = iconColor ?? oldColor;
                        if (iconColor.HasValue)
                        {
                            oldStuff = thingDef.stuffCategories;
                            thingDef.stuffCategories = null;
                            oldMat = thingDef.uiIconMaterial;
                            thingDef.uiIconMaterial = null;
                        }
                    }
                    if (iconColor.HasValue) GUI.color = iconColor.Value;
                    Widgets.DefIcon(iconRect, iconDef, null, 1f, null, false, iconColor, null);
                    if (iconColor.HasValue) GUI.color = Color.white;
                    if (terrain != null)
                    {
                        terrain.uiIconColor = oldColor;
                    }
                    else if (thingDef != null)
                    {
                        thingDef.uiIconColor = oldColor;
                        if (oldStuff != null)
                        {
                            thingDef.stuffCategories = oldStuff;
                        }
                        if (iconColor.HasValue)
                        {
                            thingDef.uiIconMaterial = oldMat;
                        }
                    }
                }
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
            if (BetterResearchMenuMod.settings.restrictResearchToTechLevel && proj.techLevel > Faction.OfPlayer.def.techLevel && proj.tab != DefsOf.Anomaly && proj.tab != DefsOf.VGE_Gravtech)
            {
                return "Locked".Translate() + ": " + "BRM_CannotAccessEra".Translate();
            }

            vanillaResearchWindow ??= new MainTabWindow_Research();
            vanillaResearchWindow.selectedProject = proj;
            vanillaResearchWindow.curTabInt = proj.tab;

            GUI.BeginGroup(new Rect(-9999f, -9999f, 1f, 1f));
            vanillaResearchWindow.DrawStartButton(new Rect(0f, 0f, 1f, 1f));
            GUI.EndGroup();

            var reasons = MainTabWindow_Research.lockedReasons;
            if (reasons != null && reasons.Count > 0)
                return "Locked".Translate() + ": " + reasons[0];
            return "Locked".Translate();
        }
    }
}
