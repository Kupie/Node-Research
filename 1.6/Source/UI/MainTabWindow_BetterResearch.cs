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
        public bool isAnchor;
        public int nodeIndex;
        public string cachedKey;
        public float cachedMass;

        public Vector2 lastRepulsionForce;
        public Vector2 lastAttractionForce;
        public Vector2 lastCenterForce;

        public bool isPhantom;
        public TechLevel phantomEra;
        public bool isGroupNode;
        public GroupNodeDef groupNodeDef;
        public bool isFoundation;
        public bool isEmergence;

        public bool isFinishedCache;
        public bool canStartNowCache;
        public bool isLockedCache;
        public bool matchesSearchCache;
        public int childCount;
        public HashSet<ResearchNode> connectedNodes = new HashSet<ResearchNode>();
        public List<ResearchEdge> nodeEdges = new List<ResearchEdge>();
        public string cachedSubLabel;
        public float cachedTitleHeight_Tiny;
        public float cachedTitleHeight_Small;
        public float cachedTitleHeight_Medium;
        public float lastCachedZoom = -1f;

        public float RadiusMultiplier => this.isFoundation ? 1.5f : 1.0f;
        public float SpacingMultiplier => (state == NodeState.Dot || state == NodeState.Minimized) ? 0.5f : 1.0f;

        public float GetCachedTitleHeight(GameFont font, float labelWidth, float zoom)
        {
            if (lastCachedZoom != zoom)
            {
                cachedTitleHeight_Tiny = 0f;
                cachedTitleHeight_Small = 0f;
                cachedTitleHeight_Medium = 0f;
                lastCachedZoom = zoom;
            }
            return font switch
            {
                GameFont.Tiny => cachedTitleHeight_Tiny > 0 ? cachedTitleHeight_Tiny : (cachedTitleHeight_Tiny = Text.CalcHeight(def.LabelCap, labelWidth)),
                GameFont.Medium => cachedTitleHeight_Medium > 0 ? cachedTitleHeight_Medium : (cachedTitleHeight_Medium = Text.CalcHeight(def.LabelCap, labelWidth)),
                _ => cachedTitleHeight_Small > 0 ? cachedTitleHeight_Small : (cachedTitleHeight_Small = Text.CalcHeight(def.LabelCap, labelWidth)),
            };
        }

        public float GetNodeSize(float baseSize, NodeState currentState)
        {
            if (!this.isFoundation) return baseSize;
            return currentState == NodeState.Minimized ? baseSize * 1.875f : baseSize * 1.75f;
        }
    }
    public class ResearchEdge
    {
        public ResearchNode from;
        public ResearchNode to;
        public bool isGroupEdge;
    }

    [HotSwappable]
    [StaticConstructorOnStartup]
    public class MainTabWindow_BetterResearch : MainTabWindow_Research
    {
        private static Texture2D TexBubble = ContentFinder<Texture2D>.Get("UI/Bubble");
        private static Texture2D TexGreenBubble = ContentFinder<Texture2D>.Get("UI/GreenBubble");
        private static Texture2D TexOrangeBubble = ContentFinder<Texture2D>.Get("UI/OrangeBubble");
        private static readonly Texture2D TexBarBg = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));
        private static readonly Texture2D TexBarFill = SolidColorMaterials.NewSolidColorTexture(new ColorInt(125, 183, 96).ToColor);
        private static Texture2D TexCenter = ContentFinder<Texture2D>.Get("UI/CenterSlider");
        private static Texture2D TexSpacing = ContentFinder<Texture2D>.Get("UI/SpacingSlider");
        private static Texture2D TexContracting = ContentFinder<Texture2D>.Get("UI/ContractingSlider");
        private static Texture2D TexPhysics = ContentFinder<Texture2D>.Get("UI/PhysicsToggle");
        private static Texture2D TexVanilla = ContentFinder<Texture2D>.Get("UI/TreeToggle");
        private static Texture2D TexLock = ContentFinder<Texture2D>.Get("UI/Lock");
        private static Texture2D TexSettings = ContentFinder<Texture2D>.Get("UI/Settings");
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

        private const float ControlBtnSize = 24f;
        private const float ControlBtnGap = 8f;

        public static List<TechLevel> AllTechLevels = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Where(tl => tl != TechLevel.Undefined).ToList();
        private float TopBarHeight => CurTab == DefsOf.Main ? 45f : 0f;
        private float RightPanelWidth = 300f;
        private float NodeSizeExpanded = 80f;
        private float BottomBarHeight => GetActiveProjectsCached(CurTab).Count > 0 ? 80f : 40f;

        public static bool GodModeReveal => BetterResearchMenuMod.settings.revealAllInGodMode && DebugSettings.godMode;
        private float NodeSizeMinimized = 40f;
        private float NodeSizeDot = 20f;
        private static Color ColorBoxBackground = new ColorInt(38, 36, 36).ToColor;
        private static Color ColorGraphBackground = new ColorInt(15, 20, 26).ToColor;
        private static Color ColorAnomalyBackground = new ColorInt(45, 25, 25).ToColor;
        private static Color ColorVGEBackground = new ColorInt(25, 45, 45).ToColor;
        private static Color ColorEdgeFinished = new ColorInt(95, 99, 102).ToColor;
        private static Color ColorEdgeUnfinished = new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeDot = new ColorInt(95, 99, 102).ToColor;
        private static Color ColorNodeMinimized = new ColorInt(95, 99, 102).ToColor;
        private static Color ColorBubbleProgress = new ColorInt(125, 183, 96).ToColor;
        private static Color ColorTechLevelTab = new ColorInt(29, 34, 38).ToColor;
        private static Color ColorTechLevelTabSelected = new ColorInt(89, 94, 98).ToColor;
        private static Color ColorLeftBarBackground = new ColorInt(73, 78, 96).ToColor;
        private float ThicknessFinished = 3f;
        private float ThicknessUnfinished = 2f;
        private float IconPadding = 12f;
        private float physicsTemperature = 0f;
        private bool debugLogNextTick = false;
        private bool isPanning;
        private static Vector2 cameraOffset;
        private float zoom = 1f;
        private int lastStateCheckHash = -1;
        private ResearchTabDef lastCurTab;
        private static TechLevel currentEra = TechLevel.Undefined;
        private List<ResearchNode> nodes = [];
        private List<ResearchEdge> edges = [];
        private ResearchNode selectedNode;
        private bool selectionLocked;
        private Vector2 dragOffset;
        private Vector2 dragStartMousePos;
        private bool wasDraggingNode;
        private bool hasSignificantDrag;
        private Rect graphRect;
        private float prevPanelWidth = 0f;
        private Vector2 lastMousePos;
        private static Color currentBgColor = new ColorInt(15, 20, 26).ToColor;
        private static Dictionary<string, Vector2> cachedCameraOffsets = [];
        private static HashSet<string> seededLayoutKeys = new HashSet<string>();

        private static Dictionary<ResearchProjectDef, List<Def>> cachedUnlockedDefs = [];
        private static Dictionary<ResearchProjectDef, CachedProjInfo> projInfoCache = new Dictionary<ResearchProjectDef, CachedProjInfo>();
        private static Dictionary<string, Texture2D> cachedCustomTextures = [];
        private Dictionary<TechLevel, (List<ResearchProjectDef> all, int finished)> topBarDataCache = [];
        private int topBarCacheStateHash = -1;

        private List<ResearchProjectDef> activeProjectsCache;
        private bool activeProjectsCacheDirty = true;
        private bool lastGodMode = false;
        private Dictionary<string, NodeState> godModeStateSnapshot = null;
        private Dictionary<ResearchProjectDef, float> descHeightCache = new Dictionary<ResearchProjectDef, float>();

        private List<TechLevel> cachedErasWithProjects;
        private ResearchTabDef lastErasTab;

        public override float Margin => 0f;
        public override Vector2 InitialSize
        {
            get
            {
                Vector2 requestedTabSize = RequestedTabSize;
                requestedTabSize.x = UI.screenWidth;
                requestedTabSize.y *= 1.3f;
                if (requestedTabSize.y > (float)(UI.screenHeight - 35))
                {
                    requestedTabSize.y = UI.screenHeight - 35;
                }
                return requestedTabSize;
            }
        }
        private bool LeftBarVisible =>
            CurTab == DefsOf.Main &&
            BetterResearchMenuMod.settings.enableTechAdvancement;

        private string GetCacheKey(ResearchNode node) =>
            node.isGroupNode ? $"group_{node.groupNodeDef.defName}_{(int)currentEra}" :
            node.isPhantom ? $"phantom_{(int)node.phantomEra}_{(int)currentEra}" : GetCacheKey(node.def);

        private string GetCacheKey(ResearchProjectDef def) => $"{def.defName}_{currentEra}";

        private static List<Def> GetCachedUnlockedDefs(ResearchProjectDef proj)
        {
            if (!cachedUnlockedDefs.TryGetValue(proj, out var list))
            {
                list = proj.UnlockedDefs.ToList();
                cachedUnlockedDefs[proj] = list;
            }
            return list;
        }

        private static Texture2D GetCachedCustomTexture(string texPath)
        {
            if (!cachedCustomTextures.TryGetValue(texPath, out var tex))
            {
                tex = ContentFinder<Texture2D>.Get(texPath, true);
                cachedCustomTextures[texPath] = tex;
            }
            return tex;
        }

        public class CachedProjInfo
        {
            public bool hasEmergence;
            public TechLevel emergenceLevel;
            public bool hasCustomIcon;
            public Texture2D customTex;
            public Texture2D markerTex;
            public Def iconDef;
        }

        private static string BuildNodeSubLabel(ResearchNode node)
        {
            var pts = "BRM_Points".Translate(node.def.Cost);
            if (node.isFoundation) return pts + "\n" + "BRM_Foundation".Translate();
            if (node.isEmergence) return pts + "\n" + "BRM_Emergence".Translate();
            return pts;
        }

        private static CachedProjInfo GetProjInfo(ResearchProjectDef def)
        {
            if (!projInfoCache.TryGetValue(def, out var info))
            {
                info = new CachedProjInfo();
                var ext = def.GetModExtension<ResearchIconExtension>();
                if (ext != null)
                {
                    if (!ext.texPath.NullOrEmpty())
                    {
                        info.hasCustomIcon = true;
                        info.customTex = GetCachedCustomTexture(ext.texPath);
                    }
                    if (!ext.markerTexPath.NullOrEmpty())
                    {
                        info.markerTex = GetCachedCustomTexture(ext.markerTexPath);
                    }
                }

                var emergenceExt = def.GetModExtension<EmergenceExtension>();
                if (emergenceExt != null)
                {
                    info.hasEmergence = true;
                    info.emergenceLevel = emergenceExt.targetLevel;
                }

                var unlocks = GetCachedUnlockedDefs(def);
                if (unlocks.Count > 0) info.iconDef = unlocks[0];

                projInfoCache[def] = info;
            }
            return info;
        }

        private (List<ResearchProjectDef> all, int finished) GetTopBarData(TechLevel techLevel)
        {
            if (topBarCacheStateHash != lastStateCheckHash)
            {
                topBarDataCache.Clear();
                topBarCacheStateHash = lastStateCheckHash;
            }
            if (!topBarDataCache.TryGetValue(techLevel, out var data))
            {
                var all = DefDatabase<ResearchProjectDef>.AllDefs
                    .Where(x => (techLevel == TechLevel.Undefined
                                || x.techLevel == techLevel
                                || (x.techLevel == TechLevel.Undefined && techLevel == Faction.OfPlayer.def.techLevel))
                                && x.tab == CurTab)
                    .ToList();
                data = (all, all.Count(x => x.IsFinished));
                topBarDataCache[techLevel] = data;
            }
            return data;
        }

        private List<ResearchProjectDef> GetActiveProjectsCached(ResearchTabDef tab)
        {
            if (!activeProjectsCacheDirty) return activeProjectsCache;
            activeProjectsCache = GetActiveProjectsForTab(tab);
            activeProjectsCacheDirty = false;
            return activeProjectsCache;
        }

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

        private void CollapseNode(ResearchNode node)
        {
            if (node == null) return;
            node.state = NodeState.Minimized;
            State.nodeStates[node.def.defName] = node.state;
            node.velocity = Vector2.zero;

            physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            DefsOf.BRM_CollapsingNode.PlayOneShotOnCamera();
        }

        public override void PreOpen()
        {
            TabCollapser.Collapse();
            base.PreOpen();
            if (CurTab == null) CurTab = DefsOf.Main;
            lastCurTab = CurTab;
            if (currentEra == TechLevel.Undefined && Faction.OfPlayer.def.techLevel != TechLevel.Undefined)
                currentEra = Faction.OfPlayer.def.techLevel;

            InitPhysics(true);
        }

        public void ForceEra(TechLevel era)
        {
            currentEra = era;
            selectionLocked = false;
            selectedNode = null;
            selectedProject = null;
            zoom = 1f;
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
                if (def.tab != CurTab) continue;
                if (CurTab == DefsOf.Main && currentEra != TechLevel.Undefined && def.techLevel != currentEra) continue;
                if (!GodModeReveal && BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted) continue;

                var node = new ResearchNode { def = def };
                node.isFoundation = def.IsFoundation();
                node.isEmergence = def.HasModExtension<EmergenceExtension>();
                node.state = GetNodeState(def);
                node.isFinishedCache = def.IsFinished;
                node.canStartNowCache = def.CanStartNow;
                node.isLockedCache = !node.canStartNowCache && !node.isFinishedCache;
                node.cachedSubLabel = BuildNodeSubLabel(node);

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
                node.nodeIndex = nodes.Count - 1;
                node.cachedKey = node.isPhantom ? $"phantom_{(int)node.phantomEra}_{(int)currentEra}" : $"{def.defName}_{currentEra}";
            }

            foreach (var node in nodes)
            {
                node.edgeCount = 0;
                node.childCount = 0;
                node.nodeEdges.Clear();
            }

            foreach (var node in nodes)
            {
                if (node.isPhantom || node.def.prerequisites == null) continue;
                foreach (var prereq in node.def.prerequisites)
                {
                    var parentNode = nodes.FirstOrDefault(n => !n.isPhantom && n.def == prereq);
                    if (parentNode != null)
                    {
                        var edge = new ResearchEdge { from = parentNode, to = node };
                        edges.Add(edge);
                        parentNode.nodeEdges.Add(edge);
                        node.nodeEdges.Add(edge);
                        parentNode.edgeCount++;
                        parentNode.childCount++;
                        node.edgeCount++;
                        parentNode.connectedNodes.Add(node);
                        node.connectedNodes.Add(parentNode);
                    }
                }
            }

            foreach (var node in nodes)
                node.cachedMass = 1f + Mathf.Pow(node.edgeCount, 1.2f) * 0.5f;

            string layoutKey = $"{CurTab?.defName}_{(int)currentEra}";
            if (!seededLayoutKeys.Contains(layoutKey))
            {
                SeedHierarchicalPositions();
                seededLayoutKeys.Add(layoutKey);
            }

            if (CurTab == DefsOf.Main && currentEra != TechLevel.Undefined)
            {
                phantomEdgeSet.Clear();
                var phantomNodeByEra = new Dictionary<TechLevel, ResearchNode>();
                int nonPhantomCount = nodes.Count;
                for (int pi = 0; pi < nonPhantomCount; pi++)
                {
                    var node = nodes[pi];
                    if (node.isPhantom || node.def.prerequisites == null) continue;
                    foreach (var prereq in node.def.prerequisites)
                    {
                        if (prereq.techLevel == currentEra || prereq.techLevel == TechLevel.Undefined) continue;

                        var era = prereq.techLevel;
                        if (!phantomNodeByEra.TryGetValue(era, out var phantom))
                        {
                            phantom = new ResearchNode
                            {
                                isPhantom = true,
                                phantomEra = era,
                                state = NodeState.Minimized,
                                isFoundation = false,
                                isEmergence = false
                            };
                            phantom.cachedKey = $"phantom_{(int)era}_{(int)currentEra}";
                            if (State.nodePositions.TryGetValue(phantom.cachedKey, out var savedPos))
                                phantom.pos = savedPos;
                            else
                                phantom.pos = new Vector2(((int)era - (int)currentEra) * techLevelSpacing, 0f);
                            phantom.drawPos = phantom.pos;
                            phantomNodeByEra[era] = phantom;
                            nodes.Add(phantom);
                            phantom.nodeIndex = nodes.Count - 1;
                            phantom.cachedMass = 1f + Mathf.Pow(phantom.edgeCount, 1.2f) * 0.5f;
                        }

                        if (phantomEdgeSet.Add((phantom, node)))
                        {
                            var edge = new ResearchEdge { from = phantom, to = node };
                            edges.Add(edge);
                            phantom.nodeEdges.Add(edge);
                            node.nodeEdges.Add(edge);
                            phantom.edgeCount++;
                            phantom.childCount++;
                            node.edgeCount++;
                            phantom.connectedNodes.Add(node);
                            node.connectedNodes.Add(phantom);
                            node.cachedMass = 1f + Mathf.Pow(node.edgeCount, 1.2f) * 0.5f;
                        }
                    }
                }
            }

            {
                int preGroupCount = nodes.Count;
                var groupNodeByDef = new Dictionary<GroupNodeDef, ResearchNode>();
                for (int gi = 0; gi < preGroupCount; gi++)
                {
                    var node = nodes[gi];
                    if (node.isPhantom || node.isGroupNode || node.def == null) continue;
                    var ext = node.def.GetModExtension<GroupParentExtension>();
                    if (ext?.groupNode == null) continue;
                    var gDef = ext.groupNode;
                    if (gDef.tab != null && gDef.tab != CurTab) continue;

                    if (!groupNodeByDef.TryGetValue(gDef, out var groupNode))
                    {
                        groupNode = new ResearchNode
                        {
                            isGroupNode = true,
                            groupNodeDef = gDef,
                            state = NodeState.Minimized,
                        };
                        groupNode.cachedKey = $"group_{gDef.defName}_{(int)currentEra}";
                        groupNode.pos = State.nodePositions.TryGetValue(groupNode.cachedKey, out var savedGroupPos)
                            ? savedGroupPos
                            : new Vector2(Rand.Range(-150f, 150f), Rand.Range(-150f, 150f));
                        groupNode.drawPos = groupNode.pos;
                        groupNodeByDef[gDef] = groupNode;
                        nodes.Add(groupNode);
                        groupNode.nodeIndex = nodes.Count - 1;
                    }

                    var gEdge = new ResearchEdge { from = groupNode, to = node, isGroupEdge = true };
                    edges.Add(gEdge);
                    groupNode.nodeEdges.Add(gEdge);
                    node.nodeEdges.Add(gEdge);
                    groupNode.edgeCount++;
                    groupNode.childCount++;
                    node.edgeCount++;
                    groupNode.connectedNodes.Add(node);
                    node.connectedNodes.Add(groupNode);
                    node.cachedMass = 1f + Mathf.Pow(node.edgeCount, 1.2f) * 0.5f;
                }
                foreach (var gn in groupNodeByDef.Values)
                    gn.cachedMass = 1f + Mathf.Pow(gn.edgeCount, 1.2f) * 0.5f;
            }

            var anchor = nodes.FirstOrDefault(n => n.isPhantom);

            if (anchor == null)
            {
                anchor = nodes.Where(n => !n.isPhantom && !n.isGroupNode && n.def.IsFinished)
                              .OrderBy(n => n.def.prerequisites?.Count ?? 0)
                              .FirstOrDefault();
            }

            if (anchor == null)
            {
                anchor = nodes.Where(n => !n.isPhantom && !n.isGroupNode)
                              .OrderBy(n => n.def.prerequisites?.Count ?? 0)
                              .FirstOrDefault();
            }

            if (previouslySelectedDef != null)
            {
                selectedNode = nodes.FirstOrDefault(n => !n.isPhantom && n.def == previouslySelectedDef);
            }

            if (selectedNode == null && selectedProject != null)
            {
                selectedNode = nodes.FirstOrDefault(n => !n.isPhantom && n.def == selectedProject);
            }
            else if (selectedNode == null && Find.ResearchManager.currentProj != null)
            {
                selectedNode = nodes.FirstOrDefault(n => !n.isPhantom && n.def == Find.ResearchManager.currentProj);
            }

            if (instant)
            {
                InitPhysicsLayout();
                string key = $"{CurTab.defName}_{currentEra}";
                if (cachedCameraOffsets.TryGetValue(key, out var savedOffset))
                {
                    cameraOffset = savedOffset;

                    if (nodes.Any())
                    {
                        var min = new Vector2(nodes.Min(n => n.pos.x), nodes.Min(n => n.pos.y));
                        var max = new Vector2(nodes.Max(n => n.pos.x), nodes.Max(n => n.pos.y));
                        var bounds = new Rect(min.x, min.y, max.x - min.x, max.y - min.y).ExpandedBy(1000f);
                        if (!bounds.Contains(-cameraOffset))
                        {
                            cameraOffset = ComputeCentroidOffset();
                            cachedCameraOffsets[key] = cameraOffset;
                        }
                    }
                }
                else
                {
                    cameraOffset = ComputeCentroidOffset();
                    cachedCameraOffsets[key] = cameraOffset;
                }
            }
        }

        private void SeedHierarchicalPositions()
        {
            var foundations = nodes.Where(n => n.isFoundation && !n.isPhantom).ToList();
            if (foundations.Count == 0) return;

            float foundationRadius = 700f * Mathf.Sqrt(Mathf.Max(1, foundations.Count));
            for (int i = 0; i < foundations.Count; i++)
            {
                float angle = (float)i / foundations.Count * Mathf.PI * 2f;
                foundations[i].pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * foundationRadius;
            }

            var nodeSet = new HashSet<ResearchNode>(nodes.Where(n => !n.isPhantom && !n.isGroupNode));
            var placed = new HashSet<ResearchNode>();
            var pendingParentCount = new Dictionary<ResearchNode, int>();

            foreach (var node in nodeSet)
            {
                if (node.isFoundation) { placed.Add(node); continue; }
                int inCount = 0;
                foreach (var edge in node.nodeEdges)
                    if (edge.to == node && nodeSet.Contains(edge.from)) inCount++;
                pendingParentCount[node] = inCount;
            }

            var ready = new Queue<ResearchNode>(foundations);

            while (ready.Count > 0)
            {
                var current = ready.Dequeue();

                foreach (var edge in current.nodeEdges)
                {
                    if (edge.from != current) continue;
                    var child = edge.to;
                    if (!nodeSet.Contains(child) || placed.Contains(child)) continue;

                    pendingParentCount[child]--;
                    if (pendingParentCount[child] > 0) continue;

                    Vector2 parentCentroid = Vector2.zero;
                    int parentCount = 0;
                    foreach (var ce in child.nodeEdges)
                    {
                        if (ce.to != child) continue;
                        if (!placed.Contains(ce.from)) continue;
                        parentCentroid += ce.from.pos;
                        parentCount++;
                    }
                    if (parentCount > 0) parentCentroid /= parentCount;

                    Vector2 outwardDir = Rand.UnitVector2;

                    child.pos = parentCentroid + outwardDir * 150f
                        + new Vector2(Rand.Range(-20f, 20f), Rand.Range(-20f, 20f));
                    placed.Add(child);
                    ready.Enqueue(child);
                }
            }

            foreach (var node in nodeSet)
            {
                if (placed.Contains(node)) continue;
                node.pos = new Vector2(Rand.Range(-200f, 200f), Rand.Range(-200f, 200f));
            }

            foreach (var node in nodes)
                if (!node.isPhantom)
                    State.nodePositions[node.cachedKey] = node.pos;
        }

        private NodeState GetNodeState(ResearchProjectDef def)
        {
            if (!GodModeReveal)
            {
                if (BetterResearchMenuMod.settings.restrictViewingFutureTechLevels && def.techLevel > Faction.OfPlayer.def.techLevel)
                    return NodeState.Hidden;

                if (BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted)
                    return NodeState.Hidden;

                if (def.HasModExtension<EmergenceExtension>())
                {
                    if (!BetterResearchMenuMod.settings.enableEmergence) return NodeState.Hidden;
                    if (def.techLevel < State.startingScenarioTechLevel) return NodeState.Hidden;
                    var ext = def.GetModExtension<EmergenceExtension>();
                    if (ext.targetLevel <= State.startingScenarioTechLevel) return NodeState.Hidden;
                    if (def.techLevel != currentEra && currentEra != TechLevel.Undefined) return NodeState.Hidden;
                    GetAdvancementProgressRaw(def.techLevel, DefsOf.Main, out int finished, out int total);
                    if (finished != total) return NodeState.Hidden;
                }
            }

            if (State.nodeStates.TryGetValue(def.defName, out var state))
                return state;

            if (def.IsFinished)
            {
                if (BetterResearchMenuMod.settings.neverCollapseFoundations && def.IsFoundation()) return NodeState.Expanded;
                return BetterResearchMenuMod.settings.startCollapsed ? NodeState.Minimized : NodeState.Expanded;
            }
            bool isOrphan = def.prerequisites.NullOrEmpty() && def.hiddenPrerequisites.NullOrEmpty();
            if (isOrphan && !def.IsFinished)
            {
                if (BetterResearchMenuMod.settings.neverCollapseFoundations && def.IsFoundation()) return NodeState.Expanded;
                return NodeState.Minimized;
            }
            if (def.PrerequisitesCompleted) return NodeState.Dot;

            return NodeState.Expanded;
        }

        private void InitPhysicsLayout()
        {
            physicsTemperature = 100f;
            var activeCount = nodes.Count(n => n.state != NodeState.Hidden);
            if (activeCount == 0) return;

            for (int i = 0; i < 5000; i++)
            {
                PhysicsTick(0.04f, true);

                if (i > 150 && (velocitySum / activeCount) < 0.00001f)
                {
                    Log.Message($"[BRM] Layout settled at {i}");
                    break;
                }
            }

            physicsTemperature = 0f;
            foreach (var node in nodes)
            {
                node.velocity = Vector2.zero;
                node.drawPos = node.pos;
                if (!node.isPhantom) State.nodePositions[node.cachedKey] = node.pos;
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (lastCurTab != CurTab)
            {
                lastCurTab = CurTab;
                currentEra = CurTab == DefsOf.Main ? Faction.OfPlayer.def.techLevel : TechLevel.Undefined;
                zoom = 1f;
                InitPhysics(true);
            }

            bool currentGodMode = GodModeReveal;
            if (currentGodMode != lastGodMode)
            {
                lastGodMode = currentGodMode;
                OnGodModeChanged(currentGodMode);
            }

            if (selectedProject != null && (selectedNode == null || selectedNode.def != selectedProject))
            {
                selectedNode = nodes.FirstOrDefault(n => !n.isPhantom && n.def == selectedProject);
            }
            else if (selectedNode != null && selectedProject != selectedNode.def)
            {
                selectedProject = selectedNode.def;
            }

            int currentStateHash = 0;
            var allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                if (allDefs[i].IsFinished) currentStateHash++;
                if (allDefs[i].PrerequisitesCompleted) currentStateHash++;
            }

            if (lastStateCheckHash != currentStateHash)
            {
                if (lastStateCheckHash != -1)
                {
                    InitPhysics(false);
                    physicsTemperature = Mathf.Max(physicsTemperature, 100f);
                }
                lastStateCheckHash = currentStateHash;
                foreach (var node in nodes)
                {
                    if (node.isPhantom || node.isGroupNode) continue;
                    node.isFinishedCache = node.def.IsFinished;
                    node.canStartNowCache = node.def.CanStartNow;
                    node.isLockedCache = !node.canStartNowCache && !node.isFinishedCache;
                }
            }

            currentBgColor = Color.Lerp(currentBgColor, CurTab == DefsOf.Anomaly ? ColorAnomalyBackground : CurTab == DefsOf.VGE_Gravtech ? ColorVGEBackground : ColorGraphBackground, Time.deltaTime * 5f);

            if (wasDraggingNode && hasSignificantDrag)
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
                    node.drawPos = Vector2.SmoothDamp(node.drawPos, node.pos, ref node.dampVelocity, 0.05f);
                }
            }

            if (physicsTemperature <= 0.5f)
            {
                foreach (var node in nodes)
                    node.drawPos = node.pos;
            }
        }

        private void OnGodModeChanged(bool godModeOn)
        {
            bool anyChanged = false;

            if (godModeOn)
            {
                godModeStateSnapshot = new Dictionary<string, NodeState>(State.nodeStates);

                foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                {
                    if (def.tab != CurTab) continue;
                    bool wouldBeHidden =
                        (BetterResearchMenuMod.settings.restrictViewingFutureTechLevels && def.techLevel > Faction.OfPlayer.def.techLevel) ||
                        (BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted) ||
                        def.HasModExtension<EmergenceExtension>();
                    if (wouldBeHidden)
                    {
                        State.nodeStates[def.defName] = NodeState.Expanded;
                        anyChanged = true;
                    }
                }
            }
            else
            {
                if (godModeStateSnapshot != null)
                {
                    foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                    {
                        if (def.tab != CurTab) continue;
                        bool wouldBeHidden =
                            (BetterResearchMenuMod.settings.restrictViewingFutureTechLevels && def.techLevel > Faction.OfPlayer.def.techLevel) ||
                            (BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted) ||
                            def.HasModExtension<EmergenceExtension>();
                        if (!wouldBeHidden) continue;

                        if (godModeStateSnapshot.TryGetValue(def.defName, out var original))
                            State.nodeStates[def.defName] = original;
                        else
                            State.nodeStates.Remove(def.defName);
                        anyChanged = true;
                    }
                    godModeStateSnapshot = null;
                }
            }

            if (anyChanged)
            {
                InitPhysics(false);
                physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            }
        }

        private void PhysicsTick(float dt, bool ignoreSettings = false)
        {
            if (!BetterResearchMenuMod.settings.physicsEnabled && !ignoreSettings) { physicsTemperature = 0f; return; }
            if (physicsTemperature < 0.01f) { physicsTemperature = 0f; velocitySum = 0f; return; }

            velocitySum = 0f;
            int nodeCount = nodes.Count;
            int movedCount = 0;
            Vector2 netMovement = Vector2.zero;

            for (int ni = 0; ni < nodeCount; ni++)
            {
                var node = nodes[ni];
                if (node.isDragging || node.state == NodeState.Hidden || node.isAnchor || node.isPhantom)
                {
                    node.velocity = Vector2.zero;
                    continue;
                }

                Vector2 repulsion = Vector2.zero;
                Vector2 attraction = Vector2.zero;
                bool isCollapsed = node.state == NodeState.Dot || node.state == NodeState.Minimized;

                for (int oi = 0; oi < nodeCount; oi++)
                {
                    if (ni == oi) continue;
                    var other = nodes[oi];
                    if (other.state == NodeState.Hidden) continue;

                    Vector2 dir = node.pos - other.pos;
                    float dist = dir.magnitude;
                    if (dist < 1f) { dir = Rand.UnitVector2; dist = 1f; }

                    float k = 500f * BetterResearchMenuMod.settings.spacingForceMultiplier;
                    float weight = (1f + Mathf.Sqrt(node.childCount) * 0.5f) * (1f + Mathf.Sqrt(other.childCount) * 0.5f);

                    float forceMag = (k * k / dist) * 0.2f * weight;
                    if (node.connectedNodes.Contains(other)) forceMag *= 0.4f;

                    bool otherIsCollapsed = other.state == NodeState.Dot || other.state == NodeState.Minimized;
                    if (isCollapsed && !otherIsCollapsed) forceMag *= 0.15f;
                    else if (isCollapsed && otherIsCollapsed) forceMag *= 2.0f;

                    repulsion += (dir / dist) * forceMag;
                }

                foreach (var edge in node.nodeEdges)
                {
                    var other = (edge.from == node) ? edge.to : edge.from;
                    if (other.state == NodeState.Hidden) continue;

                    Vector2 dir = other.pos - node.pos;
                    float dist = dir.magnitude;
                    if (dist < 10f) continue;

                    const float k_att = 200f;
                    float attMul = 1f;

                    if (isCollapsed)
                    {
                        bool isParentEdge = edge.to == node;
                        attMul = isParentEdge ? 30f : 6f;
                    }

                    attraction += (dir / dist) * (dist * dist / k_att) * attMul * BetterResearchMenuMod.settings.contractingForceMultiplier;
                }

                float isolationFactor = Mathf.Exp(-node.edgeCount * 0.4f);
                Vector2 centerForce = -node.pos * 1.5f * BetterResearchMenuMod.settings.centerForceMultiplier * isolationFactor;

                Vector2 totalForce = repulsion + attraction + centerForce;

                node.lastRepulsionForce = repulsion;
                node.lastAttractionForce = attraction;
                node.lastCenterForce = centerForce;

                node.velocity = (node.velocity + (totalForce / node.cachedMass) * dt) * 0.75f;

                float speed = node.velocity.magnitude;
                if (speed > physicsTemperature)
                    node.velocity *= physicsTemperature / speed;

                velocitySum += node.velocity.sqrMagnitude;
            }

            foreach (var node in nodes)
            {
                if (node.isDragging || node.state == NodeState.Hidden || node.isAnchor || node.isPhantom) continue;

                Vector2 move = node.velocity * dt * 8f;
                node.pos += move;
                netMovement += move;
                movedCount++;
            }

            if (movedCount > 0)
            {
                Vector2 drift = netMovement / movedCount;
                foreach (var node in nodes)
                {
                    if (node.isDragging || node.state == NodeState.Hidden || node.isAnchor || node.isPhantom) continue;
                    node.pos -= drift;
                    if (!ignoreSettings) State.nodePositions[node.cachedKey] = node.pos;
                }
            }

            if (ignoreSettings) physicsTemperature *= 0.99f;
            else
            {
                physicsTemperature *= 0.997f;
                if (wasDraggingNode || hasSignificantDrag)
                    physicsTemperature = Mathf.Max(physicsTemperature, 20f);
            }

            if (debugLogNextTick)
            {
                debugLogNextTick = false;
                var topMovers = nodes
                    .Where(n => n.state != NodeState.Hidden && !n.isDragging && !n.isPhantom)
                    .OrderByDescending(n => n.velocity.sqrMagnitude)
                    .Take(3)
                    .ToList();
                foreach (var n in topMovers)
                {
                    Log.Message($"[BRM TopMover] {n.def.defName}: vel={n.velocity.magnitude:F4}, pos=({n.pos.x:F1},{n.pos.y:F1}), edgeCount={n.edgeCount}, isFoundation={n.isFoundation}\n" +
                        $"  Rep: {n.lastRepulsionForce.magnitude:F1}, Att: {n.lastAttractionForce.magnitude:F1}, Ctr: {n.lastCenterForce.magnitude:F1}");
                }
                Log.Message($"[BRM PostReheat] physicsTemperature={physicsTemperature:F4}, velocitySum={velocitySum:F6}");
            }
        }

        private float velocitySum = 0f;
        private HashSet<(ResearchNode, ResearchNode)> phantomEdgeSet = new HashSet<(ResearchNode, ResearchNode)>();
        private float[] nodeRadiusCache = new float[0];

        private Vector2 ComputeCentroidOffset()
        {
            var visible = nodes.Where(n => !n.isPhantom && n.state != NodeState.Hidden).ToList();
            if (visible.Count == 0) return Vector2.zero;
            float cx = 0f, cy = 0f;
            for (int i = 0; i < visible.Count; i++) { cx += visible[i].pos.x; cy += visible[i].pos.y; }
            return new Vector2(-cx / visible.Count, -cy / visible.Count);
        }

        public override void DoWindowContents(Rect inRect)
        {
            activeProjectsCacheDirty = true;

            var targetBgColor = CurTab == DefsOf.Anomaly ? ColorAnomalyBackground : CurTab == DefsOf.VGE_Gravtech ? ColorVGEBackground : ColorGraphBackground;
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

            float leftBarShift = LeftBarVisible ? 45f : 5f;

            var controlAreaRect = new Rect(leftBarShift + 5f, graphRect.height - 145f, 250f, 160f);
            float searchBarWidth = 200f;
            float searchBarHeight = 24f;
            var searchBarRect = new Rect(graphRect.width - searchBarWidth - 4f, 4f, searchBarWidth, searchBarHeight);
            var pivot = new Vector2(graphRect.width / 2f, (inRect.height - TopBarHeight - 40f) / 2f);

            var panelRect = new Rect(inRect.width - RightPanelWidth, TopBarHeight, RightPanelWidth, graphRect.height);

            bool searchActive = quickSearchWidget.filter.Active;
            foreach (var node in nodes)
            {
                node.matchesSearchCache = node.isPhantom || node.isGroupNode || !searchActive
                    || (node.def != null && matchingProjects.Contains(node.def));
            }

            HandleInputs(graphRect, controlAreaRect, panelRect, searchBarRect, inRect);

            var mousePos = Event.current.mousePosition;
            var localMousePos = mousePos - new Vector2(graphRect.x, graphRect.y);

            bool mouseInPanel = selectedNode != null && panelRect.Contains(Event.current.mousePosition);
            bool mouseOverLeftBar = LeftBarVisible && new Rect(0f, TopBarHeight, 55f, inRect.height - TopBarHeight - BottomBarHeight).Contains(mousePos);
            bool mouseOverAdvance = false;
            if (LeftBarVisible)
            {
                var advanceBtnRect = new Rect(50f + 15f, TopBarHeight + 15f, 200f, 40f);
                if (advanceBtnRect.Contains(mousePos)) mouseOverAdvance = true;
            }

            if (!mouseInPanel && !mouseOverLeftBar && !mouseOverAdvance && !controlAreaRect.Contains(localMousePos) && !searchBarRect.Contains(localMousePos) && graphRect.Contains(mousePos))
            {
                ResearchNode hoveredNode = null;
                for (var i = nodes.Count - 1; i >= 0; i--)
                {
                    var node = nodes[i];
                    if (node.state == NodeState.Hidden) continue;
                    if (node.isPhantom || node.isGroupNode) continue;
                    if (!node.matchesSearchCache) continue;
                    var screenPos = (node.drawPos + cameraOffset) * zoom + pivot;

                    bool isFoundation = node.isFoundation;
                    bool isEmergence = node.isEmergence;
                    float nodeSize;

                    if (node.state == NodeState.Dot || node.state == NodeState.Minimized)
                    {
                        var hitSize = NodeSizeMinimized * zoom;
                        var isHovering = Vector2.Distance(screenPos, localMousePos) < hitSize / 2f;
                        bool isLocked = node.isLockedCache;
                        var drawState = (node.state == NodeState.Minimized || isHovering || isLocked) ? NodeState.Minimized : NodeState.Dot;

                        float baseSize = drawState == NodeState.Minimized ? NodeSizeMinimized : NodeSizeDot;
                        nodeSize = node.GetNodeSize(baseSize, drawState) * zoom;
                        if (isEmergence && drawState == NodeState.Minimized) nodeSize *= 1.5f;
                    }
                    else
                    {
                        nodeSize = (isFoundation || isEmergence ? NodeSizeExpanded * 2f : NodeSizeExpanded) * zoom;
                    }

                    if (Vector2.Distance(screenPos, localMousePos) < nodeSize / 2f)
                    {
                        hoveredNode = node;
                        break;
                    }
                }

                if (!selectionLocked && hoveredNode != null && !isPanning && !wasDraggingNode)
                {
                    selectedNode = hoveredNode;
                    selectedProject = hoveredNode.def;
                }

                if (Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1 || Event.current.button == 2))
                {
                    if (hoveredNode != null && Event.current.button != 2)
                    {
                        if (Event.current.button == 0)
                        {
                            selectedNode = hoveredNode;
                            selectedProject = hoveredNode.def;

                            hoveredNode.isDragging = true;
                            wasDraggingNode = true;
                            hasSignificantDrag = false;
                            dragStartMousePos = localMousePos;
                            var worldMousePos = ((localMousePos - pivot) / zoom) - cameraOffset;
                            dragOffset = hoveredNode.pos - worldMousePos;
                        }
                        else if (Event.current.button == 1)
                        {
                            CollapseNode(hoveredNode);
                        }
                        Event.current.Use();
                    }
                    else if (Event.current.button != 1)
                    {
                        if (Event.current.button == 0 && hoveredNode == null)
                        {
                            selectionLocked = false;
                            selectedNode = null;
                            selectedProject = null;
                        }
                        isPanning = true;
                        Event.current.Use();
                    }
                }
            }

            if (Event.current.rawType == EventType.MouseUp)
            {
                isPanning = false;
                if (wasDraggingNode)
                {
                    if (Event.current.button == 0 && selectedNode != null && Vector2.Distance(localMousePos, dragStartMousePos) < 5f)
                    {
                        selectionLocked = true;
                        if (selectedNode.state == NodeState.Minimized || selectedNode.state == NodeState.Dot)
                        {
                            if (!GodModeReveal && selectedNode.isLockedCache)
                            {
                                var reasons = GetLockedReasons(selectedNode.def);
                                Messages.Message("Locked".Translate() + (reasons.Count > 0 ? ": " + reasons[0] : ""), MessageTypeDefOf.RejectInput, false);
                            }
                            else
                            {
                                selectedNode.state = NodeState.Expanded;
                                State.nodeStates[selectedNode.def.defName] = selectedNode.state;

                                if (BetterResearchMenuMod.settings.maxExpandedNodes > 0)
                                {
                                    State.expandedNodeOrder.Remove(selectedNode.def.defName);
                                    State.expandedNodeOrder.Add(selectedNode.def.defName);

                                    while (State.expandedNodeOrder.Count > BetterResearchMenuMod.settings.maxExpandedNodes)
                                    {
                                        var oldestDefName = State.expandedNodeOrder[0];
                                        State.expandedNodeOrder.RemoveAt(0);
                                        if (State.nodeStates.TryGetValue(oldestDefName, out var s) && s == NodeState.Expanded)
                                        {
                                            State.nodeStates[oldestDefName] = NodeState.Minimized;
                                            var oldNode = nodes.FirstOrDefault(n => !n.isPhantom && n.def.defName == oldestDefName);
                                            if (oldNode != null) oldNode.state = NodeState.Minimized;
                                        }
                                    }
                                }

                                physicsTemperature = Mathf.Max(physicsTemperature, 100f);
                                DefsOf.BRM_ExpandingNode.PlayOneShotOnCamera();

                                var visibleChildren = selectedNode.nodeEdges.Where(e => e.from == selectedNode && e.to.state != NodeState.Hidden).Select(e => e.to).ToList();
                                for (int i = 0; i < visibleChildren.Count; i++)
                                {
                                    var child = visibleChildren[i];
                                    var outDir = (child.pos - selectedNode.pos);
                                    if (outDir.sqrMagnitude < 0.01f)
                                    {
                                        float angle = ((float)i / visibleChildren.Count) * Mathf.PI * 2f;
                                        outDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                                    }
                                    else
                                    {
                                        outDir = outDir.normalized;
                                    }
                                    child.velocity += outDir * 300f;
                                }
                            }
                        }
                        else if (selectedNode.state == NodeState.Expanded)
                        {
                            if (!GodModeReveal && selectedNode.isLockedCache)
                            {
                                var reasons = GetLockedReasons(selectedNode.def);
                                Messages.Message("Locked".Translate() + (reasons.Count > 0 ? ": " + reasons[0] : ""), MessageTypeDefOf.RejectInput, false);
                            }
                            else if (selectedNode.canStartNowCache && !GetActiveProjectsCached(CurTab).Contains(selectedNode.def))
                            {
                                AttemptBeginResearch(selectedNode.def);
                            }
                        }
                    }
                    if (hasSignificantDrag)
                    {
                        physicsTemperature = Mathf.Max(physicsTemperature, 100f);
                    }
                }

                wasDraggingNode = false;
                hasSignificantDrag = false;
                foreach (var node in nodes)
                    node.isDragging = false;
            }

            foreach (var node in nodes)
            {
                if (node.isDragging && Event.current.type == EventType.MouseDrag)
                {
                    hasSignificantDrag = true;
                    node.pos = ((localMousePos - pivot) / zoom) - cameraOffset + dragOffset;
                    node.velocity = Vector2.zero;
                    node.dampVelocity = Vector2.zero;
                    State.nodePositions[node.cachedKey] = node.pos;
                    physicsTemperature = Mathf.Max(physicsTemperature, 100f);
                }
            }

            Widgets.BeginGroup(graphRect);
            DrawGraphControls(controlAreaRect);
            quickSearchWidget.OnGUI(searchBarRect, UpdateSearchResults);

            Vector2 WorldToScreen(Vector2 worldPos)
            {
                return (worldPos + cameraOffset) * zoom + pivot;
            }

            var viewPort = new Rect(-200f, -200f, graphRect.width + 400f, graphRect.height + 400f);
            var activeProjects = GetActiveProjectsCached(CurTab);
            foreach (var edge in edges)
            {
                if (edge.from.state == NodeState.Hidden || edge.to.state == NodeState.Hidden)
                    continue;
                if (!edge.from.matchesSearchCache || !edge.to.matchesSearchCache) continue;

                Color edgeColor;
                float edgeThickness;
                if (edge.isGroupEdge)
                {
                    edgeColor = new Color(0.65f, 0.65f, 0.45f, 0.35f);
                    edgeThickness = 1f * zoom;
                }
                else
                {
                    var isFinished = !edge.from.isPhantom && edge.from.isFinishedCache;
                    edgeColor = isFinished ? ColorEdgeFinished : ColorEdgeUnfinished;
                    edgeThickness = (isFinished ? ThicknessFinished : ThicknessUnfinished) * zoom;
                }

                Vector2 fromPos = WorldToScreen(edge.from.drawPos);
                Vector2 toPos = WorldToScreen(edge.to.drawPos);
                Rect edgeBounds = new Rect(Mathf.Min(fromPos.x, toPos.x), Mathf.Min(fromPos.y, toPos.y), Mathf.Abs(fromPos.x - toPos.x), Mathf.Abs(fromPos.y - toPos.y));
                if (!viewPort.Overlaps(edgeBounds)) continue;

                Widgets.DrawLine(fromPos, toPos, edgeColor, edgeThickness);
            }

            foreach (var node in nodes)
            {
                if (node.state == NodeState.Hidden)
                    continue;
                if (!node.matchesSearchCache) continue;
                Vector2 screenPos = WorldToScreen(node.drawPos);
                if (!viewPort.Contains(screenPos)) continue;

                if (node.isGroupNode)
                {
                    var gSize = NodeSizeMinimized * zoom;
                    var gRect = new Rect(screenPos.x - gSize / 2f, screenPos.y - gSize / 2f, gSize, gSize);
                    GUI.color = new Color(0.75f, 0.75f, 0.5f, 0.75f);
                    GUI.DrawTexture(gRect, TexBubble);
                    GUI.color = Color.white;
                    var gTex = node.groupNodeDef.GetTexture();
                    if (gTex != null)
                        GUI.DrawTexture(gRect.ContractedBy(6f * zoom), gTex);
                    if (zoom > 0.4f)
                    {
                        Text.Anchor = TextAnchor.UpperCenter;
                        Text.Font = GameFont.Tiny;
                        float lw = 150f * zoom;
                        Widgets.Label(new Rect(screenPos.x - lw / 2f, screenPos.y + gSize / 2f + 2f * zoom, lw, 40f * zoom),
                            node.groupNodeDef.LabelCap);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    continue;
                }

                if (node.isPhantom)
                {
                    var size = NodeSizeMinimized * zoom;
                    var bubbleRect = new Rect(screenPos.x - size / 2f, screenPos.y - size / 2f, size, size);
                    GUI.color = new Color(0.6f, 0.7f, 1f, 0.85f);
                    GUI.DrawTexture(bubbleRect, TexBubble);
                    GUI.color = Color.white;
                    if (TechLevelIcons.TryGetValue(node.phantomEra, out var eraIcon))
                        GUI.DrawTexture(bubbleRect.ContractedBy(7f * zoom), eraIcon);
                    if (zoom > 0.4f)
                    {
                        Text.Anchor = TextAnchor.UpperCenter;
                        Text.Font = GameFont.Tiny;
                        float lw = 150f * zoom;
                        Widgets.Label(new Rect(screenPos.x - lw / 2f, screenPos.y + size / 2f + 2f * zoom, lw, 40f * zoom),
                            node.phantomEra.ToStringHuman().CapitalizeFirst());
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    continue;
                }

                bool isFoundation = node.isFoundation;
                bool isEmergence = node.isEmergence;
                bool isLocked = node.isLockedCache;

                if (node.state == NodeState.Dot || node.state == NodeState.Minimized)
                {
                    var hitSize = NodeSizeMinimized * zoom;
                    var isHovering = Vector2.Distance(screenPos, localMousePos) < hitSize / 2f;
                    var drawState = (node.state == NodeState.Minimized || isHovering || isLocked) ? NodeState.Minimized : NodeState.Dot;

                    float baseSize = drawState == NodeState.Minimized ? NodeSizeMinimized : NodeSizeDot;
                    var size = node.GetNodeSize(baseSize, drawState) * zoom;
                    if (isEmergence && drawState == NodeState.Minimized) size *= 1.5f;
                    var buttonRect = new Rect(screenPos.x - size / 2f, screenPos.y - size / 2f, size, size);

                    if (node.isFinishedCache)
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
                            State.openedNodes ??= new HashSet<string>();
                            if (isLocked)
                            {
                                GUI.DrawTexture(buttonRect.ContractedBy(4f * zoom), TexLock);
                            }
                            else if (node.isPhantom is false && State.openedNodes.Contains(node.def.defName))
                            {
                                DrawBubble(buttonRect, node.def, 4f * zoom, activeProjects, drawSilhouette: true);
                            }
                            else
                            {
                                Text.Anchor = TextAnchor.MiddleCenter;
                                Text.Font = zoom < 0.6f ? GameFont.Tiny : GameFont.Medium;
                                Widgets.Label(buttonRect, "?");
                            }
                        }
                    }
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    continue;
                }

                var nodeSize = (isFoundation || isEmergence ? NodeSizeExpanded * 2f : NodeSizeExpanded) * zoom;
                var nodeRect = new Rect(screenPos.x - nodeSize / 2f, screenPos.y - nodeSize / 2f, nodeSize, nodeSize);

                if (node == selectedNode)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(nodeRect.ExpandedBy(10f * zoom), TexBubble);
                    GUI.color = Color.white;
                }
                if (node.isPhantom is false)
                    State.openedNodes.Add(node.def.defName);
                var padding = (isFoundation || isEmergence) ? IconPadding * 2f : IconPadding;
                bool isSilhouetted = !node.isFinishedCache;
                DrawBubble(nodeRect, node.def, padding * zoom, activeProjects, drawSilhouette: isSilhouetted);

                if (zoom > 0.4f)
                {
                    Text.Anchor = TextAnchor.UpperCenter;
                    Text.Font = zoom < 0.6f ? GameFont.Tiny : (zoom > 1.2f ? GameFont.Medium : GameFont.Small);

                    float labelWidth = 200f * zoom;
                    float unscaledNodeSize = isFoundation || isEmergence ? NodeSizeExpanded * 2f : NodeSizeExpanded;
                    Rect labelRect = new Rect(screenPos.x - (labelWidth / 2f), screenPos.y + (unscaledNodeSize * zoom / 2f) + 5f * zoom, labelWidth, 200f * zoom);

                    Widgets.Label(labelRect, node.def.LabelCap);
                    float titleHeight = node.GetCachedTitleHeight(Text.Font, labelWidth, zoom);

                    Text.Font = GameFont.Tiny;
                    var subRect = new Rect(labelRect.x, labelRect.y + titleHeight, labelRect.width, labelRect.height);
                    Widgets.Label(subRect, node.cachedSubLabel);
                }
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndGroup();

            if (CurTab == DefsOf.Main)
            {
                DrawAdvanceButton(inRect);
                DrawTopBar(inRect);
                DrawLeftBar(inRect);
            }
            DrawBottomBar(inRect);
            if (selectedNode != null && selectedNode.isPhantom is false)
                DrawRightPanel(inRect);
        }

        private void HandleInputs(Rect graphRect, Rect sliderExcl, Rect panelExcl, Rect searchBarExcl, Rect inRect)
        {
            float zoomSensitivity = 0.05f;
            float minZoom = 0.05f;
            float maxZoom = 3f;

            Vector2 mousePos = Event.current.mousePosition;
            if (panelExcl.Contains(mousePos)) return;

            if (LeftBarVisible)
            {
                float leftBarWidth = 35f;
                var leftBarRect = new Rect(0f, TopBarHeight, leftBarWidth + 5f, inRect.height - TopBarHeight - BottomBarHeight);
                var advanceBtnRect = new Rect(leftBarWidth + 15f, TopBarHeight + 15f, 200f, 40f);
                if (leftBarRect.Contains(mousePos) || advanceBtnRect.Contains(mousePos))
                {
                    return;
                }
            }

            Vector2 localMousePos = mousePos - new Vector2(graphRect.x, graphRect.y);

            if (Event.current.type == EventType.MouseDown)
                lastMousePos = localMousePos;
            if (Event.current.type == EventType.ScrollWheel && graphRect.Contains(Event.current.mousePosition))
            {
                zoom -= Event.current.delta.y * zoomSensitivity;
                zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
                Event.current.Use();
            }

            if (!sliderExcl.Contains(localMousePos) && !searchBarExcl.Contains(localMousePos) && isPanning && Event.current.type == EventType.MouseDrag)
            {
                cameraOffset += (localMousePos - lastMousePos) / zoom;
                lastMousePos = localMousePos;
                cachedCameraOffsets[$"{CurTab.defName}_{currentEra}"] = cameraOffset;
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

            if (cachedErasWithProjects == null || lastErasTab != CurTab)
            {
                cachedErasWithProjects = AllTechLevels.Where(tl => DefDatabase<ResearchProjectDef>.AllDefs.Any(x => (x.techLevel == tl || x.techLevel == TechLevel.Undefined && tl == Faction.OfPlayer.def.techLevel) && x.tab == CurTab)).ToList();
                cachedErasWithProjects.Insert(0, TechLevel.Undefined);
                lastErasTab = CurTab;
            }

            var erasWithProjects = cachedErasWithProjects;
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
                var (allProjectsInEra, finishedInEra) = GetTopBarData(techLevel);
                if (TechLevelIcons.TryGetValue(techLevel, out var icon))
                    GUI.DrawTexture(new Rect(innerRect.x + iconMargin, innerRect.y, iconSize, iconSize), icon);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (Widgets.ButtonInvisible(segRect))
                {
                    if (techLevel != TechLevel.Undefined && BetterResearchMenuMod.settings.restrictResearchToTechLevel && techLevel > Faction.OfPlayer.def.techLevel && !GodModeReveal)
                    {
                        Messages.Message("BRM_CannotAccessEra".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                    else if (techLevel != currentEra)
                    {
                        currentEra = techLevel;
                        selectionLocked = false;
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
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawAdvanceButton(Rect inRect)
        {
            if (!LeftBarVisible) return;
            if (BetterResearchMenuMod.settings.enableEmergence) return;

            float leftBarWidth = 35f;
            var playerEra = Faction.OfPlayer.def.techLevel;
            var leftRect = new Rect(0f, TopBarHeight, leftBarWidth, inRect.height - TopBarHeight - BottomBarHeight);
            var progress = GetAdvancementProgress(out var finished, out var total);

            float threshold = BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.EraCompletion ? BetterResearchMenuMod.settings.eraCompletionPercentage : 1f;
            if (progress >= threshold && playerEra < TechLevel.Archotech)
            {
                var nextEra = (TechLevel)((int)playerEra + 1);
                var advanceBtnRect = new Rect(leftRect.xMax + 15f, leftRect.y + 15f, 200f, 40f);
                if (Widgets.ButtonText(advanceBtnRect, "BRM_AdvanceTo".Translate(nextEra.ToStringHuman().CapitalizeFirst())))
                {
                    Faction.OfPlayer.def.techLevel = nextEra;
                    Find.WindowStack.Add(new Window_TechAdvance(nextEra));
                    var hasProjectsInNextEra = DefDatabase<ResearchProjectDef>.AllDefs.Any(x => x.techLevel == nextEra && x.tab == CurTab);
                    if (hasProjectsInNextEra)
                        currentEra = nextEra;
                    InitPhysics(true);
                    DefsOf.BRM_Advancement.PlayOneShotOnCamera();
                }
            }
        }

        private void DrawLeftBar(Rect inRect)
        {
            if (LeftBarVisible)
            {
                float leftBarWidth = 35f;
                float iconSize = 40f;
                float iconMargin = 5f;
                float labelWidth = 200f;
                float labelHeight = 50f;

                var playerEra = Faction.OfPlayer.def.techLevel;
                var leftRect = new Rect(0f, TopBarHeight, leftBarWidth, inRect.height - TopBarHeight - BottomBarHeight);
                Widgets.DrawBoxSolid(leftRect, ColorLeftBarBackground);
                var progress = GetAdvancementProgress(out var finished, out var total);

                GUI.DrawTexture(leftRect, TexBarBg);
                var fillHeight = leftRect.height * progress;
                GUI.DrawTexture(new Rect(leftRect.x, leftRect.yMax - fillHeight, leftRect.width, fillHeight), TexBarFill);

                var nextEra = (TechLevel)((int)playerEra + 1);
                if (TechLevelIcons.TryGetValue(nextEra, out var nextIcon))
                {
                    var nextIconRect = new Rect(leftRect.center.x - iconSize / 2f, leftRect.y + iconMargin, iconSize, iconSize);
                    GUI.DrawTexture(nextIconRect, nextIcon);
                }

                if (TechLevelIcons.TryGetValue(playerEra, out var currentIcon))
                {
                    var currentIconRect = new Rect(leftRect.center.x - iconSize / 2f, leftRect.yMax - iconSize - iconMargin, iconSize, iconSize);
                    GUI.DrawTexture(currentIconRect, currentIcon);
                }

                Text.Font = GameFont.Small;
                if (progress < 1f)
                {
                    string transKey = BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.EraCompletion
                        ? "BRM_ProjectsComplete"
                        : "BRM_FoundationsComplete";

                    if (total == 0)
                    {
                        Widgets.Label(new Rect(leftRect.xMax + 5, leftRect.y, labelWidth, labelHeight), transKey.Translate(0, 0));
                    }
                    else
                    {
                        Widgets.Label(new Rect(leftRect.xMax + 5, leftRect.y, labelWidth, labelHeight), transKey.Translate(finished, total));
                    }
                }
            }
        }

        private void DrawRightPanel(Rect inRect)
        {
            float btnMargin = 5f;
            float btnSize = 24f;

            var panelRect = new Rect(inRect.width - RightPanelWidth, TopBarHeight, RightPanelWidth, inRect.height - TopBarHeight - BottomBarHeight);
            Widgets.DrawBoxSolid(panelRect, Widgets.WindowBGFillColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(new Rect(panelRect.x + btnMargin, panelRect.y + btnMargin, btnSize, btnSize), "—", drawBackground: false, textColor: Color.white, doMouseoverSound: true))
            {
                CollapseNode(selectedNode);
                selectedNode = null;
                selectedProject = null;
            }
            if (Widgets.ButtonText(new Rect(panelRect.xMax - btnSize - 5, panelRect.y + btnMargin, btnSize, btnSize), "x", drawBackground: false, textColor: Color.white, doMouseoverSound: true))
            {
                selectedNode = null;
                selectedProject = null;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (selectedNode is null) return;
            if (selectedProject != selectedNode.def)
            {
                selectedProject = selectedNode.def;
                descHeightCache.Clear();
            }
            var proj = selectedProject;

            float subY = panelRect.y + 50f;
            using (new TextBlock(GameFont.Medium, TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(panelRect.x + 10f, subY, panelRect.width - 20f, 50f), ref subY, selectedProject.LabelCap);
            }
            subY += 10f;
            if (!descHeightCache.TryGetValue(proj, out float dHeight))
            {
                string text = proj.Description;
                if (ModsConfig.AnomalyActive && proj.knowledgeCategory != null)
                    text += "\n\n" + "AnomalyResearchDescriptionHelpText".Translate().Colorize(ColoredText.SubtleGrayColor);
                dHeight = Text.CalcHeight(text, panelRect.width - 20f);
                descHeightCache[proj] = dHeight;
            }

            var descRect = new Rect(panelRect.x + 10f, subY, panelRect.width - 20f, dHeight);
            Widgets.Label(descRect, proj.Description);
            subY += dHeight;
            subY += 10f;
            Widgets.DrawLineHorizontal(panelRect.x + 2f, subY, panelRect.width - 4f, Color.gray);
            subY += 10f;
            if (ModsConfig.AnomalyActive && selectedProject.knowledgeCategory != null)
            {
                Widgets.Label(panelRect.x + 10f, ref subY, panelRect.width - 20f, "KnowledgeCategory".Translate() + ": " + selectedProject.knowledgeCategory.LabelCap);
            }
            var rect2 = new Rect(panelRect.x + 10f, subY, panelRect.width - 20f, 500f);
            DrawTechprintInfo(rect2, ref subY);

            var outRect = new Rect(panelRect.x + 10f, subY, panelRect.width - 20f, panelRect.height - subY - 70f);
            DrawProjectScrollView(outRect);

            var btnRect = new Rect(panelRect.x + 10f, panelRect.yMax - 60f, panelRect.width - 20f, 40f);
            DrawStartButton(btnRect);

            if (Prefs.DevMode && !proj.IsFinished)
            {
                if (Widgets.ButtonText(new Rect(btnRect.x, btnRect.y - 30f, btnRect.width, 25f), "Debug: Finish now"))
                {
                    Find.ResearchManager.FinishProject(proj);
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

            var activeProjs = GetActiveProjectsCached(CurTab);

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
                if (CurTab == DefsOf.Anomaly && proj.knowledgeCategory != null)
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
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawGraphControls(Rect controlAreaRect)
        {
            var physicsBtnRect = new Rect(controlAreaRect.x, controlAreaRect.y, ControlBtnSize, ControlBtnSize);
            var vanillaBtnRect = new Rect(physicsBtnRect.xMax + ControlBtnGap, controlAreaRect.y, ControlBtnSize, ControlBtnSize);

            if (Widgets.ButtonImage(physicsBtnRect, TexPhysics, BetterResearchMenuMod.settings.physicsEnabled ? Color.white : Color.gray))
            {
                BetterResearchMenuMod.settings.physicsEnabled = !BetterResearchMenuMod.settings.physicsEnabled;
                if (!BetterResearchMenuMod.settings.physicsEnabled)
                {
                    physicsTemperature = 0f;
                    foreach (var node in nodes)
                    {
                        node.velocity = Vector2.zero;
                        node.dampVelocity = Vector2.zero;
                        node.drawPos = node.pos;
                    }
                }
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(physicsBtnRect, "BRM_TogglePhysics".Translate());

            if (!BetterResearchMenuMod.settings.forbidVanillaMenu && Widgets.ButtonImage(vanillaBtnRect, TexVanilla))
            {
                Close();
                TabCollapser.Restore();
                var vanillaWindow = new MainTabWindow_Research();
                vanillaWindow.def = MainButtonDefOf.Research;
                Find.WindowStack.Add(vanillaWindow);
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(vanillaBtnRect, "BRM_OpenVanillaMenu".Translate());

            var reheatBtnRect = new Rect(vanillaBtnRect.xMax + ControlBtnGap, controlAreaRect.y, ControlBtnSize, ControlBtnSize);
            if (Widgets.ButtonImage(reheatBtnRect, TexPhysics, Color.yellow))
            {
                float maxVel = 0f;
                ResearchNode maxVelNode = null;
                foreach (var n in nodes)
                {
                    float v = n.velocity.sqrMagnitude;
                    if (v > maxVel) { maxVel = v; maxVelNode = n; }
                }
                Log.Message($"[BRM Reheat] physicsTemperature={physicsTemperature:F4}, velocitySum={velocitySum:F6}, maxNodeVelocity={Mathf.Sqrt(maxVel):F6} ({maxVelNode?.def?.defName ?? "null"})");
                physicsTemperature = 100f;
                debugLogNextTick = true;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(reheatBtnRect, "Reheat (debug)");

            var settingsBtnRect = new Rect(reheatBtnRect.xMax + ControlBtnGap, controlAreaRect.y, ControlBtnSize, ControlBtnSize);
            if (Widgets.ButtonImage(settingsBtnRect, TexSettings))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<BetterResearchMenuMod>()));
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(settingsBtnRect, "BRM_OpenSettings".Translate());

            float oldGrav = BetterResearchMenuMod.settings.centerForceMultiplier;
            float oldSpace = BetterResearchMenuMod.settings.spacingForceMultiplier;
            float oldCont = BetterResearchMenuMod.settings.contractingForceMultiplier;

            float sliderHeight = 22f;
            float verticalSpacing = 6f;
            float iconSize = 16f;
            float sliderWidth = controlAreaRect.width - 25f;

            var gravityRect = new Rect(controlAreaRect.x + 25f, controlAreaRect.y + ControlBtnSize + verticalSpacing, sliderWidth, sliderHeight);
            var spacingRect = new Rect(controlAreaRect.x + 25f, gravityRect.yMax + verticalSpacing, sliderWidth, sliderHeight);
            var contractingRect = new Rect(controlAreaRect.x + 25f, spacingRect.yMax + verticalSpacing, sliderWidth, sliderHeight);
            var stressTestRect = new Rect(controlAreaRect.x + 25f, contractingRect.yMax + verticalSpacing, sliderWidth, sliderHeight);

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
                BetterResearchMenuMod.instance.WriteSettings();
                physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            }
        }

        private void SetType(ResearchTabDef newTab)
        {
            if (CurTab == newTab) return;
            CurTab = newTab;
            lastCurTab = newTab;
            selectionLocked = false;
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

            var info = GetProjInfo(proj);
            var iconRect = rect.ContractedBy(iconPadding);
            Color? iconColor = drawSilhouette ? new Color(0.1f, 0.1f, 0.1f, 0.8f) : null;

            if (info.hasEmergence && TechLevelIcons.TryGetValue(info.emergenceLevel, out var tIcon))
            {
                if (iconColor.HasValue) GUI.color = iconColor.Value;
                Widgets.DrawTextureFitted(iconRect, tIcon, 1f);
                if (iconColor.HasValue) GUI.color = Color.white;
            }
            else if (info.hasCustomIcon && info.customTex != null)
            {
                if (iconColor.HasValue) GUI.color = iconColor.Value;
                Widgets.DrawTextureFitted(iconRect, info.customTex, 1f);
                if (iconColor.HasValue) GUI.color = Color.white;
            }
            else
            {
                Def iconDef = info.iconDef;
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

            if (info.markerTex != null)
            {
                float mSize = rect.width * 0.45f;
                GUI.DrawTexture(new Rect(rect.xMax - mSize * 0.9f, rect.y - mSize * 0.1f, mSize, mSize), info.markerTex);
            }
        }

        public override void Notify_ClickOutsideWindow()
        {
            base.Notify_ClickOutsideWindow();
            quickSearchWidget.Unfocus();
        }

        public static float GetAdvancementProgressRaw(TechLevel playerEra, ResearchTabDef tab, out int finished, out int total)
        {
            if (BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.Foundations)
            {
                var foundations = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => x.techLevel == playerEra && x.tab == tab && x.HasModExtension<ResearchFoundationExtension>()).ToList();
                total = foundations.Count;
                finished = foundations.Count(x => x.IsFinished);
            }
            else
            {
                var eraProjects = DefDatabase<ResearchProjectDef>.AllDefs.Where(x => x.techLevel == playerEra && x.tab == tab && !x.HasModExtension<EmergenceExtension>()).ToList();
                total = eraProjects.Count;
                finished = eraProjects.Count(x => x.IsFinished);
            }
            return total > 0 ? (float)finished / total : 1f;
        }

        private float GetAdvancementProgress(out int finished, out int total) => GetAdvancementProgressRaw(Faction.OfPlayer.def.techLevel, this.CurTab, out finished, out total);

        private List<string> GetLockedReasons(ResearchProjectDef proj)
        {
            var list = new List<string>();
            if (BetterResearchMenuMod.settings.restrictResearchToTechLevel && proj.techLevel > Faction.OfPlayer.def.techLevel && proj.tab != DefsOf.Anomaly && proj.tab != DefsOf.VGE_Gravtech)
            {
                list.Add("BRM_CannotAccessEra".Translate());
                return list;
            }

            if (proj.HasModExtension<EmergenceExtension>())
            {
                var progress = GetAdvancementProgressRaw(proj.techLevel, DefsOf.Main, out _, out _);
                float threshold = BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.EraCompletion ? BetterResearchMenuMod.settings.eraCompletionPercentage : 1f;
                if (progress < threshold)
                {
                    if (BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.Foundations) list.Add("BRM_RequiresAllFoundations".Translate());
                    else list.Add("BRM_RequiresEraCompletion".Translate(threshold.ToStringPercent()));
                }
            }

            var oldProj = selectedProject;
            var oldTab = curTabInt;
            selectedProject = proj;
            curTabInt = proj.tab;

            GUI.BeginGroup(new Rect(-9999f, -9999f, 1f, 1f));
            DrawStartButton(new Rect(0f, 0f, 1f, 1f));
            GUI.EndGroup();

            selectedProject = oldProj;
            curTabInt = oldTab;

            var reasons = lockedReasons;
            if (reasons != null && reasons.Count > 0)
                list.AddRange(reasons);

            return list;
        }
    }
}
