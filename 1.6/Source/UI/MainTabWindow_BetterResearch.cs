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
        public float cachedWeight;
        public float collisionRadius;

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

        public float RadiusMultiplier => (this.isFoundation || this.isEmergence) ? 1.95f : 1.0f;
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
            if (!this.isFoundation && !this.isEmergence) return baseSize;
            return currentState == NodeState.Minimized ? baseSize * 2.4375f : baseSize * 2.275f;
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
        public MainTabWindow_BetterResearch()
        {
			
			// Hitting "Enter" in the search bar would init hitting the "Accept" system in Rimworld; had to disable closing on accept to make "Enter to instant search" work
            closeOnAccept = false;
        }

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
            { TechLevel.Undefined, ContentFinder<Texture2D>.Get("UI/TechLevels/AllTab") },
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
        private const float ZoomThreshold = 0.1f;

        public static List<TechLevel> AllTechLevels = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Where(tl => tl != TechLevel.Undefined).ToList();
        private float TopBarHeight => CurTab == DefsOf.Main ? 45f : 0f;
        private float RightPanelWidth = 300f;
        private float NodeSizeExpanded = 80f;
        private float BottomBarHeight => GetActiveProjectsCached(CurTab).Count > 0 ? 80f : 40f;
        private const float QueueBarHeight = 36f;

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
        private int fastForwardTicks = 0;
        private bool[] adjacencyMatrixFlat;
        private static bool[] isHiddenCache = new bool[1000];
        private static bool[] isCollapsedCache = new bool[1000];
        private static bool[] isHubCache = new bool[1000];
        private static float[] weightCache = new float[1000];
        private static float[] radiusCache = new float[1000];
        private static float[] pX = new float[1000];
        private static float[] pY = new float[1000];
        private static float[] centerForceBaseCache = new float[1000];
        private static float[] invMassCache = new float[1000];
        private bool isPanning;
        private int queueDragIndex = -1;
        private Vector3 savedCameraPos;
        private float savedCameraSize;
        private float queueDragStartX;
        private float queueDragCurrentX;
        private float queueScrollOffset;
        private static Vector2 cameraOffset;
        private float zoom = 1f;
        private float targetZoom = 1f;
        private bool zoomAnchoring = false;
        private Vector2 zoomAnchorScreen;
        private Vector2 zoomAnchorWorld;
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
        private bool appliedSearchActive = false;

        public static bool sessionInitialized = false;

        private static bool HasAccessToAnomalyTab()
        {
            return Find.Anomaly.HighestLevelReached > 0 || (!Find.Anomaly.GenerateMonolith && Find.Storyteller.difficulty.AnomalyPlaystyleDef.enableAnomalyContent);
        }

        public static void ResetSession()
        {
            currentEra = TechLevel.Undefined;
            cachedCameraOffsets.Clear();
            seededLayoutKeys.Clear();
            sessionInitialized = false;
        }

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
        private string lastFilterText = "";
        private float lastSearchTypingTime = 0f;
        private bool searchPending = false;

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
                try
                {
                    list = proj.UnlockedDefs.ToList();
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce("[BetterResearchMenu] Failed to get unlocked defs for " + proj.defName + ". This project is likely broken by a mod conflict: " + ex, proj.defName.GetHashCode());
                    list = new List<Def>();
                }
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
                                && x.tab == CurTab && !x.HasModExtension<EmergenceExtension>())
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
            if (node == null || node.isGroupNode || node.isPhantom) return;
            node.state = NodeState.Minimized;
            State.nodeStates[node.def.defName] = node.state;
            node.velocity = Vector2.zero;

            physicsTemperature = Mathf.Max(physicsTemperature, 100f);
            DefsOf.BRM_CollapsingNode.PlayOneShotOnCamera();
        }

        public override void PreOpen()
        {
            Startup.Collapse();
            base.PreOpen();
            if (Find.CameraDriver != null)
            {
                savedCameraPos = Find.CameraDriver.rootPos;
                savedCameraSize = Find.CameraDriver.rootSize;
            }
            if (CurTab == null) CurTab = DefsOf.Main;
            lastCurTab = CurTab;
            appliedSearchActive = false;

            if (!sessionInitialized)
            {
                currentEra = TechLevel.Undefined;
                sessionInitialized = true;
                nodes.Clear();
                edges.Clear();
                godModeStateSnapshot = null;
                selectedNode = null;
                selectedProject = null;
                selectionLocked = false;
                lastStateCheckHash = -1;
                topBarDataCache.Clear();
                topBarCacheStateHash = -1;
                activeProjectsCache = null;
                activeProjectsCacheDirty = true;
                descHeightCache.Clear();
                cachedErasWithProjects = null;
                lastErasTab = null;
                lastFilterText = "";
                lastSearchTypingTime = 0f;
                searchPending = false;
                prevPanelWidth = 0f;
                physicsTemperature = 0f;
                fastForwardTicks = 0;
                zoom = 1f;
                targetZoom = 1f;
                zoomAnchoring = false;
            }

            InitPhysics(true);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (Find.CameraDriver != null)
            {
                Find.CameraDriver.SetRootPosAndSize(savedCameraPos, savedCameraSize);
            }
        }

        public void ForceEra(TechLevel era)
        {
            currentEra = era;
            selectionLocked = false;
            selectedNode = null;
            selectedProject = null;
            zoom = 1f;
            targetZoom = 1f;
            zoomAnchoring = false;
            InitPhysics(true);
        }

        public void InitPhysics(bool instant = false)
        {
            ResearchProjectDef previouslySelectedDef = selectedNode?.def;

            float techLevelSpacing = 350f;
            float randomOffset = 50f;

            foreach (var node in nodes)
            {
                if (!node.isPhantom && node.cachedKey != null)
                {
                    if (node.state == NodeState.Hidden)
                    {
                        State.nodePositions.Remove(node.cachedKey);
                    }
                    else
                    {
                        State.nodePositions[node.cachedKey] = node.pos;
                    }
                }
            }

            nodes.Clear();
            edges.Clear();

            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.tab != CurTab) continue;
                if (CurTab == DefsOf.Main && currentEra != TechLevel.Undefined && def.techLevel != currentEra) continue;

                var cacheKey = $"{def.defName}_{currentEra}";
                if (!GodModeReveal && BetterResearchMenuMod.settings.restrictViewingFutureProjects && !def.IsFinished && !def.PrerequisitesCompleted)
                {
                    State.nodePositions.Remove(cacheKey);
                    continue;
                }

                var node = new ResearchNode { def = def };
                node.isFoundation = def.IsFoundation();
                node.isEmergence = def.HasModExtension<EmergenceExtension>();
                node.state = GetNodeState(def);
                if (node.state == NodeState.Hidden)
                {
                    State.nodePositions.Remove(cacheKey);
                }

                node.isFinishedCache = def.IsFinished;
                node.canStartNowCache = def.CanStartNow;
                node.isLockedCache = !node.canStartNowCache && !node.isFinishedCache;
                node.cachedSubLabel = BuildNodeSubLabel(node);

                if (node.state != NodeState.Hidden && BetterResearchMenuMod.settings.autoRevealNodes)
                    (State.openedNodes ??= new HashSet<string>()).Add(def.defName);

                if (State.nodePositions.TryGetValue(cacheKey, out var savedPos))
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

            {
                int preGroupCount = nodes.Count;
                var activeGroupDefs = new HashSet<GroupNodeDef>();
                for (int gi = 0; gi < preGroupCount; gi++)
                {
                    var node = nodes[gi];
                    if (node.isPhantom || node.isGroupNode || node.def == null) continue;
                    var ext = node.def.GetModExtension<GroupParentExtension>();
                    if (ext?.groupNode == null) continue;
                    var gDef = ext.groupNode;
                    if (gDef.tab != null && gDef.tab != CurTab) continue;
                    activeGroupDefs.Add(gDef);
                }

                var toProcess = new Queue<GroupNodeDef>(activeGroupDefs);
                while (toProcess.Count > 0)
                {
                    var curr = toProcess.Dequeue();
                    if (curr.groupPrerequisites != null)
                    {
                        foreach (var prereq in curr.groupPrerequisites)
                        {
                            if (prereq.tab != null && prereq.tab != CurTab) continue;
                            if (activeGroupDefs.Add(prereq))
                            {
                                toProcess.Enqueue(prereq);
                            }
                        }
                    }
                }

                var groupNodeByDef = new Dictionary<GroupNodeDef, ResearchNode>();
                foreach (var gDef in activeGroupDefs)
                {
                    var groupNode = new ResearchNode
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

                for (int gi = 0; gi < preGroupCount; gi++)
                {
                    var node = nodes[gi];
                    if (node.isPhantom || node.isGroupNode || node.def == null) continue;
                    var ext = node.def.GetModExtension<GroupParentExtension>();
                    if (ext?.groupNode == null) continue;
                    var gDef = ext.groupNode;
                    if (groupNodeByDef.TryGetValue(gDef, out var groupNode))
                    {
                        var gEdge = new ResearchEdge { from = groupNode, to = node, isGroupEdge = true };
                        edges.Add(gEdge);
                        groupNode.nodeEdges.Add(gEdge);
                        node.nodeEdges.Add(gEdge);
                        groupNode.edgeCount++;
                        groupNode.childCount++;
                        node.edgeCount++;
                        groupNode.connectedNodes.Add(node);
                        node.connectedNodes.Add(groupNode);
                    }
                }

                foreach (var groupNode in groupNodeByDef.Values)
                {
                    if (groupNode.groupNodeDef.prerequisites != null)
                    {
                        foreach (var prereq in groupNode.groupNodeDef.prerequisites)
                        {
                            var parentNode = nodes.FirstOrDefault(n => !n.isPhantom && !n.isGroupNode && n.def == prereq);
                            if (parentNode != null)
                            {
                                var edge = new ResearchEdge { from = parentNode, to = groupNode, isGroupEdge = true };
                                edges.Add(edge);
                                parentNode.nodeEdges.Add(edge);
                                groupNode.nodeEdges.Add(edge);
                                parentNode.edgeCount++;
                                parentNode.childCount++;
                                groupNode.edgeCount++;
                                parentNode.connectedNodes.Add(groupNode);
                                groupNode.connectedNodes.Add(parentNode);
                            }
                        }
                    }

                    if (groupNode.groupNodeDef.groupPrerequisites != null)
                    {
                        foreach (var prereq in groupNode.groupNodeDef.groupPrerequisites)
                        {
                            if (groupNodeByDef.TryGetValue(prereq, out var parentGroupNode))
                            {
                                var edge = new ResearchEdge { from = parentGroupNode, to = groupNode, isGroupEdge = true };
                                edges.Add(edge);
                                parentGroupNode.nodeEdges.Add(edge);
                                groupNode.nodeEdges.Add(edge);
                                parentGroupNode.edgeCount++;
                                parentGroupNode.childCount++;
                                groupNode.edgeCount++;
                                parentGroupNode.connectedNodes.Add(groupNode);
                                groupNode.connectedNodes.Add(parentGroupNode);
                            }
                        }
                    }
                }
            }

            foreach (var groupNode in nodes.Where(n => n.isGroupNode))
            {
                if (!HasVisibleResearch(groupNode, new HashSet<ResearchNode>()))
                    groupNode.state = NodeState.Hidden;
            }

            if (CurTab == DefsOf.Main && currentEra != TechLevel.Undefined)
            {
                phantomEdgeSet.Clear();
                var phantomNodeByEra = new Dictionary<TechLevel, ResearchNode>();
                int nonPhantomCount = nodes.Count;
                for (int pi = 0; pi < nonPhantomCount; pi++)
                {
                    var node = nodes[pi];
                    if (node.isPhantom) continue;

                    List<ResearchProjectDef> prereqs = null;
                    if (node.isGroupNode && node.groupNodeDef.prerequisites != null)
                        prereqs = node.groupNodeDef.prerequisites;
                    else if (node.def != null && node.def.prerequisites != null)
                        prereqs = node.def.prerequisites;

                    if (prereqs == null) continue;

                    foreach (var prereq in prereqs)
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
                        }
                    }
                }
            }

            for (int i = 0; i < nodes.Count; i++) nodes[i].nodeIndex = i;
            adjacencyMatrixFlat = new bool[nodes.Count * nodes.Count];
            foreach (var edge in edges)
            {
                adjacencyMatrixFlat[edge.from.nodeIndex * nodes.Count + edge.to.nodeIndex] = true;
                adjacencyMatrixFlat[edge.to.nodeIndex * nodes.Count + edge.from.nodeIndex] = true;
            }

            foreach (var node in nodes)
            {
                node.cachedMass = 1f + Mathf.Pow(node.edgeCount, 1.2f) * 0.5f;
                node.cachedWeight = 1f + Mathf.Sqrt(node.childCount) * 0.5f;
            }

            string layoutKey = $"{CurTab?.defName}_{(int)currentEra}";
            bool wasSeeded = seededLayoutKeys.Contains(layoutKey);
            if (!wasSeeded)
            {
                SeedHierarchicalPositions();
                seededLayoutKeys.Add(layoutKey);
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
                if (!wasSeeded)
                {
                    InitPhysicsLayout();
                }
                string key = $"{CurTab.defName}_{currentEra}_{GodModeReveal}";
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
                            cachedCameraOffsets[$"{CurTab.defName}_{currentEra}_{GodModeReveal}"] = cameraOffset;
                        }
                    }
                }
                else
                {
                    cameraOffset = ComputeCentroidOffset();
                    cachedCameraOffsets[$"{CurTab.defName}_{currentEra}_{GodModeReveal}"] = cameraOffset;
                }
            }
        }

        private void SeedHierarchicalPositions()
        {
            var foundations = nodes.Where(n => n.isFoundation && !n.isPhantom).ToList();
            if (foundations.Count == 0) return;

            float foundationRadius = 910f * Mathf.Sqrt(Mathf.Max(1, foundations.Count));
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
                        if (godModeStateSnapshot != null)
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
                if (def.IsHidden)
                    return NodeState.Hidden;

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
                    if (BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.EraCompletion)
                    {
                        var progress = GetAdvancementProgressRaw(def.techLevel, DefsOf.Main, out _, out _);
                        if (progress < 1f) return NodeState.Hidden;
                    }
                }
            }

            if (BetterResearchMenuMod.settings.autoRevealNodes)
                return NodeState.Expanded;

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
            physicsTemperature = 200f;
            var activeCount = nodes.Count(n => n.state != NodeState.Hidden);
            if (activeCount == 0) return;

            for (int i = 0; i < 5000; i++)
            {
                PhysicsTick(0.04f, true);

                if (i > 150 && (velocitySum / activeCount) < 0.001f)
                {
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

            var currentFilterText = quickSearchWidget.filter.Text;
            if (currentFilterText != lastFilterText)
            {
                lastFilterText = currentFilterText;
                if (string.IsNullOrEmpty(currentFilterText))
                {
                    searchPending = false;
                    UpdateSearchResults();
                    appliedSearchActive = quickSearchWidget.filter.Active;
                }
                else
                {
                    lastSearchTypingTime = Time.realtimeSinceStartup;
                    searchPending = true;
                }
            }
            if (searchPending && Time.realtimeSinceStartup - lastSearchTypingTime >= 0.8f)
            {
                searchPending = false;
                UpdateSearchResults();
                appliedSearchActive = quickSearchWidget.filter.Active;
            }
            if (lastCurTab != CurTab)
            {
                lastCurTab = CurTab;
                zoom = 1f;
                targetZoom = 1f;
                zoomAnchoring = false;
                InitPhysics(true);
            }

            bool currentGodMode = GodModeReveal;
            if (currentGodMode != lastGodMode)
            {
                lastGodMode = currentGodMode;
                OnGodModeChanged(currentGodMode);
            }

            if (selectedProject != null && (selectedNode == null || selectedNode.isGroupNode || selectedNode.isPhantom || selectedNode.def != selectedProject))
            {
                selectedNode = nodes.FirstOrDefault(n => !n.isPhantom && !n.isGroupNode && n.def == selectedProject);
            }
            else if (selectedNode != null && !selectedNode.isGroupNode && !selectedNode.isPhantom && selectedProject != selectedNode.def)
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

            int ticksThisFrame = fastForwardTicks > 0 ? 30 : 1;
            bool isFastForwarding = fastForwardTicks > 0;
            if (fastForwardTicks > 0) fastForwardTicks--;

            for (int i = 0; i < ticksThisFrame; i++)
            {
                PhysicsTick(0.02f, isFastForwarding);
            }

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
                        def.IsHidden ||
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
                bool hasAccessToTab = true;
                if (CurTab == DefsOf.Anomaly)
                {
                    hasAccessToTab = HasAccessToAnomalyTab();
                }
                else if (CurTab == DefsOf.VGE_Gravtech)
                {
                    hasAccessToTab = DefsOf.VGE_Gravtech != null && DefsOf.BasicGravtech.IsFinished;
                }

                bool hasAccessToEra = true;
                if (CurTab == DefsOf.Main && currentEra != TechLevel.Undefined && BetterResearchMenuMod.settings.restrictResearchToTechLevel)
                {
                    if (currentEra > Faction.OfPlayer.def.techLevel)
                    {
                        hasAccessToEra = false;
                    }
                }

                if (!hasAccessToTab || !hasAccessToEra)
                {
                    CurTab = DefsOf.Main;
                    lastCurTab = DefsOf.Main;
                    currentEra = TechLevel.Undefined;
                    selectionLocked = false;
                    selectedNode = null;
                    selectedProject = null;
                    zoom = 1f;
                    targetZoom = 1f;
                    zoomAnchoring = false;
                    anyChanged = true;
                }

                if (godModeStateSnapshot != null)
                {
                    foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                    {
                        if (def.tab != CurTab) continue;
                        bool wouldBeHidden =
                            def.IsHidden ||
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
                physicsTemperature = Mathf.Max(physicsTemperature, 200f);
                fastForwardTicks = 60;
            }
        }

        private void PhysicsTick(float dt, bool ignoreSettings = false)
        {
            if (!BetterResearchMenuMod.settings.physicsEnabled && !ignoreSettings) { physicsTemperature = 0f; return; }
            if (physicsTemperature < 0.01f) { physicsTemperature = 0f; velocitySum = 0f; return; }

            velocitySum = 0f;
            int nodeCount = nodes.Count;
            float k_rep = 500f * BetterResearchMenuMod.settings.spacingForceMultiplier;
            float baseRep = (k_rep * k_rep) * 0.2f;
            float contForce = BetterResearchMenuMod.settings.contractingForceMultiplier;
            float centerForceMul = BetterResearchMenuMod.settings.centerForceMultiplier;

            float graphRadiusBound = Mathf.Max(300f, Mathf.Sqrt(nodeCount) * 120f);
            float graphRadiusBoundSq = graphRadiusBound * graphRadiusBound;
            float dynamicHubBuffer = Mathf.Clamp(12000f / Mathf.Max(1, nodeCount), 150f, 600f);

            if (isHiddenCache.Length < nodeCount)
            {
                int newSize = nodeCount + 200;
                isHiddenCache = new bool[newSize];
                isCollapsedCache = new bool[newSize];
                isHubCache = new bool[newSize];
                weightCache = new float[newSize];
                radiusCache = new float[newSize];
                pX = new float[newSize];
                pY = new float[newSize];
                centerForceBaseCache = new float[newSize];
                invMassCache = new float[newSize];
            }

            for (int i = 0; i < nodeCount; i++)
            {
                var n = nodes[i];
                bool hidden = n.state == NodeState.Hidden;
                isHiddenCache[i] = hidden;
                if (hidden) continue;

                bool coll = n.state == NodeState.Dot || n.state == NodeState.Minimized;
                isCollapsedCache[i] = coll;
                isHubCache[i] = n.isFoundation || n.isGroupNode;
                weightCache[i] = n.cachedWeight;

                if (n.isGroupNode) n.collisionRadius = 25f;
                else if (coll)
                {
                    if (n.isFoundation || n.isEmergence) n.collisionRadius = 52f;
                    else n.collisionRadius = 20f;
                }
                else
                {
                    if (n.isFoundation || n.isEmergence) n.collisionRadius = 104f;
                    else n.collisionRadius = 40f;
                }
                radiusCache[i] = n.collisionRadius;
                pX[i] = n.pos.x;
                pY[i] = n.pos.y;
                centerForceBaseCache[i] = 1.5f * centerForceMul * (n.edgeCount == 0 ? 3.0f : Mathf.Exp(-n.edgeCount * 0.4f));
                invMassCache[i] = 1f / n.cachedMass;
            }

            for (int ni = 0; ni < nodeCount; ni++)
            {
                var node = nodes[ni];
                if (node.isDragging || isHiddenCache[ni] || node.isPhantom || node.isAnchor)
                {
                    node.velocity = Vector2.zero;
                    continue;
                }

                float nx = pX[ni];
                float ny = pY[ni];

                if (float.IsNaN(nx) || float.IsNaN(ny))
                {
                    nx = Rand.Range(-10f, 10f);
                    ny = Rand.Range(-10f, 10f);
                    node.pos = new Vector2(nx, ny);
                    pX[ni] = nx;
                    pY[ni] = ny;
                    node.velocity = Vector2.zero;
                }

                float repX = 0f;
                float repY = 0f;

                bool isCollapsed = isCollapsedCache[ni];
                float nodeWeight = weightCache[ni];
                float nodeRadius = radiusCache[ni];
                bool nodeIsHub = isHubCache[ni];

                float mulIfOtherColl = isCollapsed ? 2.0f : 0.15f;
                float mulIfOtherNotColl = isCollapsed ? 0.15f : 1f;

                float baseRepWeight = baseRep * nodeWeight;
                int niRow = ni * nodeCount;

                for (int oi = 0; oi < nodeCount; oi++)
                {
                    if (ni == oi || isHiddenCache[oi]) continue;

                    float dx = nx - pX[oi];
                    float dy = ny - pY[oi];
                    float distSq = dx * dx + dy * dy;

                    if (!(distSq >= 1f))
                    {
                        dx = Rand.Value - 0.5f;
                        dy = Rand.Value - 0.5f;
                        if (dx == 0f && dy == 0f) dx = 0.01f;
                        distSq = dx * dx + dy * dy;
                    }

                    float forceMagSq = baseRepWeight * weightCache[oi] / distSq;

                    if (adjacencyMatrixFlat[niRow + oi]) forceMagSq *= 0.4f;

                    bool otherIsHub = isHubCache[oi];

                    if (nodeIsHub && otherIsHub)
                    {
                        forceMagSq *= 25.0f;
                    }

                    forceMagSq *= isCollapsedCache[oi] ? mulIfOtherColl : mulIfOtherNotColl;

                    float buffer = (nodeIsHub && otherIsHub) ? dynamicHubBuffer : 20f;
                    float minDist = nodeRadius + radiusCache[oi] + buffer;
                    float minDistSq = minDist * minDist;
                    if (distSq < minDistSq)
                    {
                        float dist = Mathf.Sqrt(distSq);
                        forceMagSq += (minDist - dist) * 2000f / dist;
                    }

                    repX += dx * forceMagSq;
                    repY += dy * forceMagSq;
                }

                float attX = 0f;
                float attY = 0f;

                int edgeCount = node.nodeEdges.Count;
                for (int e = 0; e < edgeCount; e++)
                {
                    var edge = node.nodeEdges[e];
                    var other = (edge.from == node) ? edge.to : edge.from;
                    int oi = other.nodeIndex;
                    if (isHiddenCache[oi]) continue;

                    float dx = pX[oi] - nx;
                    float dy = pY[oi] - ny;
                    float distSq = dx * dx + dy * dy;

                    if (!(distSq >= 100f)) continue;

                    float dist = Mathf.Sqrt(distSq);

                    const float k_att = 200f;
                    float attMul = 1f;

                    bool fromCollapsed = isCollapsedCache[edge.from.nodeIndex];
                    bool toCollapsed = isCollapsedCache[edge.to.nodeIndex];
                    if (toCollapsed) attMul = 30f;
                    else if (fromCollapsed) attMul = 6f;

                    float mul = (dist / k_att) * attMul * contForce;
                    attX += dx * mul;
                    attY += dy * mul;
                }

                float distToCenterSq = nx * nx + ny * ny;
                float distanceMultiplier = 1f + (distToCenterSq / graphRadiusBoundSq);
                float centerForceFactor = centerForceBaseCache[ni] * distanceMultiplier;

                float cx = -nx * centerForceFactor;
                float cy = -ny * centerForceFactor;

                float totalForceX = repX + attX + cx;
                float totalForceY = repY + attY + cy;

                node.lastRepulsionForce = new Vector2(repX, repY);
                node.lastAttractionForce = new Vector2(attX, attY);
                node.lastCenterForce = new Vector2(cx, cy);

                float vx = node.velocity.x;
                float vy = node.velocity.y;
                float invMass = invMassCache[ni];

                vx = (vx + totalForceX * invMass * dt) * 0.75f;
                vy = (vy + totalForceY * invMass * dt) * 0.75f;

                float speedSq = vx * vx + vy * vy;
                float physTempSq = physicsTemperature * physicsTemperature;

                if (speedSq > physTempSq)
                {
                    float speed = Mathf.Sqrt(speedSq);
                    float shrink = physicsTemperature / speed;
                    vx *= shrink;
                    vy *= shrink;
                    speedSq = physTempSq;
                }
                else if (speedSq < 0.0025f && !ignoreSettings)
                {
                    vx = 0f;
                    vy = 0f;
                    speedSq = 0f;
                }

                node.velocity = new Vector2(vx, vy);
                velocitySum += speedSq;
            }

            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodes[i];
                if (node.isDragging || isHiddenCache[node.nodeIndex] || node.isAnchor) continue;

                node.pos.x += node.velocity.x * dt * 8f;
                node.pos.y += node.velocity.y * dt * 8f;
            }

            if (ignoreSettings) physicsTemperature *= 0.995f;
            else
            {
                physicsTemperature *= 0.98f;
                if (wasDraggingNode || hasSignificantDrag)
                {
                    physicsTemperature = Mathf.Max(physicsTemperature, 20f);
                }
            }
        }

        private float velocitySum = 0f;
        private HashSet<(ResearchNode, ResearchNode)> phantomEdgeSet = new HashSet<(ResearchNode, ResearchNode)>();

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

            var panelWidth = (selectedNode != null && !selectedNode.isPhantom && !selectedNode.isGroupNode) ? RightPanelWidth : 0f;
            if (prevPanelWidth != panelWidth)
            {
                cameraOffset.x += (panelWidth - prevPanelWidth) / 2f / zoom;
                prevPanelWidth = panelWidth;
            }
            graphRect = new Rect(0f, TopBarHeight, inRect.width - panelWidth, inRect.height - TopBarHeight - BottomBarHeight - QueueBarHeight);

            float leftBarShift = LeftBarVisible ? 45f : 5f;

            var controlAreaRect = new Rect(leftBarShift + 5f, graphRect.height - 145f, 250f, 160f);
            float searchBarWidth = 200f;
            float searchBarHeight = 24f;
            var searchBarRect = new Rect(graphRect.width - searchBarWidth - 4f, 4f, searchBarWidth, searchBarHeight);
            var pivot = new Vector2(graphRect.width / 2f, (inRect.height - TopBarHeight - 40f) / 2f);

            var panelRect = new Rect(inRect.width - RightPanelWidth, TopBarHeight, RightPanelWidth, graphRect.height);

            bool searchActive = appliedSearchActive;
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
                        nodeSize = ((isFoundation || isEmergence) ? NodeSizeExpanded * 2.6f : NodeSizeExpanded) * zoom;
                    }

                    if (Vector2.Distance(screenPos, localMousePos) < nodeSize / 2f)
                    {
                        hoveredNode = node;
                        break;
                    }
                }

                if (!selectionLocked && hoveredNode != null && !isPanning && !wasDraggingNode)
                {
                    if (!IsMysteryNode(hoveredNode) || BetterResearchMenuMod.settings.revealMysteryNodeOnHover)
                    {
                        selectedNode = hoveredNode;
                        selectedProject = (hoveredNode.isGroupNode || hoveredNode.isPhantom) ? null : hoveredNode.def;
                    }
                }

                if (Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1 || Event.current.button == 2))
                {
                    if (hoveredNode != null && Event.current.button != 2)
                    {
                        if (Event.current.button == 0)
                        {
                            selectedNode = hoveredNode;
                            selectedProject = (hoveredNode.isGroupNode || hoveredNode.isPhantom) ? null : hoveredNode.def;

                            hoveredNode.isDragging = true;
                            wasDraggingNode = true;
                            hasSignificantDrag = false;
                            dragStartMousePos = localMousePos;
                            var worldMousePos = ((localMousePos - pivot) / zoom) - cameraOffset;
                            dragOffset = hoveredNode.pos - worldMousePos;
                        }
                        else if (Event.current.button == 1)
                        {
                            if (!hoveredNode.isGroupNode && !hoveredNode.isPhantom)
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
                    if (Event.current.button == 0 && selectedNode != null && !selectedNode.isGroupNode && !selectedNode.isPhantom && Vector2.Distance(localMousePos, dragStartMousePos) < 5f)
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
                            if (!GodModeReveal && selectedNode.isLockedCache && !Event.current.shift)
                            {
                                // Locked only by missing research prerequisites: queue the chain
                                // Locked by anything else (era, study, mechanitor, other shit): show message
                                bool onlyMissingPrereqs = !selectedNode.def.PrerequisitesCompleted
                                    && selectedNode.def.TechprintRequirementMet
                                    && selectedNode.def.PlayerMechanitorRequirementMet
                                    && selectedNode.def.AnalyzedThingsRequirementsMet
                                    && !(BetterResearchMenuMod.settings.restrictResearchToTechLevel && selectedNode.def.techLevel > Faction.OfPlayer.def.techLevel);
                                if (onlyMissingPrereqs)
                                {
                                    var chain = GetPrerequisiteChain(selectedNode.def);
                                    if (chain.Count > 0)
                                    {
                                        State.researchQueue.Clear();
                                        foreach (var dep in chain)
                                            if (!dep.IsFinished && !State.researchQueue.Contains(dep.defName))
                                                State.researchQueue.Add(dep.defName);
                                        if (State.researchQueue.Count > 0)
                                        {
                                            var firstDef = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(State.researchQueue[0]);
                                            if (firstDef != null && firstDef.CanStartNow)
                                            {
                                                Find.ResearchManager.SetCurrentProject(firstDef);
                                                TutorSystem.Notify_Event("StartResearchProject");
                                            }
                                        }
                                        Messages.Message("BRM_QueuedResearch".Translate(selectedNode.def.LabelCap, chain.Count), MessageTypeDefOf.TaskCompletion, false);
                                    }
                                }
                                else
                                {
                                    var reasons = GetLockedReasons(selectedNode.def);
                                    Messages.Message("Locked".Translate() + (reasons.Count > 0 ? ": " + reasons[0] : ""), MessageTypeDefOf.RejectInput, false);
                                }
                            }
                            else if (Event.current.shift && !selectedNode.def.IsFinished && !GetActiveProjectsCached(CurTab).Contains(selectedNode.def))
                            {
                                // Block shift+queue if locked by non-prerequisite reason
                                bool shiftOnlyMissingPrereqs = !selectedNode.def.PrerequisitesCompleted
                                    && selectedNode.def.TechprintRequirementMet
                                    && selectedNode.def.PlayerMechanitorRequirementMet
                                    && selectedNode.def.AnalyzedThingsRequirementsMet
                                    && !(BetterResearchMenuMod.settings.restrictResearchToTechLevel && selectedNode.def.techLevel > Faction.OfPlayer.def.techLevel);
                                if (selectedNode.isLockedCache && !shiftOnlyMissingPrereqs)
                                {
                                    var shiftReasons = GetLockedReasons(selectedNode.def);
                                    Messages.Message("Locked".Translate() + (shiftReasons.Count > 0 ? ": " + shiftReasons[0] : ""), MessageTypeDefOf.RejectInput, false);
                                }
                                else
                                {
                                    var chain = GetPrerequisiteChain(selectedNode.def);
                                    if (chain.Count == 0)
                                    {
                                        Messages.Message("BRM_QueueAlreadyFinished".Translate(), MessageTypeDefOf.RejectInput, false);
                                    }
                                    else
                                    {
                                        int added = 0;
                                        foreach (var dep in chain)
                                        {
                                            if (!State.researchQueue.Contains(dep.defName) && !dep.IsFinished)
                                            {
                                                State.researchQueue.Add(dep.defName);
                                                added++;
                                            }
                                        }

                                        if (Find.ResearchManager.currentProj == null && State.researchQueue.Count > 0)
                                        {
                                            var firstDefName = State.researchQueue[0];
                                            var firstDef = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(firstDefName);
                                            if (firstDef != null && firstDef.CanStartNow)
                                            {
                                                Find.ResearchManager.SetCurrentProject(firstDef);
                                                TutorSystem.Notify_Event("StartResearchProject");
                                            }
                                        }

                                        if (added > 0)
                                            Messages.Message("BRM_QueuedResearch".Translate(selectedNode.def.LabelCap, added), MessageTypeDefOf.TaskCompletion, false);
                                        else
                                            Messages.Message("BRM_QueueAlreadyQueued".Translate(selectedNode.def.LabelCap), MessageTypeDefOf.NeutralEvent, false);
                                    }
                                }
                            }
                            else if (selectedNode.canStartNowCache && !GetActiveProjectsCached(CurTab).Contains(selectedNode.def))
                            {
                                if (selectedNode.def.HasModExtension<EmergenceExtension>() && selectedNode.def.techLevel == TechLevel.Animal)
                                {
                                    SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                                    Find.ResearchManager.SetCurrentProject(selectedNode.def);
                                    TutorSystem.Notify_Event("StartResearchProject");
                                }
                                else
                                {
                                    State.researchQueue.Clear();
                                    AttemptBeginResearch(selectedNode.def);
                                }
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
                    if (zoom > ZoomThreshold)
                    {
                        DrawScaledLabel(new Rect(screenPos.x - (150f * zoom) / 2f, screenPos.y + gSize / 2f + 2f * zoom, 150f * zoom, 40f * zoom), node.groupNodeDef.LabelCap, zoom);
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
                    if (zoom > ZoomThreshold)
                    {
                        DrawScaledLabel(new Rect(screenPos.x - (150f * zoom) / 2f, screenPos.y + size / 2f + 2f * zoom, 150f * zoom, 40f * zoom), node.phantomEra.ToStringHuman().CapitalizeFirst(), zoom);
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
                            else if (node.isPhantom is false && (BetterResearchMenuMod.settings.autoRevealNodes || State.openedNodes.Contains(node.def.defName)))
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

                var nodeSize = ((isFoundation || isEmergence) ? NodeSizeExpanded * 2.6f : NodeSizeExpanded) * zoom;
                var nodeRect = new Rect(screenPos.x - nodeSize / 2f, screenPos.y - nodeSize / 2f, nodeSize, nodeSize);

                if (node == selectedNode)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(nodeRect.ExpandedBy(10f * zoom), TexBubble);
                    GUI.color = Color.white;
                }
                if (node.isPhantom is false)
                    State.openedNodes.Add(node.def.defName);
                var padding = (isFoundation || isEmergence) ? IconPadding * 3f : IconPadding;
                bool isSilhouetted = !node.isFinishedCache;
                DrawBubble(nodeRect, node.def, padding * zoom, activeProjects, drawSilhouette: isSilhouetted);

                if (zoom > ZoomThreshold)
                {
                    DrawScaledResearchLabels(node, screenPos, nodeSize, zoom);
                }
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            DrawGraphControls(controlAreaRect);

            // Intercept Enter/Return before the search widget can consume the event
            if (!GUI.GetNameOfFocusedControl().NullOrEmpty() &&
                Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                lastSearchTypingTime = 0f;
                GUI.FocusControl(null);
                Event.current.Use();
            }

            quickSearchWidget.OnGUI(searchBarRect);

            Widgets.EndGroup();

            if (CurTab == DefsOf.Main)
            {
                DrawAdvanceButton(inRect);
                DrawTopBar(inRect);
                DrawQueueBar(inRect);
                DrawLeftBar(inRect);
            }
            DrawBottomBar(inRect);
            if (selectedNode != null && !selectedNode.isPhantom && !selectedNode.isGroupNode)
            {
                try
                {
                    DrawRightPanel(inRect);
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce("[BetterResearchMenu] Error drawing right panel for " + selectedNode.def.defName + ": " + ex, selectedNode.def.defName.GetHashCode());
                    selectedNode = null;
                    selectedProject = null;
                }
            }
        }

        private void HandleInputs(Rect graphRect, Rect sliderExcl, Rect panelExcl, Rect searchBarExcl, Rect inRect)
        {
            float zoomSensitivity = BetterResearchMenuMod.settings.zoomSensitivity;
            float zoomSmoothSpeed = 12f;
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
            var pivot = new Vector2(graphRect.width / 2f, (inRect.height - TopBarHeight - 40f) / 2f);

            void StartSmoothZoom(float wheelDelta, Vector2 anchorScreen)
            {
                zoomAnchorScreen = anchorScreen;
                zoomAnchorWorld = ((anchorScreen - pivot) / zoom) - cameraOffset;
                targetZoom = Mathf.Clamp(targetZoom - wheelDelta * zoomSensitivity, minZoom, maxZoom);
                zoomAnchoring = true;
            }

            if (Event.current.type == EventType.MouseDown)
                lastMousePos = localMousePos;

            if (Event.current.type == EventType.ScrollWheel && graphRect.Contains(Event.current.mousePosition))
            {
                StartSmoothZoom(Event.current.delta.y, localMousePos);
                Event.current.Use();
            }

            if (GUI.GetNameOfFocusedControl().NullOrEmpty())
            {
                bool zoomInDown = KeyBindingDefOf.MapZoom_In.IsDown;
                bool zoomOutDown = KeyBindingDefOf.MapZoom_Out.IsDown;
                bool anyZoomKeyDown = zoomInDown || zoomOutDown;

                if (anyZoomKeyDown && Event.current.type == EventType.KeyDown)
                    Event.current.Use();

                if (anyZoomKeyDown && Event.current.type == EventType.Repaint)
                {
                    Vector2 anchor = graphRect.Contains(mousePos) ? localMousePos : pivot;
                    float keyboardDelta = 0f;
                    if (zoomInDown) keyboardDelta -= Time.deltaTime * 60f;
                    if (zoomOutDown) keyboardDelta += Time.deltaTime * 60f;
                    StartSmoothZoom(keyboardDelta, anchor);
                }
            }

            if (Event.current.type == EventType.Repaint && Mathf.Abs(targetZoom - zoom) > 0.0001f)
            {
                zoom = Mathf.Lerp(zoom, targetZoom, Mathf.Clamp01(Time.deltaTime * zoomSmoothSpeed));
                if (zoomAnchoring)
                {
                    cameraOffset = ((zoomAnchorScreen - pivot) / zoom) - zoomAnchorWorld;
                    cachedCameraOffsets[$"{CurTab.defName}_{currentEra}_{GodModeReveal}"] = cameraOffset;
                }
                if (Mathf.Abs(targetZoom - zoom) <= 0.0005f)
                {
                    zoom = targetZoom;
                    zoomAnchoring = false;
                }
            }

            if (!sliderExcl.Contains(localMousePos) && !searchBarExcl.Contains(localMousePos) && isPanning && Event.current.type == EventType.MouseDrag)
            {
                cameraOffset += (localMousePos - lastMousePos) / zoom;
                lastMousePos = localMousePos;
                cachedCameraOffsets[$"{CurTab.defName}_{currentEra}_{GodModeReveal}"] = cameraOffset;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag)
            {
                lastMousePos = localMousePos;
            }

            // Camera pan using the player's configured dolly keybindings.
            // Gate on Repaint so we apply exactly once per rendered frame
			// Why not just do this on update() with deltatime? lolidk that ate my keystrokes for some reason and wouldn't fuckin work
            // In theory we consume the key event so the camera doesn't move in the background, but that didn't work either... 
			// So I just have it reset the camera to what it was on PreOpen() lmao best coder ever
            if (GUI.GetNameOfFocusedControl().NullOrEmpty() && BetterResearchMenuMod.settings.wasdPanSpeed > 0f)
            {
                bool anyDown = KeyBindingDefOf.MapDolly_Up.IsDown || KeyBindingDefOf.MapDolly_Down.IsDown ||
                              KeyBindingDefOf.MapDolly_Left.IsDown || KeyBindingDefOf.MapDolly_Right.IsDown;

                if (anyDown && Event.current.type == EventType.KeyDown)
                    Event.current.Use();

                if (anyDown && Event.current.type == EventType.Repaint)
                {
                    float panSpeed = BetterResearchMenuMod.settings.wasdPanSpeed * Time.deltaTime * 60f / zoom;
                    bool moved = false;
                    if (KeyBindingDefOf.MapDolly_Up.IsDown)    { cameraOffset.y += panSpeed; moved = true; }
                    if (KeyBindingDefOf.MapDolly_Down.IsDown)  { cameraOffset.y -= panSpeed; moved = true; }
                    if (KeyBindingDefOf.MapDolly_Left.IsDown)  { cameraOffset.x += panSpeed; moved = true; }
                    if (KeyBindingDefOf.MapDolly_Right.IsDown) { cameraOffset.x -= panSpeed; moved = true; }
                    if (moved)
                    {
                        // Keep camera frozen in place
                        PostClose();
                        cachedCameraOffsets[$"{CurTab.defName}_{currentEra}_{GodModeReveal}"] = cameraOffset;
                    }
                }
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
                        targetZoom = 1f;
                        zoomAnchoring = false;
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

            if (progress >= 1f && playerEra < TechLevel.Archotech)
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
                    VFETribalsCompat.GrantCornerstonePoint();
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

            if (selectedNode == null || selectedNode.isGroupNode || selectedNode.isPhantom) return;

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

            var hasAnomaly = DefsOf.Anomaly != null && (DebugSettings.godMode || HasAccessToAnomalyTab());
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

            bool hasAnomaly = DefsOf.Anomaly != null && (DebugSettings.godMode || HasAccessToAnomalyTab());
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

        private void DrawQueueBar(Rect inRect)
        {
            var currentProj = Find.ResearchManager?.currentProj;
            if (State.researchQueue.Count == 0 && currentProj == null)
            {
                queueDragIndex = -1;
                return;
            }

            float barHeight = QueueBarHeight;
            float y = inRect.height - BottomBarHeight - barHeight;
            var barRect = new Rect(0f, y, inRect.width, barHeight);
            Widgets.DrawBoxSolid(barRect, ColorTechLevelTab);

            float bubbleSize = barHeight - 8f;
            float itemPadding = 4f;
            float labelWidth = 150f;
            float itemWidth = bubbleSize + itemPadding + labelWidth + 16f;

            // Start after left bar if visible
            float xStart = (LeftBarVisible ? 45f : 0f) + 8f;

            // "Queue:" label
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            Widgets.Label(new Rect(xStart, y + 2f, 50f, barHeight - 4f), "Queue:");
            GUI.color = Color.white;
            xStart += 54f;

            // Cull finished entries from queue
            State.researchQueue.RemoveAll(d =>
            {
                var dd = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(d);
                return dd == null || dd.IsFinished;
            });
            // Also remove currentProj from queue if it's in there (it's already active)
            if (currentProj != null)
                State.researchQueue.Remove(currentProj.defName);

            var activeProjs = GetActiveProjectsCached(CurTab);
            var mousePos = Event.current.mousePosition;

            // Build unified display list: currentProj at index 0, then queue
            // displayIndex 0 = currentProj (or null), displayIndex 1..n = queue items
            int totalDisplay = (currentProj != null ? 1 : 0) + State.researchQueue.Count;

            float queueViewportLeft = xStart;
            float queueViewportRight = inRect.width - 8f;
            float queueViewportWidth = Mathf.Max(0f, queueViewportRight - queueViewportLeft);
            float queueContentWidth = totalDisplay * itemWidth;
            float maxQueueScrollOffset = Mathf.Max(0f, queueContentWidth - queueViewportWidth);
            queueScrollOffset = Mathf.Clamp(queueScrollOffset, 0f, maxQueueScrollOffset);

            if (Event.current.type == EventType.ScrollWheel && barRect.Contains(mousePos) && maxQueueScrollOffset > 0f)
            {
                queueScrollOffset = Mathf.Clamp(queueScrollOffset + Event.current.delta.y * 48f, 0f, maxQueueScrollOffset);
                Event.current.Use();
            }

            // Build item rects for hit-testing (only fully visible ones)
            var itemRects = new List<(int displayIndex, Rect rect)>();
            float x = xStart - queueScrollOffset;
            for (int i = 0; i < totalDisplay; i++)
            {
                if (x >= queueViewportLeft && x + itemWidth <= queueViewportRight)
                    itemRects.Add((i, new Rect(x, y, itemWidth, barHeight)));

                x += itemWidth;
            }

            ResearchProjectDef GetDisplayDef(int displayIndex)
            {
                if (displayIndex < 0 || State.researchQueue == null)
                    return null;

                if (currentProj != null)
                {
                    if (displayIndex == 0) return currentProj;
                    int qIdx = displayIndex - 1;
                    if (qIdx < 0 || qIdx >= State.researchQueue.Count)
                        return null;
                    return DefDatabase<ResearchProjectDef>.GetNamedSilentFail(State.researchQueue[qIdx]);
                }

                if (displayIndex >= State.researchQueue.Count)
                    return null;
                return DefDatabase<ResearchProjectDef>.GetNamedSilentFail(State.researchQueue[displayIndex]);
            }

            bool IsCurrentResearch(ResearchProjectDef def)
            {
                var active = Find.ResearchManager?.currentProj;
                return def != null && active != null && active.defName == def.defName;
            }

            void ClearCurrentResearch()
            {
                if (Find.ResearchManager != null)
                    Traverse.Create(Find.ResearchManager).Field("currentProj").SetValue(null);
            }

            bool PromoteNextQueuedProject()
            {
                while (State.researchQueue.Count > 0)
                {
                    var nextDefName = State.researchQueue[0];
                    State.researchQueue.RemoveAt(0);

                    var nextDef = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(nextDefName);
                    if (nextDef == null || nextDef.IsFinished)
                        continue;

                    if (nextDef.CanStartNow)
                    {
                        Find.ResearchManager.SetCurrentProject(nextDef);
                        TutorSystem.Notify_Event("StartResearchProject");
                        return true;
                    }

                    State.researchQueue.Insert(0, nextDefName);
                    break;
                }

                ClearCurrentResearch();
                return false;
            }

            // --- Drag logic ---
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                foreach (var (idx, rect) in itemRects)
                {
                    if (rect.Contains(mousePos))
                    {
                        queueDragIndex = idx;
                        queueDragStartX = rect.x;
                        queueDragCurrentX = rect.x;
                        Event.current.Use();
                        break;
                    }
                }
            }
            else if (Event.current.type == EventType.MouseDrag && queueDragIndex >= 0)
            {
                queueDragCurrentX += Event.current.delta.x;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseUp && queueDragIndex >= 0)
            {
                // Find drop position by imagining the dragged item removed and seeing where
                // the ghost's left edge falls among the remaining slots.
                // Build positions of non-dragged items in their display order.
                float ghostLeft = queueDragCurrentX;
                var otherRects = itemRects.Where(r => r.displayIndex != queueDragIndex).ToList();
                int dropIndex = queueDragIndex; // default: no move

                if (otherRects.Count > 0)
                {
                    // Find which gap the ghost's left edge falls into
                    // gaps: before first, between each pair, after last
                    int insertBefore = otherRects.Count; // default: after all others
                    for (int i = 0; i < otherRects.Count; i++)
                    {
                        if (ghostLeft < otherRects[i].rect.x + itemWidth * 0.5f)
                        {
                            insertBefore = i;
                            break;
                        }
                    }

                    // Convert insertBefore (index into otherRects) to a displayIndex
                    // If inserting before item i in otherRects, the new displayIndex is that item's displayIndex
                    // adjusted for the fact that the dragged item will be removed first.
                    if (insertBefore < otherRects.Count)
                    {
                        int targetDisplay = otherRects[insertBefore].displayIndex;
                        // If dragging rightward, the target's displayIndex will shift down by 1 after removal
                        dropIndex = targetDisplay > queueDragIndex ? targetDisplay - 1 : targetDisplay;
                    }
                    else
                    {
                        // After all others: last other item's displayIndex, adjusted
                        int lastDisplay = otherRects[otherRects.Count - 1].displayIndex;
                        dropIndex = lastDisplay > queueDragIndex ? lastDisplay - 1 : lastDisplay;
                    }
                }

                if (dropIndex != queueDragIndex)
                {
                    var draggedDef = GetDisplayDef(queueDragIndex);
                    if (draggedDef != null)
                    {
                        bool wasCurrent = currentProj != null && queueDragIndex == 0;

                        // Current proj chain is just itself — prereqs are already done.
                        // Queue item chain includes any of its queued prerequisites.
                        var chain = wasCurrent
                            ? new List<string> { draggedDef.defName }
                            : GetPrerequisiteChain(draggedDef)
                                .Where(d => State.researchQueue.Contains(d.defName))
                                .Select(d => d.defName)
                                .ToList();

                        foreach (var name in chain)
                            State.researchQueue.Remove(name);

                        if (dropIndex == 0)
                        {
                            // Dropping onto the current slot: promote first of chain to current,
                            // push the old current (if any) to the front of the queue.
                            var newCurrentDef = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(chain[0]);
                            chain.RemoveAt(0);
                            if (currentProj != null)
                                State.researchQueue.Insert(0, currentProj.defName);
                            State.researchQueue.InsertRange(0, chain);
                            if (newCurrentDef != null && newCurrentDef.CanStartNow)
                            {
                                Find.ResearchManager.SetCurrentProject(newCurrentDef);
                                TutorSystem.Notify_Event("StartResearchProject");
                            }
                        }
                        else
                        {
                            // Dropping into the queue. Subtract 1 from dropIndex when there's a
                            // current-slot item taking up display position 0.
                            int queueInsertAt = dropIndex - (currentProj != null && !wasCurrent ? 1 : 0);

                            // Can't insert after an item that depends on our chain
                            int maxInsert = State.researchQueue.Count;
                            for (int i = 0; i < State.researchQueue.Count; i++)
                            {
                                var item = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(State.researchQueue[i]);
                                if (item != null)
                                {
                                    var itemPrereqs = GetPrerequisiteChain(item).Select(d => d.defName).ToList();
                                    if (chain.Any(c => itemPrereqs.Contains(c)))
                                    {
                                        maxInsert = i;
                                        break;
                                    }
                                }
                            }

                            // Can't insert before an item that our chain depends on
                            int minInsert = 0;
                            for (int i = 0; i < State.researchQueue.Count; i++)
                            {
                                var item = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(State.researchQueue[i]);
                                if (item != null && chain.Any(c =>
                                {
                                    var d = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(c);
                                    return d != null && GetPrerequisiteChain(d).Any(p => p.defName == item.defName);
                                }))
                                    minInsert = i + 1;
                            }

                            queueInsertAt = Mathf.Clamp(queueInsertAt, minInsert, maxInsert);
                            State.researchQueue.InsertRange(queueInsertAt, chain);

                            // If current research was moved into queue, start whatever is now first
                            if (wasCurrent && State.researchQueue.Count > 0)
                            {
                                var newFirst = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(State.researchQueue[0]);
                                if (newFirst != null && newFirst.CanStartNow)
                                {
                                    Find.ResearchManager.SetCurrentProject(newFirst);
                                    TutorSystem.Notify_Event("StartResearchProject");
                                }
                            }
                        }

                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }

                queueDragIndex = -1;
                Event.current.Use();
            }

            // --- Draw items ---
            x = xStart - queueScrollOffset;
            for (int i = 0; i < totalDisplay; i++)
            {
                if (x + itemWidth < queueViewportLeft) { x += itemWidth; continue; }
                if (x > queueViewportRight) break;
                if (x < queueViewportLeft || x + itemWidth > queueViewportRight) { x += itemWidth; continue; }

                var def = GetDisplayDef(i);
                if (def == null) { x += itemWidth; continue; }

                bool isCurrent = currentProj != null && i == 0;
                bool isDragging = queueDragIndex == i;
                float drawX = isDragging ? queueDragCurrentX : x;

                // Drop insertion indicator
                if (queueDragIndex >= 0 && !isDragging)
                {
                    float midDrag = queueDragCurrentX + itemWidth / 2f;
                    float midThis = x + itemWidth / 2f;
                    float midPrev = x - itemWidth / 2f;
                    if (midDrag >= midPrev && midDrag < midThis)
                    {
                        GUI.color = new Color(1f, 1f, 0.3f, 0.8f);
						// Widgets.DrawLineVertical(x - 2f, y + 4f, barHeight - 8f);
						Widgets.DrawLine(new Vector2(x - 8f, y + 4f), new Vector2(x - 8f, y + barHeight + 4f), Color.yellow, 3f);

						GUI.color = Color.white;
                    }
                }

                // Highlight current research slot
                if (isCurrent && !isDragging)
                {
                    GUI.color = new Color(0.4f, 0.8f, 0.4f, 0.15f);
                    Widgets.DrawBoxSolid(new Rect(x, y, itemWidth, barHeight), GUI.color);
                    GUI.color = Color.white;
                }

                if (isDragging)
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);

                var bubbleRect = new Rect(drawX, y + 4f, bubbleSize, bubbleSize);
                DrawBubble(bubbleRect, def, 2f, activeProjs);

                Text.Font = GameFont.Tiny;
                GUI.color = isDragging ? new Color(1f, 1f, 1f, 0.2f) : new Color(1f, 1f, 1f, 0.4f);
                Widgets.Label(new Rect(drawX, y + 1f, bubbleSize, 10f), isCurrent ? "▶" : i.ToString());

                GUI.color = isDragging ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                var labelRect = new Rect(drawX + bubbleSize + itemPadding, y + 2f, labelWidth, barHeight - 4f);
                Widgets.Label(labelRect, def.LabelCap);
                GUI.color = Color.white;

                // Separator
                if (!isDragging && i < totalDisplay - 1)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.15f);
                    Widgets.DrawLineVertical(x + itemWidth - 8f, y + 6f, barHeight - 12f);
                    GUI.color = Color.white;
                }

                // Tooltip + right-click to remove
                var itemRect = new Rect(x, y, itemWidth, barHeight);
                if (!isDragging)
                {
                    string tip = isCurrent
                        ? "Currently researching  |  Right-click to remove  |  Drag right to queue it"
                        : "Drag to reorder  |  Right-click to remove: " + def.LabelCap;
                    TooltipHandler.TipRegion(itemRect, tip);

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && itemRect.Contains(mousePos))
                    {
                        bool removedCurrent = IsCurrentResearch(def);
                        if (removedCurrent && !State.researchQueue.Contains(def.defName))
                            State.researchQueue.Insert(0, def.defName);

                        RemoveFromQueueCascade(def.defName);

                        if (removedCurrent && Find.ResearchManager.currentProj == null)
                            PromoteNextQueuedProject();

                        SoundDefOf.Click.PlayOneShotOnCamera();
                        Event.current.Use();
                    }
                }

                x += itemWidth;
            }

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
                else
                {
                    physicsTemperature = Mathf.Max(physicsTemperature, 100f);
                }
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(physicsBtnRect, "BRM_TogglePhysics".Translate());

            if (!BetterResearchMenuMod.settings.forbidVanillaMenu && Widgets.ButtonImage(vanillaBtnRect, TexVanilla))
            {
                Close();
                Startup.Restore();
                var vanillaWindow = new MainTabWindow_Research();
                vanillaWindow.def = MainButtonDefOf.Research;
                Find.WindowStack.Add(vanillaWindow);
                SoundDefOf.TabOpen.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(vanillaBtnRect, "BRM_OpenVanillaMenu".Translate());

            var settingsBtnRect = new Rect(vanillaBtnRect.xMax + ControlBtnGap, controlAreaRect.y, ControlBtnSize, ControlBtnSize);
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

            GUI.DrawTexture(new Rect(gravityRect.x - 24f, gravityRect.y + 1f, 16f, 16f), TexCenter);
            BetterResearchMenuMod.settings.centerForceMultiplier = Widgets.HorizontalSlider(gravityRect, BetterResearchMenuMod.settings.centerForceMultiplier, 0.1f, 5.0f, true);

            GUI.DrawTexture(new Rect(spacingRect.x - 24f, spacingRect.y + 1f, iconSize, iconSize), TexSpacing);
            BetterResearchMenuMod.settings.spacingForceMultiplier = Widgets.HorizontalSlider(spacingRect, BetterResearchMenuMod.settings.spacingForceMultiplier, 0.1f, 10.0f, true);

            GUI.DrawTexture(new Rect(contractingRect.x - 24f, contractingRect.y + 1f, iconSize, iconSize), TexContracting);
            BetterResearchMenuMod.settings.contractingForceMultiplier = Widgets.HorizontalSlider(contractingRect, BetterResearchMenuMod.settings.contractingForceMultiplier, 0.1f, 5.0f, true);

            if (!Mathf.Approximately(oldGrav, BetterResearchMenuMod.settings.centerForceMultiplier) ||
                !Mathf.Approximately(oldSpace, BetterResearchMenuMod.settings.spacingForceMultiplier) ||
                !Mathf.Approximately(oldCont, BetterResearchMenuMod.settings.contractingForceMultiplier))
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
            zoom = 1f;
            targetZoom = 1f;
            zoomAnchoring = false;
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
                total = Mathf.Max(1, Mathf.RoundToInt(eraProjects.Count * BetterResearchMenuMod.settings.eraCompletionPercentage));
                finished = eraProjects.Count(x => x.IsFinished);
            }
            return total > 0 ? Mathf.Clamp01((float)finished / total) : 1f;
        }

        private float GetAdvancementProgress(out int finished, out int total) => GetAdvancementProgressRaw(Faction.OfPlayer.def.techLevel, this.CurTab, out finished, out total);

        // Removes defName from the queue, then recursively removes anything that depends on it
		// Recursion is fuckin magical every time I manage to get it working
        private void RemoveFromQueueCascade(string defName)
        {
            if (defName.NullOrEmpty()) return;

            bool removedCurrent = Find.ResearchManager?.currentProj != null && Find.ResearchManager.currentProj.defName == defName;
            if (removedCurrent)
                Traverse.Create(Find.ResearchManager).Field("currentProj").SetValue(null);

            bool removedQueued = State.researchQueue.Remove(defName);
            if (!removedQueued && !removedCurrent) return;

            // Find and remove anything queued that has this as a prerequisite
            var dependents = State.researchQueue
                .Where(d =>
                {
                    var d2 = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(d);
                    return d2 != null && GetPrerequisiteChain(d2).Any(p => p.defName == defName);
                })
                .ToList();
            foreach (var dep in dependents)
                RemoveFromQueueCascade(dep);
        }

        // Returns the ordered list of unfinished prerequisites needed before def, including def itself.
        // Order is leaf-first so queue[0] is always the next thing to research.
        private List<ResearchProjectDef> GetPrerequisiteChain(ResearchProjectDef def)
        {
            var result = new List<ResearchProjectDef>();
            var visited = new HashSet<string>();
            BuildChain(def, result, visited);
            return result;
        }

        private void BuildChain(ResearchProjectDef def, List<ResearchProjectDef> result, HashSet<string> visited)
        {
            if (!visited.Add(def.defName)) return;
            if (def.IsFinished) return;

            if (def.prerequisites != null)
                foreach (var prereq in def.prerequisites)
                    BuildChain(prereq, result, visited);

            if (def.hiddenPrerequisites != null)
                foreach (var prereq in def.hiddenPrerequisites)
                    BuildChain(prereq, result, visited);

            result.Add(def);
        }

        private bool CanQueueDespiteLock(ResearchProjectDef def)
        {
            if (def == null || def.IsFinished) return false;

            // Queueing locked projects is only allowed when the it's cuz a missing research-prerequisite
            // Study techprint, mechanitor, and similar non-research gates are still blocked
            if (!def.TechprintRequirementMet) return false;
            if (!def.PlayerMechanitorRequirementMet) return false;
            if (!def.AnalyzedThingsRequirementsMet) return false;

            return !def.PrerequisitesCompleted
                || (BetterResearchMenuMod.settings.restrictResearchToTechLevel
                    && def.techLevel > Faction.OfPlayer.def.techLevel
                    && def.tab != DefsOf.Anomaly
                    && def.tab != DefsOf.VGE_Gravtech);
        }

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
                if (progress < 1f)
                {
                    if (BetterResearchMenuMod.settings.advancementTiedTo == AdvancementType.Foundations) list.Add("BRM_RequiresAllFoundations".Translate());
                    else list.Add("BRM_RequiresEraCompletion".Translate(BetterResearchMenuMod.settings.eraCompletionPercentage.ToStringPercent()));
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

        private static bool IsMysteryNode(ResearchNode node)
        {
            if (node.isPhantom || node.isGroupNode) return false;
            if (node.state == NodeState.Expanded) return false;
            return !(State.openedNodes?.Contains(node.def.defName) ?? false);
        }

        private bool HasVisibleResearch(ResearchNode node, HashSet<ResearchNode> visited)
        {
            if (!visited.Add(node)) return false;
            foreach (var edge in node.nodeEdges)
            {
                var other = edge.from == node ? edge.to : edge.from;
                if (other.isPhantom) continue;
                if (!other.isGroupNode)
                {
                    if (other.state != NodeState.Hidden) return true;
                }
                else
                {
                    if (HasVisibleResearch(other, visited)) return true;
                }
            }
            return false;
        }
        private void DrawScaledLabel(Rect rect, string text, float zoom, float baseFontSize = 12f)
        {
            if (zoom < 0.4f)
            {
                int fontSize = Mathf.RoundToInt(baseFontSize * (zoom / 0.4f));
                GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = TextAnchor.UpperCenter, wordWrap = true };
                style.normal.textColor = Color.white;
                GUI.Label(rect, text, style);
            }
            else
            {
                Text.Anchor = TextAnchor.UpperCenter;
                Text.Font = GameFont.Tiny;
                Widgets.Label(rect, text);
            }
        }

        private void DrawScaledResearchLabels(ResearchNode node, Vector2 screenPos, float nodeSize, float zoom)
        {
            GameFont currentFont = zoom < 0.6f ? GameFont.Tiny : (zoom > 1.2f ? GameFont.Medium : GameFont.Small);
            float labelWidth = 200f * zoom;
            Rect labelRect = new Rect(screenPos.x - (labelWidth / 2f), screenPos.y + (nodeSize / 2f) + 2f * zoom, labelWidth, 500f);

            if (zoom < 0.4f)
            {
                float scale = zoom / 0.4f;
                int mainFontSize = Mathf.RoundToInt(12f * scale);
                int subFontSize = Mathf.RoundToInt(11f * scale);

                GUIStyle mainStyle = new GUIStyle(GUI.skin.label) { fontSize = mainFontSize, alignment = TextAnchor.UpperCenter, wordWrap = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                mainStyle.normal.textColor = Color.white;

                GUIStyle subStyle = new GUIStyle(GUI.skin.label) { fontSize = subFontSize, alignment = TextAnchor.UpperCenter, wordWrap = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                subStyle.normal.textColor = Color.white;

                float mainHeight = mainStyle.CalcHeight(new GUIContent(node.def.LabelCap), labelWidth);
                GUI.Label(labelRect, node.def.LabelCap, mainStyle);

                Rect subRect = new Rect(labelRect.x, labelRect.y + mainHeight - (1f * zoom), labelRect.width, 500f);
                GUI.Label(subRect, node.cachedSubLabel, subStyle);
            }
            else
            {
                Text.Anchor = TextAnchor.UpperCenter;
                Text.Font = currentFont;
                Widgets.Label(labelRect, node.def.LabelCap);

                float mainHeight = node.GetCachedTitleHeight(currentFont, labelWidth, zoom);
                Text.Font = GameFont.Tiny;
                Rect subRect = new Rect(labelRect.x, labelRect.y + mainHeight, labelRect.width, 500f);
                Widgets.Label(subRect, node.cachedSubLabel);
            }
        }
    }
}